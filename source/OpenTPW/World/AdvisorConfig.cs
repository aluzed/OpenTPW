using System.Globalization;

namespace OpenTPW;

/// <summary>
/// The advisor's tuning config, parsed from <c>Advisor/Advisor.sam</c> — the EA text key/value format shared
/// with ride <c>.sam</c> files (via <see cref="SettingsFile"/>/<c>SAMParser</c>). It carries the global pacing
/// rules (<see cref="MinTimeAnyMessage"/> / <see cref="MinTimeSameMessage"/> / <see cref="MinScoreForConsideration"/>),
/// the per-group message rules (<c>MessageGroups[N].*</c> → <see cref="Group"/>), and a generic accessor for
/// the ~200 per-advice scoring parameters (<see cref="Param"/>) the message scheduler (<see cref="AdvisorMessages"/>)
/// reads to decide which tip to say. Fault-tolerant: a missing key falls back to its documented default, so a
/// stripped or absent file still yields a usable config. See docs/tickets/T-046.
/// </summary>
public sealed class AdvisorConfig
{
	/// <summary>One message group's rules (<c>MessageGroups[Index].*</c>): the minimum gap before the same
	/// message repeats, whether it may be said only once, and how many player slaps retire it.</summary>
	public sealed record MessageGroup( int Index, float MinTimeSameMessage, bool SayOnlyOnce, int DiscardAfterSlaps );

	/// <summary>Minimum seconds between <i>any</i> two advisor messages (<c>GeneralAdvisor.MinTimeAnyMessage</c>).</summary>
	public float MinTimeAnyMessage { get; }

	/// <summary>Default minimum seconds before the same message repeats when its group doesn't override it
	/// (<c>GeneralAdvisor.MinTimeSameMessage</c>).</summary>
	public float MinTimeSameMessage { get; }

	/// <summary>The lowest relevance score worth saying (<c>GeneralAdvisor.MinScoreForConsideration</c>).</summary>
	public float MinScoreForConsideration { get; }

	private readonly Dictionary<int, MessageGroup> groups;
	private readonly Dictionary<string, Dictionary<string, float>> advice; // adviceName → (param → value)

	/// <summary>The defined message groups (sparse — the file defines 0-5 and 9), ordered by index.</summary>
	public IReadOnlyList<MessageGroup> MessageGroups =>
		groups.Values.OrderBy( g => g.Index ).ToList();

	/// <summary>The rules for group <paramref name="index"/>; a permissive default (the global same-message
	/// gap, repeatable, never auto-discarded) when the file doesn't define it.</summary>
	public MessageGroup Group( int index ) =>
		groups.TryGetValue( index, out var g ) ? g : new MessageGroup( index, MinTimeSameMessage, false, 0 );

	/// <summary>A per-advice scoring parameter, e.g. <c>Param("NewResearchGroupRide", "Score")</c>; null when
	/// the advice or key isn't present.</summary>
	public float? Param( string advice, string key ) =>
		advice != null && this.advice.TryGetValue( advice, out var p ) && p.TryGetValue( key, out var v )
			? v : null;

	/// <summary>Build the config from parsed <c>.sam</c> entries (decoupled from file IO for testability).</summary>
	public AdvisorConfig( IEnumerable<SettingsPair> entries )
	{
		groups = new();
		advice = new();

		// Documented defaults (used when the file omits a key or is absent entirely).
		float anyGap = 5f, sameGap = 120f, minScore = 25f;
		var groupFields = new Dictionary<int, Dictionary<string, float>>();

		foreach ( var pair in entries )
		{
			if ( string.IsNullOrEmpty( pair.Key ) || !TryFloat( pair.Value, out var num ) )
				continue;

			int dot = pair.Key.IndexOf( '.' );
			if ( dot <= 0 || dot >= pair.Key.Length - 1 )
				continue;
			var owner = pair.Key.Substring( 0, dot );
			var field = pair.Key.Substring( dot + 1 );

			if ( owner.Equals( "GeneralAdvisor", StringComparison.OrdinalIgnoreCase ) )
			{
				if ( field.Equals( "MinTimeAnyMessage", StringComparison.OrdinalIgnoreCase ) ) anyGap = num;
				else if ( field.Equals( "MinTimeSameMessage", StringComparison.OrdinalIgnoreCase ) ) sameGap = num;
				else if ( field.Equals( "MinScoreForConsideration", StringComparison.OrdinalIgnoreCase ) ) minScore = num;
			}
			else if ( TryGroupIndex( owner, out int gi ) )
			{
				if ( !groupFields.TryGetValue( gi, out var f ) )
					groupFields[gi] = f = new Dictionary<string, float>( StringComparer.OrdinalIgnoreCase );
				f[field] = num;
			}
			else
			{
				if ( !advice.TryGetValue( owner, out var p ) )
					advice[owner] = p = new Dictionary<string, float>( StringComparer.OrdinalIgnoreCase );
				p[field] = num;
			}
		}

		MinTimeAnyMessage = anyGap;
		MinTimeSameMessage = sameGap;
		MinScoreForConsideration = minScore;

		foreach ( var (index, f) in groupFields )
		{
			float same = f.TryGetValue( "MinTimeSameMessage", out var s ) ? s : sameGap;
			bool once = f.TryGetValue( "SayOnlyOnce", out var o ) && o != 0f;
			int slaps = f.TryGetValue( "DiscardAfterSlaps", out var d ) ? (int)d : 0;
			groups[index] = new MessageGroup( index, same, once, slaps );
		}
	}

	/// <summary>Load <c>Advisor/Advisor.sam</c> from the game VFS; returns a defaults-only config (never null)
	/// when the file is missing or unreadable, so the advisor still runs.</summary>
	public static AdvisorConfig Load()
	{
		try
		{
			using var stream = FileSystem.OpenRead( "Advisor/Advisor.sam" );
			if ( stream != null )
				return new AdvisorConfig( new SettingsFile( stream ).Entries );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[advisor] config load failed: {e.Message}" );
		}
		return new AdvisorConfig( Array.Empty<SettingsPair>() );
	}

	// "MessageGroups[3]" → 3. False for anything that isn't a bracketed-index group key.
	private static bool TryGroupIndex( string owner, out int index )
	{
		index = -1;
		const string prefix = "MessageGroups[";
		if ( !owner.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) || !owner.EndsWith( "]" ) )
			return false;
		var inner = owner.Substring( prefix.Length, owner.Length - prefix.Length - 1 );
		return int.TryParse( inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out index );
	}

	private static bool TryFloat( string value, out float result ) =>
		float.TryParse( value, NumberStyles.Float, CultureInfo.InvariantCulture, out result );
}
