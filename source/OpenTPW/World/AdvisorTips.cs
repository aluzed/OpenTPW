namespace OpenTPW;

/// <summary>
/// Player-facing English text for each advisor advice id (T-046). The original's per-message speech is a
/// runtime-assembled message database (the clip→text binding isn't a liftable table — see T-046's RE note),
/// so OpenTPW carries its own readable tip text keyed by the advice id the rule-engine raises
/// (<see cref="AdvisorAdvice.Evaluate"/>). This turns the talking bug head from decoration into a useful
/// hint: the HUD shows what the advisor is actually telling you.
/// </summary>
public static class AdvisorTips
{
	private static readonly Dictionary<string, string> Tips = new()
	{
		["WelcomeTutorial"] = "Welcome to your park! Place a few rides and shops, then open the gates.",
		["InTheRedSixMonths"] = "You've lost money for six months — raise the gate fee or sell off a ride before you go bust!",
		["InTheRedThreeMonths"] = "Three months in the red. Push up your prices or trim staff wages.",
		["InTheRedMonthLeft"] = "Money's tight this month — keep an eye on your wages and upkeep.",
		["VisitorsThirsty"] = "Your visitors are getting thirsty. Build a drink stall!",
		["VisitorsHungry"] = "Your visitors are hungry. Build a food stall!",
		["NewResearchGroupRide"] = "New research is available — put a researcher to work to unlock ride upgrades.",
		["CongratVisitorsHappy"] = "Your visitors are having a wonderful time — keep it up!",
	};

	/// <summary>The readable tip for an advice id, or a sensible fallback if it isn't mapped yet.</summary>
	public static string TextFor( string messageId )
	{
		if ( string.IsNullOrEmpty( messageId ) )
			return "";
		return Tips.TryGetValue( messageId, out var text ) ? text : "The advisor has something to say...";
	}

	/// <summary>Whether a tip id has authored text (vs. the generic fallback) — for tests.</summary>
	public static bool Has( string messageId ) => Tips.ContainsKey( messageId );
}
