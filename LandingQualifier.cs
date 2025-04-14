// ReSharper disable MemberCanBePrivate.Global
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using JetBrains.Annotations;
using Landfall.Haste;
using Landfall.Modding;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Localization;
using Zorro.Core.CLI;
using Zorro.Settings;
using Debug = UnityEngine.Debug;

namespace dev.alice_is.LandingQualifier;

[LandfallPlugin]
[SuppressMessage("ReSharper", "RedundantDefaultMemberInitializer")]
public static class LandingQualifier {

	public static ScoreStyle                style               = ScoreStyle.Vanilla;
	public static bool                      extras              = false;
	public static bool                      moreLandingsCompat  = false;
	public static bool                      overrideThresholds  = false;
	public static LandingPrecisionFixState  landingPrecisionFix = LandingPrecisionFixState.Vanilla;

	public static void log(object message) {
		switch (message) {
		case Exception:
			Debug.Log("[Landing Qualifier]");
			Debug.Log(message);
		break;
		default:
			Debug.Log($"[Landing Qualifier] {message}");
		break;
		}
	}

	static LandingQualifier() {
		On.PlayerMovement.GetLanding += (prev, movement, hit) => {
			var res = prev.Invoke(movement, hit)!;
			var rig = movement.GetComponent<Rigidbody>();
			if (landingPrecisionFix != LandingPrecisionFixState.Vanilla) fixLanding(res, hit);
			last.angle        = Vector3.Angle(rig.velocity, hit.normal);
			last.landingScore = (float)res.GetType().GetField("landingScore").GetValue(res);
			return res;
		};

		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
		
		On.UI_LandingScore.Land += (prev, _this, landingType, saved) => {
			prev(_this, landingType, saved);
			if (style == ScoreStyle.Vanilla && !extras) return;
			var text = (_this.GetType().GetField("text", flags)!.GetValue(_this) as TextMeshProUGUI)!;
			var len = text.text.Length;
			var str = extras switch {
				true when last.landingScore > 0.9995 => "Impeccable!!",
				true when last.landingScore > 0.9945 => "Pure Perfect!",
				_                            => text.text
			} + style switch {
				ScoreStyle.Angle => $" ({Math.Abs(90 - last.angle):f1}°)",
				ScoreStyle.Score => $" ({last.landingScore * 100:f1}%)",
				_ => $" ({Math.Abs(90 - last.angle):f1}°, {last.landingScore * 100:f1}%)"
			};
			text.text = str;
			_this.GetComponent<Animator>().speed *= 1.0f * len / str.Length;
		};
	}
	
	[SuppressMessage("ReSharper", "NotAccessedField.Local")]
	public record struct Save {
		public float x;
		public float t;
		public float angle;
		public float speedBefore;
		public float speedAfterBefore;
		public float speedAfterAfter;
		public float landingScore;
	}

	[SuppressMessage("ReSharper", "NotAccessedField.Local")]
	private static Save last;
	private static void fixLanding(object landing, RaycastHit hit) {
		var velBefore = (Vector3)landing.GetType().GetField("velBefore")!.GetValue(landing)!;
		var speedBefore = velBefore.magnitude;
		var t = Mathf.Lerp(0.65f, 1, GameDifficulty.currentDif.landingPresicion);
		var velAfter = Vector3.ProjectOnPlane(velBefore, hit.normal);
		var x = t + speedBefore * (1 - t) / velAfter.magnitude;
		if(landingPrecisionFix is LandingPrecisionFixState.Speed or LandingPrecisionFixState.SpeedNoVanilla) velAfter.Scale(new(x, x, x));
		var speedAfter        = velAfter.magnitude;
		var landingScore      = speedAfter * (landingPrecisionFix is LandingPrecisionFixState.Score or LandingPrecisionFixState.ScoreNoVanilla ? x : 1) / speedBefore;
		last                  = new Save {
			t = t,
			x = x,
			speedBefore = speedBefore,
			speedAfterBefore = (float)landing.GetType().GetField("speedAfter")!.GetValue(landing),
			speedAfterAfter = speedAfter,
		};
		landing.GetType().GetField("velAfter")!.SetValue(landing, velAfter);
		landing.GetType().GetField("speedAfter")!.SetValue(landing, speedAfter);
		landing.GetType().GetField("landingScore")!.SetValue(landing, landingScore);
	}
	
	// [ConsoleCommand]
	[UsedImplicitly]
	public static void tryLoad(string Class) {
		log(Class);
		try {
			var loaded = Type.GetType(Class);
			log(loaded!.Name);
		} catch (Exception e) {
			log(e);
		}
	}
}

public enum ScoreStyle {
	Vanilla,
	Score,
	Angle,
	[UsedImplicitly]
	ScoreAndAngle
}
	
[HasteSetting]
[UsedImplicitly]
public class StyleSetting: EnumSetting<ScoreStyle>, IExposedSetting {
	public override void ApplyValue() {
		LandingQualifier.style = Value;
	}

	public string GetCategory() => "lQ";
	protected override ScoreStyle GetDefaultValue() => ScoreStyle.Score;
	public LocalizedString GetDisplayName() => new UnlocalizedString("Score Style");
		
	public override List<LocalizedString> GetLocalizedChoices() {
		return [
			new UnlocalizedString("Vanilla"),
			new UnlocalizedString("Score"),
			new UnlocalizedString("Angle"),
			new UnlocalizedString("ScoreAndAngle"),
		];
	}
}

public enum LandingPrecisionFixState {
	Vanilla,
	Score,
	Speed,
	ScoreNoVanilla,
	SpeedNoVanilla,
}

[HasteSetting]
[UsedImplicitly]
public class LandingPrecisionFix: EnumSetting<LandingPrecisionFixState>, IExposedSetting {
	public override void ApplyValue() {
		LandingQualifier.landingPrecisionFix = Value;
	}

	public string GetCategory() => "lQ";
	protected override LandingPrecisionFixState GetDefaultValue() => LandingPrecisionFixState.Vanilla;
	public LocalizedString GetDisplayName() => new UnlocalizedString("Landing Precision Difficulty Fix");
		
	public override List<LocalizedString> GetLocalizedChoices() => [
		new UnlocalizedString("Vanilla"),
		new UnlocalizedString("Score Only"),
		new UnlocalizedString("Speed and Score"),
		new UnlocalizedString("Score Only, No vanilla"),
		new UnlocalizedString("Speed and Score, No vanilla"),
	];
}

[HasteSetting]
[UsedImplicitly]
public class EnableExtraQualities: OffOnSetting, IExposedSetting {
	public override void ApplyValue() {
		LandingQualifier.extras = Value == OffOnMode.ON;
	}

	public string GetCategory() => "lQ";
	protected override OffOnMode GetDefaultValue() => OffOnMode.OFF;
	public LocalizedString GetDisplayName() => new UnlocalizedString("Enable extra landing qualities");
		
	public override List<LocalizedString> GetLocalizedChoices() => [
		new UnlocalizedString("Off"),
		new UnlocalizedString("On"),
	];
}

// [HasteSetting]
[UsedImplicitly]
public class EnableThresholdOverrides: OffOnSetting, IExposedSetting {
	public override void ApplyValue() {
		LandingQualifier.overrideThresholds = Value == OffOnMode.ON;
	}

	public string GetCategory() => "lQ";
	protected override OffOnMode GetDefaultValue() => OffOnMode.OFF;
	public LocalizedString GetDisplayName() => new UnlocalizedString("Enable Landing Precision Override");
		
	public override List<LocalizedString> GetLocalizedChoices() => [
		new UnlocalizedString("Off"),
		new UnlocalizedString("On"),
	];
}

// [HasteSetting]
[UsedImplicitly]
public class PerfectThreshold: FloatSetting, IExposedSetting {
	public static Fact easyPerfectsUsed = new("runUsedEasyPerfects");
	public override void ApplyValue() {
		FactSystem.SetFact(easyPerfectsUsed, Math.Min(FactSystem.GetFact(easyPerfectsUsed), Value));
	}

	public string GetCategory() => "lQ";
	protected override float GetDefaultValue() => 0.95f;
	protected override float2 GetMinMaxValue() => new(0, 1);
	public LocalizedString GetDisplayName() => new UnlocalizedString("Score Style");
}
	
// [HasteSetting]
[UsedImplicitly]
public class IngameStats: OffOnSetting, IExposedSetting {
	public override void ApplyValue() {
		LandingQualifier.moreLandingsCompat = Value == OffOnMode.ON;
	}

	public string GetCategory() => "lQ";
	protected override OffOnMode GetDefaultValue() => OffOnMode.ON;
	public LocalizedString GetDisplayName() => new UnlocalizedString("Enable More Landings Compatibility");
		
	public override List<LocalizedString> GetLocalizedChoices() => [
		new UnlocalizedString("Off"),
		new UnlocalizedString("On"),
	];
}


