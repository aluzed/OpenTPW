using System.Runtime.InteropServices;
using Veldrid;

namespace OpenTPW;

public class Model : Asset
{
	public DeviceBuffer VertexBuffer { get; private set; } = null!;
	public DeviceBuffer? IndexBuffer { get; private set; } = null;

	/// <summary>
	/// The model's vertices, retained so dynamic geometry (ride vertex-morph animation) can mutate
	/// positions and re-upload via <see cref="UploadVertices"/>. Static models never touch this.
	/// </summary>
	public Vertex[] Vertices { get; private set; } = System.Array.Empty<Vertex>();

	public Material Material { get; private set; }
	public bool IsIndexed { get; private set; }

	private uint indexCount;
	private uint vertexCount;

	public Model( Vertex[] vertices, uint[] indices, Material material )
	{
		Material = material;
		IsIndexed = true;

		SetupMesh( vertices, indices );

		All.Add( this );
	}

	public Model( Vertex[] vertices, Material material )
	{
		Material = material;
		IsIndexed = false;

		SetupMesh( vertices );

		All.Add( this );
	}

	private void SetupMesh( Vertex[] vertices )
	{
		var factory = Device.ResourceFactory;
		var vertexStructSize = (uint)Marshal.SizeOf( typeof( Vertex ) );
		vertexCount = (uint)vertices.Length;
		Vertices = vertices;

		VertexBuffer = factory.CreateBuffer(
			new BufferDescription( vertexCount * vertexStructSize, BufferUsage.VertexBuffer )
		);

		Device.UpdateBuffer( VertexBuffer, 0, vertices );
	}

	/// <summary>Re-upload <see cref="Vertices"/> to the GPU after mutating positions (dynamic morph).</summary>
	public void UploadVertices() => Device.UpdateBuffer( VertexBuffer, 0, Vertices );

	private void SetupMesh( Vertex[] vertices, uint[] indices )
	{
		SetupMesh( vertices );

		var factory = Device.ResourceFactory;
		indexCount = (uint)indices.Length;

		IndexBuffer = factory.CreateBuffer(
			new BufferDescription( indexCount * sizeof( uint ), BufferUsage.IndexBuffer )
		);

		Device.UpdateBuffer( IndexBuffer, 0, indices );
	}

	internal void Draw()
	{
		var commandList = Render.CommandList;

		commandList.SetVertexBuffer( 0, VertexBuffer );
		commandList.SetPipeline( Material.Pipeline );

		var resourceSets = Material.GetResourceSets();

		for ( uint i = 0; i < resourceSets.Length; ++i )
			commandList.SetGraphicsResourceSet( i, resourceSets[i] );

		if ( IsIndexed )
		{
			commandList.SetIndexBuffer( IndexBuffer, IndexFormat.UInt32 );

			commandList.DrawIndexed(
				indexCount: indexCount,
				instanceCount: 1,
				indexStart: 0,
				vertexOffset: 0,
				instanceStart: 0
			);
		}
		else
		{
			commandList.Draw( vertexCount );
		}
	}
}
