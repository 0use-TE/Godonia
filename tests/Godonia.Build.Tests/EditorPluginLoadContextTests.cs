using System;
using System.IO;
using System.Linq;
using Ouse.Godonia;
using Xunit;

namespace Godonia.Build.Tests;

public sealed class EditorPluginLoadContextTests {

	[Fact]
	public void Build_copies_mvvm_next_to_stamped_plugin_and_alc_can_load_it() {
		var root = Path.Combine(Path.GetTempPath(), "godonia-plugin-alc-" + Guid.NewGuid().ToString("N"));
		var pluginDir = Path.Combine(root, "Game.Editor.Probe");
		var godotDir = Path.Combine(root, "Game");
		var cacheDir = Path.Combine(godotDir, ".godot", "godonia-plugins");
		Directory.CreateDirectory(pluginDir);
		Directory.CreateDirectory(godotDir);

		try {
			var targetsSrc = Path.Combine(Repo.Root, "samples", "Godonia.EditorPlugin.targets");
			var propsSrc = Path.Combine(Repo.Root, "samples", "Godonia.EditorPlugin.props");
			File.Copy(propsSrc, Path.Combine(godotDir, "Godonia.EditorPlugin.props"));
			File.Copy(targetsSrc, Path.Combine(godotDir, "Godonia.EditorPlugin.targets"));

			File.WriteAllText(Path.Combine(pluginDir, "Game.Editor.Probe.csproj"), """
				<Project Sdk="Microsoft.NET.Sdk">
					<PropertyGroup>
						<TargetFramework>net10.0</TargetFramework>
						<Nullable>enable</Nullable>
						<ImplicitUsings>disable</ImplicitUsings>
						<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
						<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
						<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
						<GodoniaInjectHostScripts>false</GodoniaInjectHostScripts>
						<GodoniaPluginOutputDir>$(MSBuildThisFileDirectory)../Game/.godot/godonia-plugins/</GodoniaPluginOutputDir>
					</PropertyGroup>
					<ItemGroup>
						<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
					</ItemGroup>
					<Import Project="../Game/Godonia.EditorPlugin.targets" />
				</Project>
				""");
			File.WriteAllText(Path.Combine(pluginDir, "ProbePage.cs"), """
				using CommunityToolkit.Mvvm.ComponentModel;

				namespace Game.Editor.Probe;

				public sealed partial class ProbeViewModel : ObservableObject {
					[ObservableProperty] private string _title = "probe-ok";
				}

				public static class ProbePage {
					public static string CreateTitle() => new ProbeViewModel().Title;
				}
				""");

			var build = ProcessRunner.Run("dotnet", "build \"Game.Editor.Probe.csproj\" --nologo -v:n", pluginDir);
			Assert.True(build.ExitCode == 0, build.Output);

			Assert.True(Directory.Exists(cacheDir), "plugin cache was not created");
			Assert.True(File.Exists(Path.Combine(cacheDir, "CommunityToolkit.Mvvm.dll")),
				"CommunityToolkit.Mvvm.dll was not copied next to the plugin DLL.\n" + build.Output);

			var stamped = Directory.GetFiles(cacheDir, "Game.Editor.Probe.*.dll")
				.OrderByDescending(File.GetLastWriteTimeUtc)
				.FirstOrDefault();
			Assert.NotNull(stamped);

			var alc = new EditorPluginLoadContext(stamped);
			try {
				var assembly = alc.LoadPluginFromPath(stamped);
				var type = assembly.GetType("Game.Editor.Probe.ProbePage", throwOnError: true);
				var title = (string?)type!.GetMethod("CreateTitle")!.Invoke(null, null);
				Assert.Equal("probe-ok", title);
			}
			finally {
				alc.Unload();
			}
		}
		finally {
			try {
				Directory.Delete(root, recursive: true);
			}
			catch (IOException) {
			}
		}
	}

}
