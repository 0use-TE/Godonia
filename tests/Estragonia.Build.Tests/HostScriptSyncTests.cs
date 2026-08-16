using System;
using System.IO;
using System.Text;
using Xunit;

namespace Estragonia.Build.Tests;

public sealed class HostScriptSyncTests {

	[Theory]
	[InlineData("AvaloniaControl.cs", "templates/estragonia-godot/AvaloniaControl.cs", "samples/HelloWorld/AvaloniaControl.cs")]
	[InlineData("UiHost.cs", "templates/estragonia-godot/UiHost.cs", "samples/HelloWorld/UiHost.cs")]
	[InlineData("AvaloniaEditorHost.cs", "templates/estragonia-godot/AvaloniaEditorHost.cs", "samples/HelloWorld/AvaloniaEditorHost.cs")]
	public void HostScriptsMatchAuthoritativeSource(string fileName, string templateRelative, string sampleRelative) {
		var expected = Normalize(File.ReadAllBytes(Path.Combine(Repo.HostScriptsDir, fileName)));
		var template = Normalize(File.ReadAllBytes(Path.Combine(Repo.Root, templateRelative.Replace('/', Path.DirectorySeparatorChar))));
		var sample = Normalize(File.ReadAllBytes(Path.Combine(Repo.Root, sampleRelative.Replace('/', Path.DirectorySeparatorChar))));

		Assert.True(expected.AsSpan().SequenceEqual(template), $"{templateRelative} drifted from host-scripts/{fileName}");
		Assert.True(expected.AsSpan().SequenceEqual(sample), $"{sampleRelative} drifted from host-scripts/{fileName}");
	}

	private static byte[] Normalize(byte[] bytes) {
		var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n');
		return Encoding.UTF8.GetBytes(text);
	}

}
