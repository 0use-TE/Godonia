using System;
using System.IO;
using Ouse.Godonia;
using Xunit;

namespace Godonia.Build.Tests;

public sealed class EditorPluginScaffolderTests {

	[Fact]
	public void GameIdentifier_trims_trailing_slash() {
		var root = Path.Combine(Path.GetTempPath(), "godonia-gameid-" + Path.GetRandomFileName(), "GodotGame");
		Directory.CreateDirectory(root);
		try {
			Assert.Equal("GodotGame", EditorPluginScaffolder.GameIdentifier(root));
			Assert.Equal("GodotGame", EditorPluginScaffolder.GameIdentifier(root + Path.DirectorySeparatorChar));
		}
		finally {
			try {
				Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
			}
			catch {
			}
		}
	}

	[Fact]
	public void SanitizePascal_strips_spaces_and_punctuation()
		=> Assert.Equal("MyInspector", EditorPluginScaffolder.SanitizePascal("my inspector!"));

	[Fact]
	public void DefaultPluginId_uses_project_and_name()
		=> Assert.Equal("hello.world.inspector", EditorPluginScaffolder.DefaultPluginId("HelloWorld", "Inspector"));

	[Fact]
	public void Template_plugin_targets_copy_satellite_dlls() {
		var targets = File.ReadAllText(Path.Combine(Repo.Root, "samples", "Godonia.EditorPlugin.targets"));
		Assert.Contains("_PluginSatelliteDlls", targets, StringComparison.Ordinal);
		Assert.Contains("CommunityToolkit.Mvvm", targets, StringComparison.Ordinal);

		var props = File.ReadAllText(Path.Combine(Repo.Root, "samples", "Godonia.EditorPlugin.props"));
		Assert.Contains("<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>", props, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_writes_mainscreen_manifest_slot() {
		var root = Path.Combine(Path.GetTempPath(), "godonia-scaffold-ms-" + Path.GetRandomFileName());
		var godot = Path.Combine(root, "HelloWorld");
		Directory.CreateDirectory(godot);
		File.WriteAllText(Path.Combine(godot, "project.godot"), "config_version=5\n");
		File.WriteAllText(Path.Combine(godot, "Godonia.EditorPlugin.props"), "<Project />\n");
		File.WriteAllText(Path.Combine(godot, "Godonia.EditorPlugin.targets"), "<Project />\n");

		try {
			var result = EditorPluginScaffolder.Create(new EditorPluginScaffoldRequest {
				GodotProjectRoot = godot,
				Title = "Quest",
				Name = "Quest",
				PluginId = "helloworld.quest",
				DockSlot = "MainScreen",
				UseMvvm = false,
				AddToSolution = false,
				AddToPreview = false,
				EnableInProject = false
			});

			Assert.True(result.Success, result.Message);
			var json = File.ReadAllText(Path.Combine(godot, "addons", "godonia_new_plugin", "pages", "helloworld.quest.json"));
			Assert.Contains("\"dockSlot\": \"MainScreen\"", json, StringComparison.Ordinal);
			Assert.True(EditorPluginScaffolder.IsMainScreenSlot("MainScreen"));
			Assert.False(EditorPluginScaffolder.IsMainScreenSlot("Left"));
		}
		finally {
			try {
				Directory.Delete(root, recursive: true);
			}
			catch {
			}
		}
	}

	[Fact]
	public void Create_writes_mvvm_plugin_without_godot_addon() {
		var root = Path.Combine(Path.GetTempPath(), "godonia-scaffold-" + Path.GetRandomFileName());
		var godot = Path.Combine(root, "HelloWorld");
		Directory.CreateDirectory(godot);
		File.WriteAllText(Path.Combine(godot, "project.godot"), "config_version=5\n");
		File.WriteAllText(Path.Combine(godot, "Godonia.EditorPlugin.props"), "<Project />\n");
		File.WriteAllText(Path.Combine(godot, "Godonia.EditorPlugin.targets"), "<Project />\n");
		File.WriteAllText(Path.Combine(root, "App.slnx"), """
			<Solution>
			  <Folder Name="/Samples/">
			  </Folder>
			</Solution>
			""");

		try {
			var result = EditorPluginScaffolder.Create(new EditorPluginScaffoldRequest {
				GodotProjectRoot = godot,
				Title = "Loot Tables",
				Name = "LootTables",
				PluginId = "helloworld.loot.tables",
				UseMvvm = true,
				AddToSolution = true,
				AddToPreview = false,
				EnableInProject = true
			});

			Assert.True(result.Success, result.Message);
			Assert.True(File.Exists(Path.Combine(result.PluginProjectDir!, "LootTablesView.axaml")));
			Assert.True(File.Exists(Path.Combine(result.PluginProjectDir!, "LootTablesViewModel.cs")));
			Assert.True(File.Exists(Path.Combine(godot, "addons", "godonia_new_plugin", "pages", "helloworld.loot.tables.json")));
			Assert.False(File.Exists(Path.Combine(godot, "addons", "estragonia_loot_tables", "plugin.cfg")));
			Assert.Contains("godonia_new_plugin", File.ReadAllText(Path.Combine(godot, "project.godot")), StringComparison.Ordinal);
			Assert.Contains("HelloWorld.Editor.LootTables.csproj", File.ReadAllText(Path.Combine(root, "App.slnx")), StringComparison.Ordinal);
			Assert.Contains("ObservableObject", File.ReadAllText(Path.Combine(result.PluginProjectDir!, "LootTablesViewModel.cs")), StringComparison.Ordinal);
		}
		finally {
			try {
				Directory.Delete(root, recursive: true);
			}
			catch {
				// temp cleanup is best-effort
			}
		}
	}

}
