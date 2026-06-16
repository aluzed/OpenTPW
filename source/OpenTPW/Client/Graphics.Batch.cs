using System.Runtime.InteropServices;
using Veldrid;

namespace OpenTPW;

internal static partial class Graphics
{
	// UI draw batching (T-027). Quad/DrawText append their geometry here instead of each issuing
	// its own UpdateBuffer + DrawIndexed. Consecutive draws that resolve to the same material +
	// bound resources (e.g. a run of same-texture glyphs/quads) merge into a single DrawIndexed.
	// The batch flushes when the material/binding changes, when it would overflow the shared GPU
	// buffer, or once at end of frame (Renderer.PostRender, before the MSAA resolve).
	//
	// All Graphics.Quad/DrawText callers are UI (rendered after the immediate-mode 3D models, which
	// use their own buffers), so the batch is purely UI and never interleaves with a model draw.

	private static readonly uint VertexStructSize = (uint)Marshal.SizeOf<Vertex>();

	private static Vertex[] _batchVerts = new Vertex[1024];
	private static uint[] _batchIndices = new uint[1536];
	private static int _batchVertCount;
	private static int _batchIndexCount;
	private static Material? _batchMaterial;
	private static ResourceSet[]? _batchSets;

	// Appends one mesh (local 0-based indices) to the current batch, flushing first if the GPU
	// state would change or the batch would overflow.
	private static void AppendGeometry( Material material, ReadOnlySpan<Vertex> verts, ReadOnlySpan<uint> indices )
	{
		var sets = material.GetResourceSets();

		var stateChanged = _batchMaterial != material || _batchSets != sets;
		var overflow = _batchVertCount + verts.Length > MaxVertexCount
					|| _batchIndexCount + indices.Length > MaxIndexCount;

		if ( _batchVertCount > 0 && ( stateChanged || overflow ) )
			FlushBatch();

		_batchMaterial = material;
		_batchSets = sets;

		EnsureCapacity( _batchVertCount + verts.Length, _batchIndexCount + indices.Length );

		var baseVertex = (uint)_batchVertCount;
		verts.CopyTo( _batchVerts.AsSpan( _batchVertCount ) );
		_batchVertCount += verts.Length;

		for ( var i = 0; i < indices.Length; i++ )
			_batchIndices[_batchIndexCount++] = baseVertex + indices[i];
	}

	private static void EnsureCapacity( int verts, int indices )
	{
		if ( verts > _batchVerts.Length )
			Array.Resize( ref _batchVerts, Math.Max( verts, _batchVerts.Length * 2 ) );

		if ( indices > _batchIndices.Length )
			Array.Resize( ref _batchIndices, Math.Max( indices, _batchIndices.Length * 2 ) );
	}

	/// <summary>
	/// Uploads and draws the accumulated UI batch as a single <c>DrawIndexed</c>. Called internally
	/// on state change / overflow, and by <c>Renderer.PostRender</c> once per frame before the MSAA
	/// resolve so the last batch is emitted.
	/// </summary>
	internal static void FlushBatch()
	{
		if ( _batchVertCount == 0 || _batchMaterial == null || _batchSets == null )
		{
			_batchVertCount = 0;
			_batchIndexCount = 0;
			return;
		}

		var cmd = Render.CommandList;
		cmd.UpdateBuffer( vertexBuffer, 0, ref _batchVerts[0], (uint)_batchVertCount * VertexStructSize );
		cmd.UpdateBuffer( indexBuffer, 0, ref _batchIndices[0], (uint)_batchIndexCount * sizeof( uint ) );

		cmd.SetIndexBuffer( indexBuffer, IndexFormat.UInt32 );
		cmd.SetVertexBuffer( 0, vertexBuffer );
		cmd.SetPipeline( _batchMaterial.Pipeline );

		for ( uint i = 0; i < _batchSets.Length; ++i )
			cmd.SetGraphicsResourceSet( i, _batchSets[i] );

		cmd.DrawIndexed( (uint)_batchIndexCount );

		_batchVertCount = 0;
		_batchIndexCount = 0;
		_batchMaterial = null;
		_batchSets = null;
	}
}
