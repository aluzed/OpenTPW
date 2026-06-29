using System.Numerics;

namespace OpenTPW;

/// <summary>
/// Maps a world position onto the corner minimap (T-057) and back. The park's footprint (the placement grid's
/// world bounds) projects into the minimap's on-screen rectangle: world X→u, world Y→v, clamped to the map so
/// off-park entities pin to the edge. <see cref="FlipY"/> matches the top-down map image's orientation to the
/// Y-up UI space. Pure + unit-tested; the panel feeds it the live grid bounds + its draw rect.
/// </summary>
public readonly record struct MinimapProjection(
	float OriginX, float OriginY, float WorldW, float WorldH, Rectangle Map, bool FlipY = true )
{
	private static float Clamp01( float t ) => t < 0f ? 0f : t > 1f ? 1f : t;

	/// <summary>Project a world XY into a point in the minimap's base-space rectangle (clamped to it).</summary>
	public Vector2 Project( float worldX, float worldY )
	{
		float u = WorldW <= 0f ? 0.5f : Clamp01( (worldX - OriginX) / WorldW );
		float v = WorldH <= 0f ? 0.5f : Clamp01( (worldY - OriginY) / WorldH );
		if ( FlipY )
			v = 1f - v;
		return new Vector2( Map.X + u * Map.Width, Map.Y + v * Map.Height );
	}

	/// <summary>Inverse of <see cref="Project"/>: a point on the minimap → the world XY it represents (for
	/// click-to-pan). Points outside the map clamp to the park bounds.</summary>
	public (float X, float Y) Unproject( Vector2 mapPoint )
	{
		float u = Map.Width <= 0f ? 0f : Clamp01( (mapPoint.X - Map.X) / Map.Width );
		float v = Map.Height <= 0f ? 0f : Clamp01( (mapPoint.Y - Map.Y) / Map.Height );
		if ( FlipY )
			v = 1f - v;
		return (OriginX + u * WorldW, OriginY + v * WorldH);
	}

	/// <summary>True when a base-space point lies within the minimap rectangle.</summary>
	public bool Contains( Vector2 p )
		=> p.X >= Map.X && p.X <= Map.X + Map.Width && p.Y >= Map.Y && p.Y <= Map.Y + Map.Height;
}
