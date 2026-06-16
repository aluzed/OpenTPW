using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Veldrid;

namespace OpenTPW;

[Flags]
public enum MaterialFlags
{
	None,

	DisableDepthTest,
	DisableDepthWrite,

	DisableDepth = DisableDepthTest | DisableDepthWrite
}

public partial class Material : Asset
{
	public Shader Shader { get; set; }

	public Type UniformBufferType { get; } = typeof( ObjectUniformBuffer );
	public Pipeline Pipeline { get; private set; } = null!;

	private Dictionary<string, BindableResource> _boundResources = new();

	private ResourceLayout[] _resourceLayouts = null!;

	// Resource sets are cached and reused across frames instead of being recreated every draw (the
	// old per-draw "ephemeral" sets created+destroyed ~84 GPU descriptor sets per frame — T-026).
	// Keyed by the identity of the currently bound resources, so a material that cycles bindings
	// within a frame (e.g. the shared UI material rebinding "Color" per widget — T-027) reuses one
	// set per distinct binding combination across frames instead of rebuilding. Materials with
	// stable bindings (every model mesh) simply hit a single cached entry. Cleared on shader
	// recompile (SetupResources), where the layout/buffer the sets reference is replaced.
	private readonly Dictionary<int, ResourceSet[]> _resourceSetCache = new();

	public Material( string shaderPath, MaterialFlags flags = MaterialFlags.None )
	{
		Shader = Shader.Load( shaderPath );
		Shader.OnRecompile += () => SetupResources( flags );

		All.Add( this );
		SetupResources( flags );
	}

	protected Material( string shaderPath, Type uniformBufferType, MaterialFlags flags = MaterialFlags.None )
	{
		Shader = Shader.Load( shaderPath );
		Shader.OnRecompile += () => SetupResources( flags );
		UniformBufferType = uniformBufferType;

		All.Add( this );
		SetupResources( flags );
	}

	// Persistent per-material uniform buffer. Updated in-frame on the main command list by Set<T>
	// and referenced by the cached resource set — no per-frame buffer allocation or GPU submit.
	private DeviceBuffer UniformBuffer = null!;

	private static readonly Sampler[] Samplers =
	[
		CreateSampler( SamplerType.Anisotropic ),
		CreateSampler( SamplerType.Linear ),
		CreateSampler( SamplerType.Point ),
		CreateSampler( SamplerType.AnisotropicWrap ),
		CreateSampler( SamplerType.AnisotropicRepeat ),
	];

	private static Sampler CreateSampler( SamplerType type )
	{
		var samplerFilter = type switch
		{
			SamplerType.Anisotropic or SamplerType.AnisotropicWrap or SamplerType.AnisotropicRepeat => SamplerFilter.Anisotropic,
			SamplerType.Linear => SamplerFilter.MinLinear_MagLinear_MipLinear,
			SamplerType.Point => SamplerFilter.MinPoint_MagPoint_MipPoint,
			_ => throw new NotImplementedException()
		};

		var samplerAddressMode = type switch
		{
			SamplerType.Anisotropic or SamplerType.Linear or SamplerType.Point => SamplerAddressMode.Clamp,
			SamplerType.AnisotropicWrap => SamplerAddressMode.Wrap,
			SamplerType.AnisotropicRepeat => SamplerAddressMode.Mirror,
			_ => throw new NotImplementedException()
		};

		var samplerDescription = new SamplerDescription(
			samplerAddressMode,
			samplerAddressMode,
			samplerAddressMode,
			samplerFilter,
			ComparisonKind.Always,
			(type == SamplerType.Anisotropic || type == SamplerType.AnisotropicWrap || type == SamplerType.AnisotropicRepeat) ? 16u : 0u,
			0,
			10,
			0,
			SamplerBorderColor.TransparentBlack
		);

		return Device.ResourceFactory.CreateSampler( samplerDescription );
	}

	// Binds a resource by name. The resource-set cache is keyed by the bound-resource identities,
	// so changing a binding here naturally selects (or builds) a different cached set — no explicit
	// invalidation needed.
	private void SetBound( string name, BindableResource resource )
	{
		_boundResources[name] = resource;
	}

	public void Set<T>( string name, T obj ) where T : unmanaged
	{
		// Record the uniform update on the frame's command list. (The old path called
		// ImmediateSubmit here, which created a CommandList and did a full GPU queue submit for
		// every object every frame — the lobby freeze; see T-026.) The buffer is persistent and the
		// binding is stable, so this does not invalidate the cached resource set.
		//
		// Invariant: each material is drawn at most once per frame, so the UpdateBuffer and the
		// Draw that consumes it are recorded in order on the same command list and the GPU reads the
		// right data. If a material is ever drawn multiple times per frame with different uniforms,
		// switch it to a per-draw ring / dynamic-offset buffer.
		Render.CommandList.UpdateBuffer( UniformBuffer, 0, ref obj );
		SetBound( name, UniformBuffer );
	}

	public void Set( string name, Texture[] texture )
	{
		for ( int i = 0; i < texture.Length; i++ )
		{
			SetBound( name + $"{i}", texture[i].NativeTexture );
		}

		SetBound( "s_" + name, Samplers[(int)SamplerType.AnisotropicWrap] );
	}

	public void Set( string name, Texture texture )
	{
		SetBound( name, texture.NativeTexture );
		SetBound( "s_" + name, Samplers[(int)texture.SamplerType] );
	}

	internal ResourceLayout[] CreateResourceLayouts()
	{
		return Shader.ResourceLayouts.Select( x => Device.ResourceFactory.CreateResourceLayout( x ) ).ToArray();
	}

	internal ResourceSet[] CreateResourceSets()
	{
		Debug.Assert( _resourceLayouts != null );

		List<ResourceSetDescription> resourceSetDescriptions = new();

		for ( int i = 0; i < Shader.ResourceLayouts.Length; i++ )
		{
			ResourceLayoutDescription resourceLayout = Shader.ResourceLayouts[i];
			var sortedBoundResources = new List<BindableResource>();

			foreach ( var resource in resourceLayout.Elements )
			{
				if ( _boundResources.TryGetValue( resource.Name, out var boundResource ) )
				{
					sortedBoundResources.Add( boundResource );
				}
				else
				{
					throw new Exception( $"{resource.Name} wasn't bound at draw time!" );
				}
			}

			var resourceSetDescription = new ResourceSetDescription()
			{
				Layout = _resourceLayouts[i],
				BoundResources = [.. sortedBoundResources]
			};

			resourceSetDescriptions.Add( resourceSetDescription );
		}

		return resourceSetDescriptions.Select( x => Device.ResourceFactory.CreateResourceSet( x ) ).ToArray();
	}

	// Returns the material's resource sets for the currently bound resources, building them once per
	// distinct binding combination and reusing across frames — no per-draw GPU descriptor-set
	// allocation. The key hashes the bound-resource identities (no per-draw allocation).
	internal ResourceSet[] GetResourceSets()
	{
		var key = ComputeBindingKey();
		if ( _resourceSetCache.TryGetValue( key, out var sets ) )
			return sets;

		sets = CreateResourceSets();
		_resourceSetCache[key] = sets;
		return sets;
	}

	// Identity hash of the resources currently bound to this shader's layout elements. Reference
	// identity (not value) is what a ResourceSet captures, so this is the correct cache key.
	private int ComputeBindingKey()
	{
		var hash = new HashCode();
		foreach ( var layout in Shader.ResourceLayouts )
			foreach ( var element in layout.Elements )
				hash.Add( _boundResources.TryGetValue( element.Name, out var r ) ? RuntimeHelpers.GetHashCode( r ) : 0 );

		return hash.ToHashCode();
	}

	private static void DestroyResourceSets( ResourceSet[] resourceSets )
	{
		if ( resourceSets == null )
		{
			Log.Warning( $"Resource sets were marked for death, but are already dead - we can't kill what's already dead!" );
			return;
		}

		foreach ( var item in resourceSets )
		{
			item.Dispose();
		}
	}

	private void SetupResources( MaterialFlags flags )
	{
		var vertexLayout = new VertexLayoutDescription( Vertex.VertexElementDescriptions );

		//
		// Create resource layout - but only from what we're using/need
		//
		_resourceLayouts ??= CreateResourceLayouts();

		//
		// Create pipeline
		//
		var pipelineDescription = new GraphicsPipelineDescription()
		{
			BlendState = BlendStateDescription.SingleAlphaBlend,

			DepthStencilState = new DepthStencilStateDescription(
				!flags.HasFlag( MaterialFlags.DisableDepthTest ),
				!flags.HasFlag( MaterialFlags.DisableDepthWrite ),
				flags.HasFlag( MaterialFlags.DisableDepthTest | MaterialFlags.DisableDepthWrite ) ? ComparisonKind.Always : ComparisonKind.Less
			),

			RasterizerState = new RasterizerStateDescription(
				FaceCullMode.Back,
				PolygonFillMode.Solid,
				FrontFace.Clockwise,
				true,
				false
			),

			PrimitiveTopology = PrimitiveTopology.TriangleList,
			ResourceLayouts = [.. _resourceLayouts],
			ShaderSet = new ShaderSetDescription( [vertexLayout], Shader.ShaderProgram ),
			Outputs = Render.MultisampledFramebuffer.OutputDescription
		};

		Pipeline = Device.ResourceFactory.CreateGraphicsPipeline( pipelineDescription );

		// Persistent uniform buffer, sized to the material's uniform struct rounded up to 16 (the
		// std140/uniform-buffer alignment). UpdateBuffer writes sizeof(T) <= this size at offset 0.
		var uniformSize = ((uint)Marshal.SizeOf( UniformBufferType ) + 15u) & ~15u;
		var oldBuffer = UniformBuffer;
		UniformBuffer = Device.ResourceFactory.CreateBuffer(
			new BufferDescription( uniformSize, BufferUsage.UniformBuffer | BufferUsage.Dynamic ) );

		// On a shader recompile the uniform buffer is replaced and the cached sets reference the old
		// layout/buffer — drop and dispose them (deferred so an in-flight frame isn't using one).
		// Texture bindings in _boundResources are preserved (set once at init); the next Set<T>
		// re-binds the new uniform buffer.
		if ( oldBuffer != null )
		{
			Render.ScheduleDelete( oldBuffer.Dispose );

			if ( _resourceSetCache.Count > 0 )
			{
				var old = _resourceSetCache.Values.ToArray();
				Render.ScheduleDelete( () => { foreach ( var s in old ) DestroyResourceSets( s ); } );
				_resourceSetCache.Clear();
			}
		}
	}
}

public class Material<T>( string shaderPath, MaterialFlags flags = MaterialFlags.None ) : Material( shaderPath, typeof( T ), flags );
