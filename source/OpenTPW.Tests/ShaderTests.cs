using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace OpenTPW.Tests;

[TestClass]
public class ShaderTests
{
	[TestMethod]
	public void PreprocessTest()
	{
		Log = new();

		// Shaders are copied next to the test assembly (see OpenTPW.Tests.csproj),
		// so this resolves on any OS without a hardcoded absolute path.
		var shaderPath = Path.Combine( AppContext.BaseDirectory, "content", "shaders", "test.shader" );

		var result = ShaderPreprocessor.PreprocessShader( shaderPath );
		Assert.IsTrue( result.VertexShader.Length > 0 && result.FragmentShader.Length > 0 );
	}
}
