namespace OpenTPW;

/// <summary>
/// The advisor's message scheduler (T-046). Each consideration tick the game submits candidate tips — a
/// message id, its <see cref="AdvisorConfig.MessageGroup"/> and a relevance score — and <see cref="Consider"/>
/// picks the single best one to say, enforcing the real pacing rules decoded from <c>Advisor.sam</c>:
/// <list type="bullet">
/// <item>a global gap between <i>any</i> two messages (<see cref="AdvisorConfig.MinTimeAnyMessage"/>);</item>
/// <item>a per-group gap before the <i>same</i> message repeats (<c>MinTimeSameMessage</c>);</item>
/// <item>say-once messages (<c>SayOnlyOnce</c>);</item>
/// <item>a minimum score worth saying (<see cref="AdvisorConfig.MinScoreForConsideration"/>);</item>
/// <item>retiring a message the player slaps away too often (<c>DiscardAfterSlaps</c>).</item>
/// </list>
/// Time is injected (seconds) and there is no global state, so the whole thing is unit-testable.
/// </summary>
public sealed class AdvisorMessages
{
	private readonly AdvisorConfig config;
	private float lastAnyTime = float.NegativeInfinity; // when any message last fired

	private sealed class State
	{
		public float LastSaid = float.NegativeInfinity;
		public int TimesSaid;
		public int Slaps;
		public bool Discarded;
	}

	private readonly Dictionary<string, State> state = new();
	private readonly List<(string Id, AdvisorConfig.MessageGroup Group, float Score)> candidates = new();

	public AdvisorMessages( AdvisorConfig config ) => this.config = config;

	/// <summary>The message id chosen by the most recent <see cref="Consider"/>, or null if nothing fired.</summary>
	public string? Active { get; private set; }

	/// <summary>Offer a candidate tip for this tick. Call once per relevant message before <see cref="Consider"/>.</summary>
	public void Submit( string id, AdvisorConfig.MessageGroup group, float score ) =>
		candidates.Add( (id, group, score) );

	/// <summary>Pick the best eligible candidate at <paramref name="now"/> (seconds), mark it said, and return
	/// its id — or null when nothing qualifies. Always clears the candidate list for the next tick.</summary>
	public string? Consider( float now )
	{
		string? chosen = null;

		// Global gap: stay quiet for a beat after the last message, however urgent the candidate.
		if ( now - lastAnyTime >= config.MinTimeAnyMessage )
		{
			(string Id, AdvisorConfig.MessageGroup Group, float Score)? best = null;
			foreach ( var c in candidates )
			{
				if ( c.Score < config.MinScoreForConsideration )
					continue;
				if ( !Eligible( c.Id, c.Group, now ) )
					continue;
				if ( best is null || c.Score > best.Value.Score )
					best = c;
			}

			if ( best is { } b )
			{
				chosen = b.Id;
				var st = StateOf( chosen );
				st.LastSaid = now;
				st.TimesSaid++;
				lastAnyTime = now;
			}
		}

		candidates.Clear();
		Active = chosen;
		return chosen;
	}

	// A candidate may be said now if it isn't retired, isn't a spent say-once, and its group's same-message
	// gap has elapsed since it last fired.
	private bool Eligible( string id, AdvisorConfig.MessageGroup group, float now )
	{
		var st = StateOf( id );
		if ( st.Discarded )
			return false;
		if ( group.SayOnlyOnce && st.TimesSaid > 0 )
			return false;
		return now - st.LastSaid >= group.MinTimeSameMessage;
	}

	/// <summary>Record that the player slapped <paramref name="id"/> away; once it reaches the group's
	/// <c>DiscardAfterSlaps</c> (when that's &gt; 0) the message is retired for the session.</summary>
	public void Slap( string id, AdvisorConfig.MessageGroup group )
	{
		var st = StateOf( id );
		st.Slaps++;
		if ( group.DiscardAfterSlaps > 0 && st.Slaps >= group.DiscardAfterSlaps )
			st.Discarded = true;
	}

	/// <summary>How many times <paramref name="id"/> has been said.</summary>
	public int TimesSaid( string id ) => state.TryGetValue( id, out var s ) ? s.TimesSaid : 0;

	/// <summary>Whether <paramref name="id"/> has been retired (slapped away too often).</summary>
	public bool IsDiscarded( string id ) => state.TryGetValue( id, out var s ) && s.Discarded;

	private State StateOf( string id ) => state.TryGetValue( id, out var s ) ? s : state[id] = new State();
}
