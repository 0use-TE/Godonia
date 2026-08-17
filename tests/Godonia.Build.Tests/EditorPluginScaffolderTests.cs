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
	public void Create_adds_project_reference_to_sibling_preview() {
		var root = Path.Combine(Path.GetTempPath(), "godonia-scaffold-preview-" + Path.GetRandomFileName());
		var godot = Path.Combine(root, "HelloWorld");
		var previewDir = Path.Combine(root, "HelloWorld.Editor.Preview");
		Directory.CreateDirectory(godot);
		Directory.CreateDirectory(previewDir);
		File.WriteAllText(Path.Combine(godot, "project.godot"), "config_version=5\n");
		File.WriteAllText(Path.Combine(godot, "Godonia.EditorPlugin.props"), "<Project />\n");
		File.WriteAllText(Path.Combine(godot, "Godonia.EditorPlugin.targets"), "<Project />\n");
		var previewCsproj = Path.Combine(previewDir, "HelloWorld.Editor.Preview.csproj");
		File.WriteAllText(previewCsproj, """
			<Project Sdk="Microsoft.NET.Sdk">
				<PropertyGroup>
					<OutputType>WinExe</OutputType>
					<TargetFramework>net10.0</TargetFramework>
				</PropertyGroup>
				<ItemGroup>
					<PackageReference Include="Avalonia.Desktop" />
					<PackageReference Include="Ouse.Godonia" />
				</ItemGroup>
			</Project>
			""");

		try {
			var result = EditorPluginScaffolder.Create(new EditorPluginScaffoldRequest {
				GodotProjectRoot = godot + Path.DirectorySeparatorChar,
				Title = "Skill Tree",
				Name = "SkillTree",
				PluginId = "helloworld.skill.tree",
				UseMvvm = false,
				AddToSolution = false,
				AddToPreview = true,
				EnableInProject = false
			});

			Assert.True(result.Success, result.Message);
			var preview = File.ReadAllText(previewCsproj);
			Assert.Contains("HelloWorld.Editor.SkillTree.csproj", preview, StringComparison.Ordinal);
			Assert.Contains("../HelloWorld.Editor.SkillTree/HelloWorld.Editor.SkillTree.csproj", preview.Replace('\\', '/'), StringComparison.Ordinal);
			Assert.False(Directory.Exists(Path.Combine(godot, "HelloWorld.Editor.Preview")));
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

	[Fact]
	public void Uninstall_removes_manifest_project_preview_ref_slnx_and_cached_dll() {
		var root = Path.Combine(Path.GetTempPath(), "godonia-uninstall-" + Path.GetRandomFileName());
		var godot = Path.Combine(root, "HelloWorld");
		var previewDir = Path.Combine(root, "HelloWorld.Editor.Preview");
		Directory.CreateDirectory(godot);
		Directory.CreateDirectory(previewDir);
		File.WriteAllText(Path.Combine(godot, "project.godot"), "config_version=5\n");
		File.WriteAllText(Path.Combine(godot, "Godonia.EditorPlugin.props"), "<Project />\n");
		File.WriteAllText(Path.Combine(godot, "Godonia.EditorPlugin.targets"), "<Project />\n");
		var previewCsproj = Path.Combine(previewDir, "HelloWorld.Editor.Preview.csproj");
		File.WriteAllText(previewCsproj, """
			<Project Sdk="Microsoft.NET.Sdk">
				<PropertyGroup>
					<OutputType>WinExe</OutputType>
					<TargetFramework>net10.0</TargetFramework>
				</PropertyGroup>
				<ItemGroup>
					<PackageReference Include="Avalonia.Desktop" />
					<PackageReference Include="Ouse.Godonia" />
				</ItemGroup>
			</Project>
			""");
		File.WriteAllText(Path.Combine(root, "App.slnx"), """
			<Solution>
			  <Folder Name="/Samples/">
			  </Folder>
			</Solution>
			""");

		try {
			var created = EditorPluginScaffolder.Create(new EditorPluginScaffoldRequest {
				GodotProjectRoot = godot,
				Title = "Skill Tree",
				Name = "SkillTree",
				PluginId = "helloworld.skill.tree",
				UseMvvm = false,
				AddToSolution = true,
				AddToPreview = true,
				EnableInProject = false
			});
			Assert.True(created.Success, created.Message);

			var cacheDir = Path.Combine(godot, ".godot", "godonia-plugins");
			Directory.CreateDirectory(cacheDir);
			var dll = Path.Combine(cacheDir, "HelloWorld.Editor.SkillTree.dll");
			var shadow = Path.Combine(cacheDir, "HelloWorld.Editor.SkillTree.12.dll");
			var other = Path.Combine(cacheDir, "HelloWorld.Editor.QuestManager.dll");
			File.WriteAllText(dll, "dll");
			File.WriteAllText(shadow, "shadow");
			File.WriteAllText(other, "keep");

			var listed = EditorPluginScaffolder.ListInstalled(godot);
			Assert.Contains(listed, p => p.Id == "helloworld.skill.tree");
			Assert.Equal("Skill Tree", listed[0].Title);

			var removed = EditorPluginScaffolder.Uninstall(godot, "helloworld.skill.tree");
			Assert.True(removed.Success, removed.Message);
			Assert.False(File.Exists(Path.Combine(godot, "addons", "godonia_new_plugin", "pages", "helloworld.skill.tree.json")));
			Assert.False(Directory.Exists(created.PluginProjectDir));
			Assert.DoesNotContain("SkillTree.csproj", File.ReadAllText(previewCsproj), StringComparison.Ordinal);
			Assert.DoesNotContain("SkillTree.csproj", File.ReadAllText(Path.Combine(root, "App.slnx")), StringComparison.Ordinal);
			Assert.False(File.Exists(dll));
			Assert.False(File.Exists(shadow));
			Assert.True(File.Exists(other));
			Assert.Empty(EditorPluginScaffolder.ListInstalled(godot));
		}
		finally {
			try {
				Directory.Delete(root, recursive: true);
			}
			catch {
			}
		}
	}

}
