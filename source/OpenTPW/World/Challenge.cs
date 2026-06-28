using System.Globalization;

namespace OpenTPW;

/// <summary>
/// One entry from <c>Challenges.sam</c> (T-054): a timed park goal with a cash prize. Fields are the original's
/// (`Challenges[n].*`): <see cref="Type"/> (what to achieve — see <see cref="ChallengeMetrics"/>),
/// <see cref="TargetVal"/> (how much) over <see cref="TargetTime"/> in-game days, <see cref="Prize"/> on
/// success, optional <see cref="FollowupType"/> (the Type of the challenge offered next on a win) and
/// <see cref="CheckAtEndOnly"/> (validate at the deadline vs continuously). Pure data + a parser.
/// </summary>
public sealed record Challenge(
	int Index, int Type, int FollowupType, int TargetTime, int TargetVal,
	int TargetObj, int TargetObj2, int TargetStaffType, float Prize,
	bool CheckAtEndOnly, bool Independent )
{
	/// <summary>Parse all <c>Challenges[n].*</c> entries into challenges, ordered by index.</summary>
	public static IReadOnlyList<Challenge> ParseAll( IEnumerable<SettingsPair> entries )
	{
		var byIndex = new SortedDictionary<int, Dictionary<string, float>>();
		foreach ( var pair in entries )
		{
			if ( !TryParseKey( pair.Key, out int idx, out string field ) )
				continue;
			if ( !float.TryParse( pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v ) )
				continue;
			if ( !byIndex.TryGetValue( idx, out var d ) )
				byIndex[idx] = d = new Dictionary<string, float>( StringComparer.OrdinalIgnoreCase );
			d[field] = v;
		}

		var list = new List<Challenge>();
		foreach ( var (idx, d) in byIndex )
		{
			float G( string k ) => d.TryGetValue( k, out var v ) ? v : 0f;
			list.Add( new Challenge(
				Index: idx, Type: (int)G( "Type" ), FollowupType: (int)G( "FollowupType" ),
				TargetTime: (int)G( "TargetTime" ), TargetVal: (int)G( "TargetVal" ),
				TargetObj: (int)G( "TargetObj" ), TargetObj2: (int)G( "TargetObj2" ),
				TargetStaffType: (int)G( "TargetStaffType" ), Prize: G( "Prize" ),
				CheckAtEndOnly: G( "CheckAtEndOnly" ) != 0f, Independent: G( "Independent" ) != 0f ) );
		}
		return list;
	}

	// "Challenges[12].TargetVal" → (12, "TargetVal"). False for any other key shape.
	private static bool TryParseKey( string key, out int index, out string field )
	{
		index = 0; field = "";
		const string prefix = "Challenges[";
		if ( !key.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
			return false;
		int close = key.IndexOf( ']', prefix.Length );
		if ( close < 0 || !int.TryParse( key.AsSpan( prefix.Length, close - prefix.Length ), out index ) )
			return false;
		int dot = key.IndexOf( '.', close );
		if ( dot < 0 || dot >= key.Length - 1 )
			return false;
		field = key[(dot + 1)..];
		return true;
	}
}
