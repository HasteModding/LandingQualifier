using System.Reflection;
using Landfall.Haste;
using Landfall.Modding;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using Zorro.Settings;

namespace dev.alice_is.LandingQualifier;

[LandfallPlugin]
public class LandingQualifier {

	public static float lastAngle = -1;
	public static float lastScore = -1;
	public static State style = State.Vanilla;
	
	static LandingQualifier() {
		On.PlayerMovement.GetLanding += (prev, movement, hit) => {
			var res = prev.Invoke(movement, hit);
			var rig = movement.GetComponent<Rigidbody>();
			lastAngle = Vector3.Angle(rig.velocity, hit.normal);
			lastScore = (float)res.GetType().GetField("landingScore").GetValue(res) ;
			return res;
		};

		var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
		
		On.UI_LandingScore.Land += (prev, _this, landingType, saved) => {
			if (style == State.Vanilla) {
				prev(_this, landingType, saved);
				return;
			}
			string str;
			if (!saved) {
				str = (_this.GetType().GetField(landingType switch {
					LandingType.Bad     => "m_badLanding",
					LandingType.Ok      => "m_okLanding",
					LandingType.Good    => "m_goodLanding",
					LandingType.Perfect => "m_perfectLanding",
					_                   => throw new ArgumentOutOfRangeException(nameof(landingType), landingType, null)
				}, flags)!.GetValue(_this) as LocalizedString)!.GetLocalizedString();
			} else str = $"\"{(_this.GetType().GetField("m_perfectLanding", flags)!.GetValue(_this) as LocalizedString)!.GetLocalizedString()}\"";
			str += style switch {
				State.Angle => $" ({Math.Abs(90 - lastAngle):f1}°)",
				State.Score => $" ({lastScore * 100:f1}%)",
				_ => $" ({Math.Abs(90 - lastAngle):f1}°, {lastScore * 100:f1}%)"
			};
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
}

public enum State {
	Vanilla,
	Score,
	Angle,
	ScoreAndAngle
}
	
[HasteSetting]
public class MulliganEnabledSetting : EnumSetting<State>, IExposedSetting {
	public override void ApplyValue() {
		LandingQualifier.style = Value;
	}

	public string GetCategory() => "lQ";
	protected override State GetDefaultValue() => State.Score;
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