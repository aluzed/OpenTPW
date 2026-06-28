namespace OpenTPW;

/// <summary>
/// The park-wide challenge state machine (T-054): offers a challenge, the player accepts/declines, it runs for
/// <see cref="Challenge.TargetTime"/> in-game days (counted down on <see cref="GameClock.OnNewDay"/>), wins
/// when the tracked metric's <b>gain since accepting</b> reaches <see cref="Challenge.TargetVal"/>, loses on
/// timeout, pays the prize, and offers the next one (a <see cref="Challenge.FollowupType"/> first, else the
/// next in the list). Reversed from the original (`GetCurrentChallenge`/`EndCurrentChallenge`/`mChallengeOn`).
/// <para>The metric + prize-payout are injected and the challenge list is pre-filtered to supported types, so
/// the engine is pure and unit-tested. Drive it by calling <see cref="OnNewDay"/> once per in-game day.</para>
/// </summary>
public sealed class ChallengeManager
{
	/// <summary>The active level's challenge manager (UI/input read this); null before a level loads.</summary>
	public static ChallengeManager? Current { get; set; }

	public enum Phase { Idle, Offered, Active }

	private readonly IReadOnlyList<Challenge> all;       // pre-filtered to supported types
	private readonly Func<Challenge, float> metric;      // absolute current value for a challenge's Type
	private readonly Action<float> payPrize;

	// Offer pacing in in-game days (the original's DaysUntil/After* — sensible defaults).
	public int DaysUntilFirst { get; init; } = 3;
	public int DaysAfterCompleted { get; init; } = 5;
	public int DaysAfterDeclined { get; init; } = 8;

	public Phase State { get; private set; } = Phase.Idle;
	/// <summary>The offered or active challenge (null while Idle).</summary>
	public Challenge? Active { get; private set; }
	/// <summary>In-game days remaining while Active.</summary>
	public int DaysLeft { get; private set; }
	/// <summary>Metric gain since accepting, toward <c>Active.TargetVal</c> (while Active).</summary>
	public float Progress { get; private set; }
	public int Won { get; private set; }
	public int Lost { get; private set; }
	/// <summary>The most recent finished challenge + whether it was won — for a one-shot notification.</summary>
	public (Challenge Challenge, bool Won)? LastResult { get; private set; }

	private int offerCursor;        // next sequential challenge to offer
	private int daysUntilOffer = -1; // countdown while Idle; -1 = seed from DaysUntilFirst on the first day
	private float baseline;         // metric at Accept (challenges measure a delta)
	private int forcedFollowup;     // a FollowupType to offer next (0 = none)

	public ChallengeManager( IReadOnlyList<Challenge> all, Func<Challenge, float> metric, Action<float> payPrize )
	{
		this.all = all;
		this.metric = metric;
		this.payPrize = payPrize;
	}

	/// <summary>Advance one in-game day.</summary>
	public void OnNewDay()
	{
		switch ( State )
		{
			case Phase.Idle:
				if ( daysUntilOffer < 0 )
					daysUntilOffer = DaysUntilFirst; // seeded on the first idle day (after init-properties apply)
				if ( --daysUntilOffer <= 0 )
					OfferNext();
				break;

			case Phase.Active:
				DaysLeft--;
				Progress = metric( Active! ) - baseline;
				bool met = Progress >= Active!.TargetVal;
				if ( met && !Active.CheckAtEndOnly )
					Finish( won: true );
				else if ( DaysLeft <= 0 )
					Finish( won: met );
				break;
		}
	}

	/// <summary>Accept the offered challenge: it becomes active with its deadline + a captured baseline.</summary>
	public void Accept()
	{
		if ( State != Phase.Offered || Active is not { } c )
			return;
		State = Phase.Active;
		DaysLeft = c.TargetTime;
		baseline = metric( c );
		Progress = 0f;
	}

	/// <summary>Decline the offered challenge: nothing happens and another is offered after a delay.</summary>
	public void Decline()
	{
		if ( State != Phase.Offered )
			return;
		Active = null;
		State = Phase.Idle;
		daysUntilOffer = DaysAfterDeclined;
	}

	private void Finish( bool won )
	{
		var c = Active!;
		LastResult = (c, won);
		if ( won )
		{
			Won++;
			payPrize( c.Prize );
			if ( c.FollowupType != 0 )
				forcedFollowup = c.FollowupType;
		}
		else
		{
			Lost++;
		}
		Active = null;
		State = Phase.Idle;
		daysUntilOffer = DaysAfterCompleted;
	}

	private void OfferNext()
	{
		var next = PickNext();
		if ( next is null )
		{
			daysUntilOffer = DaysAfterCompleted; // out of challenges — idle, check back later
			return;
		}
		Active = next;
		State = Phase.Offered;
	}

	private Challenge? PickNext()
	{
		if ( forcedFollowup != 0 )
		{
			int ft = forcedFollowup;
			forcedFollowup = 0;
			var f = all.FirstOrDefault( c => c.Type == ft );
			if ( f != null )
				return f;
		}
		return offerCursor < all.Count ? all[offerCursor++] : null;
	}
}
