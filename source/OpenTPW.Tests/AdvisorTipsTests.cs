using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class AdvisorTipsTests
{
	[TestMethod]
	public void EveryAdviceIdRaisedByTheRuleEngineHasText()
	{
		// The ids AdvisorAdvice.Evaluate can raise (+ the opening tutorial) must all have authored tips.
		foreach ( var id in new[]
		{
			"WelcomeTutorial", "InTheRedSixMonths", "InTheRedThreeMonths", "InTheRedMonthLeft",
			"VisitorsThirsty", "VisitorsHungry", "NewResearchGroupRide", "CongratVisitorsHappy",
		} )
			Assert.IsTrue( AdvisorTips.Has( id ), $"missing advisor tip text for '{id}'" );
	}

	[TestMethod]
	public void TextForGivesTheTipAndSensibleFallbacks()
	{
		Assert.IsTrue( AdvisorTips.TextFor( "VisitorsThirsty" ).Contains( "drink", System.StringComparison.OrdinalIgnoreCase ) );
		Assert.AreEqual( "", AdvisorTips.TextFor( "" ), "no message → no text" );
		Assert.IsFalse( string.IsNullOrEmpty( AdvisorTips.TextFor( "SomeUnmappedId" ) ), "unknown id → a generic line, not empty" );
	}
}
