using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using JetBrains.Annotations;
using Landfall.Haste;
using Landfall.Modding;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Localization;
using Zorro.Settings;

namespace dev.alice_is.LandingQualifier;

[LandfallPlugin]
[SuppressMessage("ReSharper", "RedundantDefaultMemberInitializer")]
public class LandingQualifier {

	public static float                     lastAngle              = -1;
	public static float                     lastScore              = -1;
	public static ScoreStyle                style                  = ScoreStyle.Vanilla;
	public static bool                      extras                 = false;
	public static bool                      invertLandingPrecision = false;
	public static bool                      overrideThresholds     = false;
	public static LandingPrecisionFixState  landingPrecisionFix    = LandingPrecisionFixState.Off;
	
	static LandingQualifier() {
		On.PlayerMovement.GetLanding += (prev, movement, hit) => {
			var res = prev.Invoke(movement, hit)!;
			var rig = movement.GetComponent<Rigidbody>();
			if (landingPrecisionFix != LandingPrecisionFixState.Off) fixLanding(res, hit);
			lastAngle = Vector3.Angle(rig.velocity, hit.normal);
			lastScore = (float)res.GetType().GetField("landingScore").GetValue(res);
			return res;
		};

		var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
		
		On.UI_LandingScore.Land += (prev, _this, landingType, saved) => {
			if (style == ScoreStyle.Vanilla && !extras) {
				prev(_this, landingType, saved);
				return;
			}
			string str;
			if (extras && lastScore > 0.9995) {
				str = "Impeccable!!";
			} else if (extras && lastScore > 0.9945) {
				str = "Pure Perfect!";
			} else if (!saved) {
				str = (_this.GetType().GetField(landingType switch {
					LandingType.Bad     => "m_badLanding",
					LandingType.Ok      => "m_okLanding",
					LandingType.Good    => "m_goodLanding",
					LandingType.Perfect => "m_perfectLanding",
					_                   => throw new ArgumentOutOfRangeException(nameof(landingType), landingType, null)
				}, flags)!.GetValue(_this) as LocalizedString)!.GetLocalizedString();
			} else str = $"\"{(_this.GetType().GetField("m_perfectLanding", flags)!.GetValue(_this) as LocalizedString)!.GetLocalizedString()}\"";
			str += style switch {
				ScoreStyle.Angle => $" ({Math.Abs(90 - lastAngle):f1}°)",
				ScoreStyle.Score => $" ({lastScore * 100:f1}%)",
				_ => $" ({Math.Abs(90 - lastAngle):f1}°, {lastScore * 100:f1}%)"
			};
			/*str += $"""
			{last.t}
			{last.x}
			{last.speedBefore}
			{last.speedAfterBefore}
			{last.speedAfterAfter}
			""";*/
			(_this.GetType().GetField("text", flags)!.GetValue(_this) as TextMeshProUGUI)!.text = str;
			// prev(_this, LandingType.None, false);
			_this.GetComponent<Animator>().speed = 0.5f;
			if(landingType != LandingType.None) _this.GetComponent<Animator>().Play(landingType switch {
				LandingType.Bad     => "An_UI_Bad",
				LandingType.Ok      => "An_UI_OK",
				LandingType.Good    => "An_UI_Good",
				LandingType.Perfect => "An_UI_Perfect",
				_                   => ""
			}, 0, 0);
		};
	}
	
	struct save {
		public float x;
		public float t;
		public float speedBefore;
		public float speedAfterBefore;
		public float speedAfterAfter;
	}

	private static save last;
	private static void fixLanding(object landing, RaycastHit hit) {
		var velBefore = (Vector3)landing.GetType().GetField("velBefore")!.GetValue(landing)!;
		var speedBefore = velBefore.magnitude;
		var a = invertLandingPrecision? (0.65f, 1f): (1f, 0.65f);
		var t = Mathf.Lerp(a.Item1, a.Item2, GameDifficulty.currentDif.landingPresicion);
		var velAfter = Vector3.ProjectOnPlane(velBefore, hit.normal);
		var x = t + speedBefore * (1 - t) / velAfter.magnitude;
		if(landingPrecisionFix == LandingPrecisionFixState.Speed) velAfter.Scale(new(x, x, x));
		landing.GetType().GetField("velAfter")!.SetValue(landing, velAfter);
		var speedAfter        = velAfter.magnitude;
		last                  = new();
		last.t                = t;
		last.x                = x;
		last.speedBefore      = speedBefore;
		last.speedAfterBefore = (float)landing.GetType().GetField("speedAfter")!.GetValue(landing);
		last.speedAfterAfter  = speedAfter;
		var landingScore      = speedAfter * (landingPrecisionFix == LandingPrecisionFixState.Score ? x : 1) / speedBefore;
		landing.GetType().GetField("speedAfter")!.SetValue(landing, speedAfter);
		landing.GetType().GetField("landingScore")!.SetValue(landing, landingScore);
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
	Off,
	Score,
	[UsedImplicitly]
	Speed,
}

[HasteSetting]
[UsedImplicitly]
public class LandingPrecisionFix: EnumSetting<LandingPrecisionFixState>, IExposedSetting {
	public override void ApplyValue() {
		LandingQualifier.landingPrecisionFix = Value;
	}

	public string GetCategory() => "lQ";
	protected override LandingPrecisionFixState GetDefaultValue() => LandingPrecisionFixState.Off;
	public LocalizedString GetDisplayName() => new UnlocalizedString("Landing Precision Difficulty Fix");
		
	public override List<LocalizedString> GetLocalizedChoices() => [
		new UnlocalizedString("Off"),
		new UnlocalizedString("Score Only"),
		new UnlocalizedString("Speed and Score"),
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
	
[HasteSetting]
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
	
[HasteSetting]
[UsedImplicitly]
public class InvertLandingPrecision: OffOnSetting, IExposedSetting {
	public override void ApplyValue() {
		LandingQualifier.invertLandingPrecision = Value == OffOnMode.ON;
	}

	public string GetCategory() => "lQ";
	protected override OffOnMode GetDefaultValue() => OffOnMode.ON;
	public LocalizedString GetDisplayName() => new UnlocalizedString("Invert Landing Precision Setting (so that 0% is easier than 100%)");
		
	public override List<LocalizedString> GetLocalizedChoices() => [
		new UnlocalizedString("Off"),
		new UnlocalizedString("On"),
	];
}

	
[HasteSetting]
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


