namespace OpenTPW;

/// <summary>
/// A FIFO queue that ignores duplicate entries — insertion-ordered with no repeats, O(1) head access. The
/// park-wide research queue (T-044) uses it to process rides one at a time. Pure (unit-tested).
/// </summary>
internal sealed class FifoSet<T> where T : class
{
	private readonly List<T> items = new();

	/// <summary>The queued items, head (active) first.</summary>
	public IReadOnlyList<T> Items => items;

	/// <summary>The head of the queue (the active item), or null when empty.</summary>
	public T? Active => items.Count > 0 ? items[0] : null;

	public int Count => items.Count;

	/// <summary>Append <paramref name="x"/> unless it's already queued; true if it was added.</summary>
	public bool Add( T x ) { if ( items.Contains( x ) ) return false; items.Add( x ); return true; }

	/// <summary>Remove <paramref name="x"/>; true if it was present.</summary>
	public bool Remove( T x ) => items.Remove( x );

	/// <summary>0-based position of <paramref name="x"/> (0 = active/head), or -1 if not queued.</summary>
	public int IndexOf( T x ) => items.IndexOf( x );

	public void Clear() => items.Clear();
}
