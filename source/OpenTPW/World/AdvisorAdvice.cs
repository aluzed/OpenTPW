namespace OpenTPW;

/// <summary>A snapshot of the park state the advisor reasons over each consideration tick (T-046): the few
/// figures its tips key off, captured once so the rule evaluation stays pure.</summary>
public readonly record struct ParkSnapshot(
	float Money,
	int MonthsInRed,
	int ThirstyVisitors,
	int HungryVisitors,
	float AverageHappiness,
	bool ResearchAvailable,
	// Edge-triggered milestone flags (the snapshot builder sets these only on the tick the event happens, so the
	// advisor reacts once): the golden ticket was just won (T-055), a challenge was just won / just lost (T-054).
	// Trailing defaults keep existing callers + tests compiling.
	bool GoldenTicketWon = false,
	bool ChallengeWon = false,
	bool ChallengeLost = false );

/// <summary>
/// Maps the live park state to advisor message candidates — <c>(id, group, score)</c> for the
/// <see cref="AdvisorMessages"/> scheduler — using the scoring parameters decoded from <c>Advisor.sam</c>
/// (<see cref="AdvisorConfig.Param"/>). The advice→group assignment mirrors the original's hardcoded grouping
/// (the file's comment sections: general / tutorial / congrats / research). Pure (unit-tested), so the
/// rules are decoupled from how the snapshot is gathered. See docs/tickets/T-046.
/// </summary>
public static class AdvisorAdvice
{
	// Message group per advice family (Advisor.sam groups: 0 general, 1 tutorial, 3 congrats, 4 research).
	public const int GroupGeneral = 0, GroupTutorial = 1, GroupCongrats = 3, GroupResearch = 4;

	// Default scores when the .sam omits the key (matches the shipped jungle file).
	private const float RedMonthScore = 110f, RedThreeScore = 50f, RedSixScore = 75f;
	private const float ThirstyPerPerson = 1f, HungryPerPerson = 1f, ResearchScore = 40f, CongratScore = 60f;
	private const float CongratHappiness = 75f; // average happiness worth a "well done"
	// Milestone reactions (T-054/T-055): winning the level's golden ticket is the loudest tip; a challenge
	// win/loss is a notable-but-lesser event.
	private const float GoldenTicketScore = 200f, ChallengeWonScore = 90f, ChallengeLostScore = 70f;

	/// <summary>The candidate tips justified by <paramref name="s"/>, scored via <paramref name="c"/>.</summary>
	public static List<(string Id, int Group, float Score)> Evaluate( ParkSnapshot s, AdvisorConfig c )
	{
		var list = new List<(string, int, float)>();

		// Escalating "in the red" warning by how long the park has been losing money (the higher MonthsInRed
		// score wins once the park has been in debt long enough).
		if ( s.Money < 0f )
		{
			if ( s.MonthsInRed >= 6 )
				list.Add( ("InTheRedSixMonths", GroupGeneral, c.Param( "InTheRedSixMonths", "Score" ) ?? RedSixScore) );
			else if ( s.MonthsInRed >= 3 )
				list.Add( ("InTheRedThreeMonths", GroupGeneral, c.Param( "InTheRedThreeMonths", "Score" ) ?? RedThreeScore) );
			else
				list.Add( ("InTheRedMonthLeft", GroupGeneral, c.Param( "InTheRedMonthLeft", "Score" ) ?? RedMonthScore) );
		}

		// Visitors who can't get a drink / food: score per affected visitor (so a thirstier crowd shouts louder).
		if ( s.ThirstyVisitors > 0 )
			list.Add( ("VisitorsThirsty", GroupGeneral,
				(c.Param( "VisitorsThirsty", "ScorePerThirstyPerson" ) ?? ThirstyPerPerson) * s.ThirstyVisitors) );
		if ( s.HungryVisitors > 0 )
			list.Add( ("VisitorsHungry", GroupGeneral,
				(c.Param( "VisitorsHungry", "ScorePerHungryPerson" ) ?? HungryPerPerson) * s.HungryVisitors) );

		// New research finished and ready to apply.
		if ( s.ResearchAvailable )
			list.Add( ("NewResearchGroupRide", GroupResearch, c.Param( "NewResearchGroupRide", "Score" ) ?? ResearchScore) );

		// Congratulate a happy park.
		if ( s.AverageHappiness >= CongratHappiness )
			list.Add( ("CongratVisitorsHappy", GroupCongrats, c.Param( "CongratVisitorsHappy", "Score" ) ?? CongratScore) );

		// One-shot milestone reactions (the builder edge-triggers these so they speak once).
		if ( s.GoldenTicketWon )
			list.Add( ("CongratGoldenTicket", GroupCongrats, c.Param( "CongratGoldenTicket", "Score" ) ?? GoldenTicketScore) );
		if ( s.ChallengeWon )
			list.Add( ("CongratChallengeWon", GroupCongrats, c.Param( "CongratChallengeWon", "Score" ) ?? ChallengeWonScore) );
		if ( s.ChallengeLost )
			list.Add( ("ChallengeFailed", GroupGeneral, c.Param( "ChallengeFailed", "Score" ) ?? ChallengeLostScore) );

		return list;
	}
}
