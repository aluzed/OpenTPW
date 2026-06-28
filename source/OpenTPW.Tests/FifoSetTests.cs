using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class FifoSetTests
{
	// The FIFO-no-duplicates queue behind the park-wide research queue (T-044).

	[TestMethod]
	public void InsertionOrderedWithoutDuplicates()
	{
		var q = new FifoSet<string>();
		Assert.IsTrue( q.Add( "a" ) );
		Assert.IsTrue( q.Add( "b" ) );
		Assert.IsFalse( q.Add( "a" ), "a duplicate is ignored" );

		Assert.AreEqual( 2, q.Count );
		Assert.AreEqual( "a", q.Active, "head is the first inserted" );
		Assert.AreEqual( 0, q.IndexOf( "a" ) );
		Assert.AreEqual( 1, q.IndexOf( "b" ) );
		Assert.AreEqual( -1, q.IndexOf( "z" ), "absent → -1" );
	}

	[TestMethod]
	public void RemovingTheHeadPromotesTheNext()
	{
		var q = new FifoSet<string>();
		q.Add( "a" ); q.Add( "b" ); q.Add( "c" );

		Assert.IsTrue( q.Remove( "a" ) );
		Assert.AreEqual( "b", q.Active, "the next queued item becomes active" );
		Assert.AreEqual( 0, q.IndexOf( "b" ) );
		Assert.AreEqual( 1, q.IndexOf( "c" ) );

		Assert.IsFalse( q.Remove( "a" ), "removing an absent item is a no-op" );
	}

	[TestMethod]
	public void EmptyQueueHasNoActive()
	{
		var q = new FifoSet<string>();
		Assert.IsNull( q.Active );
		Assert.AreEqual( 0, q.Count );

		q.Add( "only" );
		Assert.IsTrue( q.Remove( "only" ) );
		Assert.IsNull( q.Active, "back to empty after the last item leaves" );
	}

	[TestMethod]
	public void RemovingAMiddleItemKeepsOrder()
	{
		var q = new FifoSet<string>();
		q.Add( "a" ); q.Add( "b" ); q.Add( "c" );
		Assert.IsTrue( q.Remove( "b" ) );

		Assert.AreEqual( "a", q.Active );
		Assert.AreEqual( 0, q.IndexOf( "a" ) );
		Assert.AreEqual( 1, q.IndexOf( "c" ), "c shifts up after b leaves" );
		Assert.AreEqual( -1, q.IndexOf( "b" ) );
	}
}
