using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace Godonia.Build.Tests;

[Collection("LocalNupkg")]
public sealed class HostScriptInjectTests {

	private readonly LocalNupkgFixture _nupkg;

	public HostScriptInjectTests(LocalNupkgFixture nupkg)
		=> _nupkg = nupkg;

	[Fact]
	public void EmptyGodotProject_InsertsHostScripts_AndCompilesProbe() {
		using var dir = TempProject.Create();
		dir.WriteNugetConfig(_nupkg.NupkgsDir);
		dir.WriteGodotCsproj(inject: null, extraDir: null);
		dir.WriteFile("ProbeHost.cs", ProbeHostSource);

		var result = dir.Build();
		Assert.True(result.Success, result.Output);
		Assert.Contains("GODONIA001", result.Output);
		Assert.True(File.Exists(Path.Combine(dir.Path, "UI", "AvaloniaControl.cs")));
		Assert.True(File.Exists(Path.Combine(dir.Path, "UI", "UiHost.cs")));
		Assert.False(File.Exists(Path.Combine(dir.Path, "AvaloniaControl.cs")));
		Assert.False(File.Exists(Path.Combine(dir.Path, "UiHost.cs")));
	}

	[Fact]
	public void ExistingFiles_AreNotOverwritten() {
		using var dir = TempProject.Create();
		dir.WriteNugetConfig(_nupkg.NupkgsDir);
		dir.WriteGodotCsproj(inject: null, extraDir: null);
		const string marker = "// KEEP-ME-UNIQUE-MARKER";
		dir.WriteFile("AvaloniaControl.cs", marker + Environment.NewLine + MinimalAvaloniaControl);
		dir.WriteFile("UiHost.cs", marker + Environment.NewLine + MinimalUiHost);

		var result = dir.Build();
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("GODONIA001", result.Output);
		Assert.StartsWith(marker, File.ReadAllText(Path.Combine(dir.Path, "AvaloniaControl.cs")), StringComparison.Ordinal);
		Assert.StartsWith(marker, File.ReadAllText(Path.Combine(dir.Path, "UiHost.cs")), StringComparison.Ordinal);
	}

	[Fact]
	public void ExistingFilesInSubdirectory_DoNotCopyToRoot() {
		using var dir = TempProject.Create();
		dir.WriteNugetConfig(_nupkg.NupkgsDir);
		dir.WriteGodotCsproj(inject: null, extraDir: null);
		Directory.CreateDirectory(Path.Combine(dir.Path, "src"));
		File.Copy(Path.Combine(Repo.HostScriptsDir, "AvaloniaControl.cs"), Path.Combine(dir.Path, "src", "AvaloniaControl.cs"));
		File.Copy(Path.Combine(Repo.HostScriptsDir, "UiHost.cs"), Path.Combine(dir.Path, "src", "UiHost.cs"));

		var result = dir.Build();
		Assert.True(result.Success, result.Output);
		Assert.False(File.Exists(Path.Combine(dir.Path, "AvaloniaControl.cs")));
		Assert.False(File.Exists(Path.Combine(dir.Path, "UiHost.cs")));
		Assert.True(File.Exists(Path.Combine(dir.Path, "src", "AvaloniaControl.cs")));
	}

	[Fact]
	public void ClassLibrary_DoesNotInsertHostScripts() {
		using var dir = TempProject.Create();
		dir.WriteNugetConfig(_nupkg.NupkgsDir);
		dir.WriteFile("Lib.csproj", """
			<Project Sdk="Microsoft.NET.Sdk">
				<PropertyGroup>
					<TargetFramework>net10.0</TargetFramework>
					<ImplicitUsings>disable</ImplicitUsings>
					<Nullable>enable</Nullable>
					<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
				</PropertyGroup>
				<ItemGroup>
					<PackageReference Include="Ouse.Godonia" Version="1.0.0" />
				</ItemGroup>
			</Project>
			""");
		dir.WriteFile("Class1.cs", "namespace Lib; public class Class1 {}");

		var result = dir.Build("Lib.csproj");
		Assert.True(result.Success, result.Output);
		Assert.False(File.Exists(Path.Combine(dir.Path, "AvaloniaControl.cs")));
		Assert.False(File.Exists(Path.Combine(dir.Path, "UiHost.cs")));
		Assert.False(File.Exists(Path.Combine(dir.Path, "UI", "AvaloniaControl.cs")));
		Assert.False(File.Exists(Path.Combine(dir.Path, "UI", "UiHost.cs")));
	}

	[Fact]
	public void InjectDisabled_EmitsGodonia002_AndDoesNotCopy() {
		using var dir = TempProject.Create();
		dir.WriteNugetConfig(_nupkg.NupkgsDir);
		dir.WriteGodotCsproj(inject: false, extraDir: null);

		var result = dir.Build();
		Assert.True(result.Success, result.Output);
		Assert.Contains("GODONIA002", result.Output);
		Assert.False(File.Exists(Path.Combine(dir.Path, "AvaloniaControl.cs")));
		Assert.False(File.Exists(Path.Combine(dir.Path, "UiHost.cs")));
		Assert.False(File.Exists(Path.Combine(dir.Path, "UI", "AvaloniaControl.cs")));
		Assert.False(File.Exists(Path.Combine(dir.Path, "UI", "UiHost.cs")));
	}

	[Fact]
	public void CustomHostScriptsDir_CopiesThere_AndCompiles() {
		using var dir = TempProject.Create();
		dir.WriteNugetConfig(_nupkg.NupkgsDir);
		dir.WriteGodotCsproj(inject: true, extraDir: "Hosts");
		dir.WriteFile("ProbeHost.cs", ProbeHostSource);

		var result = dir.Build();
		Assert.True(result.Success, result.Output);
		Assert.True(File.Exists(Path.Combine(dir.Path, "Hosts", "AvaloniaControl.cs")));
		Assert.True(File.Exists(Path.Combine(dir.Path, "Hosts", "UiHost.cs")));
		Assert.False(File.Exists(Path.Combine(dir.Path, "AvaloniaControl.cs")));
	}

	private const string ProbeHostSource = """
		using Godot;
		using Ouse.Godonia;
		using AvControl = Avalonia.Controls.Control;

		public partial class ProbeHost : UiHost {
			protected override AvControl CreateRoot() => new AvControl();
		}
		""";

	private const string MinimalAvaloniaControl = """
		using Godot;
		namespace Ouse.Godonia;
		public partial class AvaloniaControl : Control { }
		""";

	private const string MinimalUiHost = """
		using Godot;
		using AvControl = Avalonia.Controls.Control;
		namespace Ouse.Godonia;
		public abstract partial class UiHost : AvaloniaControl {
			protected abstract AvControl CreateRoot();
		}
		""";

}

public sealed class LocalNupkgFixture : IDisposable {

	public string NupkgsDir { get; }

	public LocalNupkgFixture() {
		NupkgsDir = Path.Combine(Path.GetTempPath(), "godonia-nupkgs-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(NupkgsDir);

		var pack = ProcessRunner.Run(
			"dotnet",
			$"pack \"{Path.Combine(Repo.Root, "src", "Ouse.Godonia", "Ouse.Godonia.csproj")}\" -c Release -o \"{NupkgsDir}\" --nologo",
			Repo.Root);
		if (pack.ExitCode != 0)
			throw new InvalidOperationException("dotnet pack failed:\n" + pack.Output);
	}

	public void Dispose() {
		try {
			Directory.Delete(NupkgsDir, recursive: true);
		}
		catch (IOException) {
			// best-effort cleanup
		}
	}

}

[CollectionDefinition("LocalNupkg")]
public sealed class LocalNupkgCollection : ICollectionFixture<LocalNupkgFixture> {
}

internal sealed class TempProject : IDisposable {

	public string Path { get; }

	private TempProject(string path)
		=> Path = path;

	public static TempProject Create() {
		var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "godonia-inject-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return new TempProject(path);
	}

	public void WriteFile(string relative, string contents)
		=> File.WriteAllText(System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar)), contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

	public void WriteNugetConfig(string nupkgsDir) {
		var escaped = nupkgsDir.Replace('\\', '/');
		WriteFile("nuget.config", $"""
			<?xml version="1.0" encoding="utf-8"?>
			<configuration>
				<packageSources>
					<clear />
					<add key="godonia-local" value="{escaped}" />
					<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
				</packageSources>
			</configuration>
			""");
	}

	public void WriteGodotCsproj(bool? inject, string? extraDir) {
		var injectProp = inject switch {
			true => "<GodoniaInjectHostScripts>true</GodoniaInjectHostScripts>",
			false => "<GodoniaInjectHostScripts>false</GodoniaInjectHostScripts>",
			null => ""
		};
		var dirProp = extraDir is null
			? ""
			: $"<GodoniaHostScriptsDir>$(MSBuildProjectDirectory)\\{extraDir}</GodoniaHostScriptsDir>";

		WriteFile("Game.csproj", $"""
			<Project Sdk="Godot.NET.Sdk/4.7.1">
				<PropertyGroup>
					<TargetFramework>net10.0</TargetFramework>
					<EnableDynamicLoading>true</EnableDynamicLoading>
					<ImplicitUsings>disable</ImplicitUsings>
					<Nullable>enable</Nullable>
					<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
					{injectProp}
					{dirProp}
				</PropertyGroup>
				<ItemGroup>
					<PackageReference Include="Ouse.Godonia" Version="1.0.0" />
				</ItemGroup>
			</Project>
			""");
	}

	public BuildResult Build(string? projectFile = null) {
		var file = projectFile ?? "Game.csproj";
		var run = ProcessRunner.Run("dotnet", $"build \"{file}\" --nologo -v:n", Path);
		return new BuildResult(run.ExitCode == 0, run.Output);
	}

	public void Dispose() {
		try {
			Directory.Delete(Path, recursive: true);
		}
		catch (IOException) {
			// best-effort cleanup
		}
	}

}

internal readonly record struct BuildResult(bool Success, string Output);

internal static class ProcessRunner {

	public static (int ExitCode, string Output) Run(string fileName, string arguments, string workingDirectory) {
		var start = new ProcessStartInfo {
			FileName = fileName,
			Arguments = arguments,
			WorkingDirectory = workingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		using var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start " + fileName);
		var output = new StringBuilder();
		process.OutputDataReceived += (_, e) => {
			if (e.Data is not null)
				output.AppendLine(e.Data);
		};
		process.ErrorDataReceived += (_, e) => {
			if (e.Data is not null)
				output.AppendLine(e.Data);
		};
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		if (!process.WaitForExit(180_000)) {
			try {
				process.Kill(entireProcessTree: true);
			}
			catch {
				// ignore
			}

			throw new TimeoutException(fileName + " " + arguments + " timed out.");
		}

		process.WaitForExit();
		return (process.ExitCode, output.ToString());
	}

}
