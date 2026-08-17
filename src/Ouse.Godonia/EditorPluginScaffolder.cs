using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Ouse.Godonia;

/// <summary>Inputs for generating a Godonia editor plugin (Avalonia page + thin Godot addon).</summary>
public sealed class EditorPluginScaffoldRequest {

	public required string GodotProjectRoot { get; init; }

	public required string Title { get; init; }

	public required string Name { get; init; }

	public required string PluginId { get; init; }

	/// <summary>Godot dock slot (<c>Left</c>/<c>Right</c>/<c>Bottom</c>) or <c>MainScreen</c>.</summary>
	public string DockSlot { get; init; } = "LeftUl";

	public bool UseMvvm { get; init; } = true;

	public bool AddToSolution { get; init; } = true;

	public bool AddToPreview { get; init; } = true;

	public bool EnableInProject { get; init; } = true;

}

/// <summary>Paths written by <see cref="EditorPluginScaffolder"/>.</summary>
public sealed class EditorPluginScaffoldResult {

	public bool Success { get; init; }

	public string Message { get; init; } = "";

	public string? PluginProjectDir { get; init; }

	public string? AddonDir { get; init; }

	public string? ManifestPath { get; init; }

	public IReadOnlyList<string> WrittenFiles { get; init; } = [];

}

/// <summary>Creates a collectible Avalonia editor plugin project plus a thin Godot addon.</summary>
public static class EditorPluginScaffolder {

	public static EditorPluginScaffoldResult Create(EditorPluginScaffoldRequest request) {
		ArgumentNullException.ThrowIfNull(request);

		var godotRoot = Path.GetFullPath(request.GodotProjectRoot);
		if (!Directory.Exists(godotRoot))
			return Fail($"Godot project folder not found: {godotRoot}");

		var name = SanitizePascal(request.Name);
		if (name.Length == 0)
			return Fail("Name must start with a letter (PascalCase identifier).");

		var game = GameIdentifier(godotRoot);
		var title = string.IsNullOrWhiteSpace(request.Title) ? name : request.Title.Trim();
		var pluginId = string.IsNullOrWhiteSpace(request.PluginId)
			? DefaultPluginId(game, name)
			: request.PluginId.Trim().ToLowerInvariant();

		if (!IsSafePluginId(pluginId))
			return Fail("Plugin id must look like 'game.inspector' (letters, digits, dots, underscores).");

		var projectName = game + ".Editor." + name;
		var pluginDir = Path.GetFullPath(Path.Combine(godotRoot, "..", projectName));
		var hostAddonFolder = "godonia_new_plugin";
		var pagesDir = Path.Combine(godotRoot, "addons", hostAddonFolder, "pages");
		var ns = projectName;
		var hostName = "Godonia" + name + "Host";
		var slot = NormalizeSlot(request.DockSlot);
		var manifestPath = Path.Combine(pagesDir, pluginId + ".json");

		if (Directory.Exists(pluginDir) && Directory.EnumerateFileSystemEntries(pluginDir).Any())
			return Fail($"Folder already exists: {pluginDir}");
		if (File.Exists(manifestPath))
			return Fail($"A plugin with id '{pluginId}' already exists.");

		Directory.CreateDirectory(pluginDir);
		Directory.CreateDirectory(pagesDir);

		var written = new List<string>();
		var tokens = new Dictionary<string, string>(StringComparer.Ordinal) {
			["__TITLE__"] = title,
			["__NAME__"] = name,
			["__NS__"] = ns,
			["__PLUGIN_ID__"] = pluginId,
			["__PROJECT__"] = projectName,
			["__HOST_NAME__"] = hostName,
			["__GODOT_NS__"] = game + ".Editor",
			["__SLOT__"] = slot,
			["__LAYOUTS__"] = slot == "Bottom"
				? "EditorDock.DockLayout.Horizontal | EditorDock.DockLayout.Floating"
				: "EditorDock.DockLayout.Vertical | EditorDock.DockLayout.Floating",
			["__MIN_SIZE__"] = slot == "Bottom" ? "new Vector2(420, 160)" : "new Vector2(320, 240)"
		};

		var propsPath = FindEditorPluginProps(godotRoot, pluginDir);
		Write(Path.Combine(pluginDir, projectName + ".csproj"), PluginCsproj(propsPath, request.UseMvvm, godotRoot, pluginDir, projectName), written);
		Write(Path.Combine(pluginDir, name + "Page.cs"), Replace(PageCs, tokens), written);

		if (request.UseMvvm) {
			Write(Path.Combine(pluginDir, name + "View.axaml"), Replace(MvvmViewAxaml, tokens), written);
			Write(Path.Combine(pluginDir, name + "View.axaml.cs"), Replace(MvvmViewCode, tokens), written);
			Write(Path.Combine(pluginDir, name + "ViewModel.cs"), Replace(ViewModelCs, tokens), written);
		}
		else {
			Write(Path.Combine(pluginDir, name + "View.axaml"), Replace(PlainViewAxaml, tokens), written);
			Write(Path.Combine(pluginDir, name + "View.axaml.cs"), Replace(PlainViewCode, tokens), written);
		}

		Write(manifestPath, Replace(PluginJson, tokens), written);

		if (request.EnableInProject)
			EnableAddon(godotRoot, hostAddonFolder);

		if (EditorPluginCatalog.IsRunningInsideGodot) {
			EditorPluginCatalog.RegisterManifest(manifestPath);
			EditorPluginCatalog.NotifyPagesChanged();
		}

		if (request.AddToSolution)
			TryAddToSolution(godotRoot, pluginDir, projectName);

		if (request.AddToPreview)
			TryAddToPreview(godotRoot, pluginDir, projectName);

		return new EditorPluginScaffoldResult {
			Success = true,
			PluginProjectDir = pluginDir,
			AddonDir = pagesDir,
			ManifestPath = manifestPath,
			WrittenFiles = written,
			Message = $"Created {projectName}. Edit the AXAML there and build that project — Godot Build is not needed."
		};
	}

	public static string SanitizePascal(string raw) {
		if (string.IsNullOrWhiteSpace(raw))
			return "";

		var parts = Regex.Split(raw.Trim(), @"[^A-Za-z0-9]+");
		var sb = new StringBuilder();
		foreach (var part in parts) {
			if (part.Length == 0)
				continue;
			sb.Append(char.ToUpperInvariant(part[0]));
			if (part.Length > 1)
				sb.Append(part[1..]);
		}

		var name = sb.ToString();
		if (name.Length == 0 || !char.IsLetter(name[0]))
			return "";
		return name;
	}

	public static string GameIdentifier(string godotRoot) {
		var dir = Path.GetFullPath(godotRoot)
			.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		var folder = Path.GetFileName(dir);
		var pascal = SanitizePascal(folder);
		if (pascal.Length > 0)
			return pascal;

		var assembly = TryReadAssemblyName(dir);
		if (!string.IsNullOrEmpty(assembly))
			return assembly;

		return "Game";
	}

	public static string DefaultPluginId(string godotProjectName, string pascalName) {
		var game = ToDotId(godotProjectName);
		var plugin = ToDotId(pascalName);
		if (game.Length == 0)
			game = "game";
		return game + "." + plugin;
	}

	private static string ToDotId(string value) {
		var pascal = SanitizePascal(value);
		if (pascal.Length == 0)
			return "";
		var sb = new StringBuilder();
		for (var i = 0; i < pascal.Length; i++) {
			var c = pascal[i];
			if (i > 0 && char.IsUpper(c) && (char.IsLower(pascal[i - 1]) || (i + 1 < pascal.Length && char.IsLower(pascal[i + 1]))))
				sb.Append('.');
			sb.Append(char.ToLowerInvariant(c));
		}

		return sb.ToString();
	}

	private static string? TryReadAssemblyName(string godotRoot) {
		var path = Path.Combine(godotRoot, "project.godot");
		if (!File.Exists(path))
			return null;

		foreach (var line in File.ReadLines(path)) {
			var trimmed = line.Trim();
			const string key = "project/assembly_name=";
			if (!trimmed.StartsWith(key, StringComparison.Ordinal))
				continue;
			var value = trimmed[key.Length..].Trim().Trim('"');
			var pascal = SanitizePascal(value);
			if (pascal.Length > 0)
				return pascal;
		}

		return null;
	}

	private static bool IsSafePluginId(string id) {
		if (id.Length == 0 || id.StartsWith('.') || id.EndsWith('.'))
			return false;
		foreach (var c in id) {
			if (c is not ('.' or '_') && !char.IsLetterOrDigit(c))
				return false;
		}

		return id.Contains('.', StringComparison.Ordinal);
	}

	private static string NormalizeSlot(string slot)
		=> slot switch {
			"RightUr" or "Right" or "right" => "RightUr",
			"Bottom" or "bottom" => "Bottom",
			"MainScreen" or "Main" or "main" or "mainscreen" => "MainScreen",
			_ => "LeftUl"
		};

	public static bool IsMainScreenSlot(string? slot)
		=> string.Equals(NormalizeSlot(slot ?? ""), "MainScreen", StringComparison.OrdinalIgnoreCase);

	private static string? FindEditorPluginProps(string godotRoot, string pluginDir) {
		var candidates = new[] {
			Path.Combine(godotRoot, "Godonia.EditorPlugin.props"),
			Path.Combine(godotRoot, "..", "Godonia.EditorPlugin.props")
		};
		foreach (var candidate in candidates) {
			var full = Path.GetFullPath(candidate);
			if (File.Exists(full))
				return Path.GetRelativePath(pluginDir, full);
		}

		return null;
	}

	private static string PluginCsproj(string? propsRelative, bool mvvm, string godotRoot, string pluginDir, string projectName) {
		if (propsRelative is not null) {
			var targets = Path.ChangeExtension(propsRelative, ".targets");
			var mvvmRef = mvvm
				? $"{Environment.NewLine}\t<ItemGroup>{Environment.NewLine}\t\t<PackageReference Include=\"CommunityToolkit.Mvvm\" />{Environment.NewLine}\t</ItemGroup>{Environment.NewLine}"
				: "";
			return $"""
				<Project Sdk="Microsoft.NET.Sdk">

					<Import Project="{propsRelative.Replace('\\', '/')}" />

					<PropertyGroup>
						<AssemblyName>{projectName}</AssemblyName>
						<RootNamespace>{projectName.Replace(" ", "")}</RootNamespace>
					</PropertyGroup>
					{mvvmRef}
					<Import Project="{targets.Replace('\\', '/')}" />

				</Project>
				""";
		}

		var pluginOut = Path.GetRelativePath(pluginDir, Path.Combine(godotRoot, ".godot", "godonia-plugins")).Replace('\\', '/');
		var mvvmPkg = mvvm ? $"{Environment.NewLine}\t\t<PackageReference Include=\"CommunityToolkit.Mvvm\" />" : "";
		return $"""
			<Project Sdk="Microsoft.NET.Sdk">

				<PropertyGroup>
					<TargetFramework>net10.0</TargetFramework>
					<IsPackable>false</IsPackable>
					<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
					<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
					<GodoniaInjectHostScripts>false</GodoniaInjectHostScripts>
					<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
					<AssemblyName>{projectName}</AssemblyName>
					<RootNamespace>{projectName.Replace(" ", "")}</RootNamespace>
				</PropertyGroup>

				<ItemGroup>
					<PackageReference Include="Ouse.Godonia" />{mvvmPkg}
				</ItemGroup>

				<Target Name="CopyPluginToGodotCache" AfterTargets="Build">
					<PropertyGroup>
						<_PluginOut>$([MSBuild]::EnsureTrailingSlash('{pluginOut}/'))</_PluginOut>
						<_PluginStamp>$([System.DateTime]::UtcNow.Ticks)</_PluginStamp>
					</PropertyGroup>
					<ItemGroup>
						<_PluginSatelliteDlls Include="$(TargetDir)*.dll" />
						<_PluginSatelliteDlls Remove="$(TargetPath)" />
						<_PluginSatelliteDlls Remove="$(TargetDir)Avalonia*.dll" />
						<_PluginSatelliteDlls Remove="$(TargetDir)GodotSharp*.dll" />
						<_PluginSatelliteDlls Remove="$(TargetDir)Ouse.Godonia.dll" />
						<_PluginSatelliteDlls Remove="$(TargetDir)SkiaSharp*.dll" />
						<_PluginSatelliteDlls Remove="$(TargetDir)HarfBuzzSharp*.dll" />
						<_PluginSatelliteDlls Remove="$(TargetDir)MicroCom*.dll" />
					</ItemGroup>
					<MakeDir Directories="$(_PluginOut)" />
					<Copy SourceFiles="$(TargetPath)" DestinationFiles="$(_PluginOut)$(TargetName).$(_PluginStamp).dll" SkipUnchangedFiles="false" />
					<Copy SourceFiles="@(_PluginSatelliteDlls)" DestinationFolder="$(_PluginOut)" SkipUnchangedFiles="true" />
				</Target>

			</Project>
			""";
	}

	private static void EnableAddon(string godotRoot, string addonFolder) {
		var path = Path.Combine(godotRoot, "project.godot");
		if (!File.Exists(path))
			return;

		var entry = $"\"res://addons/{addonFolder}/plugin.cfg\"";
		var text = File.ReadAllText(path);
		if (text.Contains(entry, StringComparison.Ordinal))
			return;

		const string header = "[editor_plugins]";
		if (!text.Contains(header, StringComparison.Ordinal)) {
			File.AppendAllText(path, $"{Environment.NewLine}{header}{Environment.NewLine}{Environment.NewLine}enabled=PackedStringArray({entry}){Environment.NewLine}");
			return;
		}

		var pattern = new Regex(@"enabled=PackedStringArray\((.*)\)", RegexOptions.Singleline);
		var updated = pattern.Replace(text, match => {
			var inner = match.Groups[1].Value.Trim();
			if (inner.Length == 0)
				return $"enabled=PackedStringArray({entry})";
			return $"enabled=PackedStringArray({inner}, {entry})";
		}, 1);
		File.WriteAllText(path, updated);
	}

	private static void TryAddToSolution(string godotRoot, string pluginDir, string projectName) {
		var solution = FindSolutionUp(godotRoot, 4);
		if (solution is null)
			return;

		var text = File.ReadAllText(solution);
		var csproj = Path.Combine(pluginDir, projectName + ".csproj");
		var rel = Path.GetRelativePath(Path.GetDirectoryName(solution)!, csproj);
		var relUnix = rel.Replace('\\', '/');
		var relWin = rel.Replace('/', '\\');
		if (text.Contains(relUnix, StringComparison.OrdinalIgnoreCase)
			|| text.Contains(relWin, StringComparison.OrdinalIgnoreCase)
			|| text.Contains(projectName + ".csproj", StringComparison.OrdinalIgnoreCase))
			return;

		if (solution.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)) {
			TryAddToSlnx(solution, text, relUnix, projectName);
			return;
		}

		TryAddToLegacySln(solution, text, relWin, projectName);
	}

	private static void TryAddToSlnx(string path, string text, string relUnix, string projectName) {
		const string close = "</Solution>";
		var closeAt = text.LastIndexOf(close, StringComparison.OrdinalIgnoreCase);
		if (closeAt < 0)
			return;

		// Prefer nesting under an existing /Samples/ folder when present (repo layout).
		var samplesOpen = text.IndexOf("<Folder Name=\"/Samples/\">", StringComparison.OrdinalIgnoreCase);
		if (samplesOpen >= 0) {
			var samplesClose = text.IndexOf("</Folder>", samplesOpen, StringComparison.OrdinalIgnoreCase);
			if (samplesClose > samplesOpen) {
				var indented = $"    <Project Path=\"{relUnix}\" />{Environment.NewLine}";
				File.WriteAllText(path, text.Insert(samplesClose, indented));
				return;
			}
		}

		var line = $"  <Project Path=\"{relUnix}\" />{Environment.NewLine}";
		File.WriteAllText(path, text.Insert(closeAt, line));
	}

	private static void TryAddToLegacySln(string path, string text, string relSln, string projectName) {
		var guid = Guid.NewGuid().ToString("B").ToUpperInvariant();
		var typeGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
		var projectLine = $"Project(\"{typeGuid}\") = \"{projectName}\", \"{relSln}\", \"{guid}\"{Environment.NewLine}EndProject{Environment.NewLine}";

		var insertAt = text.IndexOf("Global", StringComparison.Ordinal);
		if (insertAt < 0)
			return;
		text = text.Insert(insertAt, projectLine);

		var cfgs = new StringBuilder();
		foreach (var cfg in new[] { "Debug", "Release" }) {
			foreach (var plat in new[] { "Any CPU", "x64", "x86" }) {
				cfgs.AppendLine(CultureInfo.InvariantCulture, $"\t\t{guid}.{cfg}|{plat}.ActiveCfg = {cfg}|Any CPU");
				cfgs.AppendLine(CultureInfo.InvariantCulture, $"\t\t{guid}.{cfg}|{plat}.Build.0 = {cfg}|Any CPU");
			}
		}

		const string cfgEnd = "\tEndGlobalSection";
		var cfgSection = text.IndexOf("GlobalSection(ProjectConfigurationPlatforms)", StringComparison.Ordinal);
		if (cfgSection >= 0) {
			var cfgSectionEnd = text.IndexOf(cfgEnd, cfgSection, StringComparison.Ordinal);
			if (cfgSectionEnd >= 0)
				text = text.Insert(cfgSectionEnd, cfgs.ToString());
		}

		var samples = Regex.Match(text, @"Project\(""\{2150E333-8FDC-42A3-9474-1A3956D46DE8\}""\) = ""Samples"", ""Samples"", ""(\{[^}]+\})""");
		if (samples.Success) {
			var nested = text.IndexOf("GlobalSection(NestedProjects)", StringComparison.Ordinal);
			if (nested >= 0) {
				var nestedEnd = text.IndexOf(cfgEnd, nested, StringComparison.Ordinal);
				if (nestedEnd >= 0)
					text = text.Insert(nestedEnd, $"\t\t{guid} = {samples.Groups[1].Value}{Environment.NewLine}");
			}
		}

		File.WriteAllText(path, text);
	}

	private static string? FindSolutionUp(string start, int maxHops) {
		var dir = new DirectoryInfo(start);
		for (var i = 0; i < maxHops && dir is not null; i++) {
			var slnx = dir.GetFiles("*.slnx");
			if (slnx.Length > 0)
				return slnx[0].FullName;
			var sln = dir.GetFiles("*.sln");
			if (sln.Length > 0)
				return sln[0].FullName;
			dir = dir.Parent;
		}

		return null;
	}

	private static void TryAddToPreview(string godotRoot, string pluginDir, string projectName) {
		var preview = EnsureEditorPreviewProject(godotRoot)
			?? Directory.GetFiles(Path.GetDirectoryName(godotRoot) ?? godotRoot, "*.Editor.Preview.csproj", SearchOption.TopDirectoryOnly)
				.FirstOrDefault()
			?? FindFileUp(godotRoot, "*.Editor.Preview.csproj", 3);
		if (preview is null || !File.Exists(preview))
			return;

		var text = File.ReadAllText(preview);
		var rel = Path.GetRelativePath(Path.GetDirectoryName(preview)!, Path.Combine(pluginDir, projectName + ".csproj")).Replace('\\', '/');
		if (text.Contains(rel, StringComparison.OrdinalIgnoreCase) || text.Contains(projectName + ".csproj", StringComparison.OrdinalIgnoreCase))
			return;

		var item = $"\t\t<ProjectReference Include=\"{rel}\" />{Environment.NewLine}";
		// Insert into the first ItemGroup that already has PackageReference/ProjectReference (or any ItemGroup).
		var close = text.IndexOf("</ItemGroup>", StringComparison.Ordinal);
		if (close < 0)
			return;
		File.WriteAllText(preview, text.Insert(close, item));
	}

	/// <summary>
	/// Creates <c>{Game}.Editor.Preview</c> (WinExe Avalonia host) next to the Godot project when missing,
	/// so designer preview works and new docks can be ProjectReferenced.
	/// </summary>
	private static string? EnsureEditorPreviewProject(string godotRoot) {
		var parent = Path.GetDirectoryName(godotRoot);
		if (string.IsNullOrEmpty(parent))
			return null;

		var existing = Directory.GetFiles(parent, "*.Editor.Preview.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
		if (existing is not null)
			return existing;

		var game = GameIdentifier(godotRoot);
		var previewName = game + ".Editor.Preview";
		var previewDir = Path.Combine(parent, previewName);
		var csprojPath = Path.Combine(previewDir, previewName + ".csproj");
		Directory.CreateDirectory(previewDir);

		File.WriteAllText(csprojPath, $$"""
			<Project Sdk="Microsoft.NET.Sdk">

				<PropertyGroup>
					<OutputType>WinExe</OutputType>
					<TargetFramework>net10.0</TargetFramework>
					<AssemblyName>{{previewName}}</AssemblyName>
					<RootNamespace>{{previewName}}</RootNamespace>
					<IsPackable>false</IsPackable>
					<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
					<GodoniaInjectHostScripts>false</GodoniaInjectHostScripts>
					<ApplicationManifest>app.manifest</ApplicationManifest>
					<BuiltInComInteropSupport>true</BuiltInComInteropSupport>
				</PropertyGroup>

				<ItemGroup>
					<PackageReference Include="Avalonia.Desktop" />
					<PackageReference Include="Avalonia.Themes.Fluent" />
					<PackageReference Include="Ouse.Godonia" />
				</ItemGroup>

			</Project>
			""");

		File.WriteAllText(Path.Combine(previewDir, "Program.cs"), $$"""
			using System;
			using Avalonia;

			namespace {{previewName}};

			internal static class Program {

				[STAThread]
				public static void Main(string[] args)
					=> BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

				public static AppBuilder BuildAvaloniaApp()
					=> AppBuilder
						.Configure<App>()
						.UsePlatformDetect()
						.UseSkia()
						.LogToTrace();

			}
			""");

		File.WriteAllText(Path.Combine(previewDir, "App.cs"), $$"""
			using Avalonia;
			using Avalonia.Controls.ApplicationLifetimes;
			using Avalonia.Styling;
			using Avalonia.Themes.Fluent;

			namespace {{previewName}};

			public sealed class App : Application {

				public override void Initialize() {
					Styles.Add(new FluentTheme());
					RequestedThemeVariant = ThemeVariant.Dark;
				}

				public override void OnFrameworkInitializationCompleted() {
					if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
						desktop.MainWindow = new MainWindow();

					base.OnFrameworkInitializationCompleted();
				}

			}
			""");

		File.WriteAllText(Path.Combine(previewDir, "MainWindow.cs"), $$"""
			using Avalonia;
			using Avalonia.Controls;
			using Avalonia.Layout;
			using Avalonia.Media;

			namespace {{previewName}};

			public sealed class MainWindow : Window {

				public MainWindow() {
					Title = "Godonia editor preview";
					Width = 800;
					Height = 560;
					Content = new TextBlock {
						Text = "Avalonia designer target. Open an editor-dock *.axaml to preview.\nNew docks are ProjectReferenced here automatically.",
						Margin = new Thickness(24),
						TextWrapping = TextWrapping.Wrap,
						VerticalAlignment = VerticalAlignment.Center
					};
				}

			}
			""");

		File.WriteAllText(Path.Combine(previewDir, "app.manifest"), $$"""
			<?xml version="1.0" encoding="utf-8"?>
			<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
				<assemblyIdentity version="1.0.0.0" name="{{previewName}}"/>
				<compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
					<application>
						<supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
					</application>
				</compatibility>
			</assembly>
			""");

		EnsurePreviewPackageVersions(parent);
		TryAddPreviewToSolution(godotRoot, csprojPath, previewName);
		return csprojPath;
	}

	private static void EnsurePreviewPackageVersions(string parentDir) {
		var packages = Path.Combine(parentDir, "Directory.Packages.props");
		if (!File.Exists(packages))
			return;

		var text = File.ReadAllText(packages);
		var updated = text;
		if (!updated.Contains("Avalonia.Desktop", StringComparison.Ordinal))
			updated = updated.Replace(
				"<PackageVersion Include=\"Avalonia\"",
				"<PackageVersion Include=\"Avalonia.Desktop\" Version=\"12.0.0\" />\n\t\t<PackageVersion Include=\"Avalonia.Themes.Fluent\" Version=\"12.0.0\" />\n\t\t<PackageVersion Include=\"Avalonia\"",
				StringComparison.Ordinal);
		else if (!updated.Contains("Avalonia.Themes.Fluent", StringComparison.Ordinal))
			updated = updated.Replace(
				"<PackageVersion Include=\"Avalonia.Desktop\"",
				"<PackageVersion Include=\"Avalonia.Themes.Fluent\" Version=\"12.0.0\" />\n\t\t<PackageVersion Include=\"Avalonia.Desktop\"",
				StringComparison.Ordinal);

		if (updated != text)
			File.WriteAllText(packages, updated);
	}

	private static void TryAddPreviewToSolution(string godotRoot, string previewCsproj, string previewName) {
		var solution = FindSolutionUp(godotRoot, 4);
		if (solution is null)
			return;

		var text = File.ReadAllText(solution);
		if (text.Contains(previewName + ".csproj", StringComparison.OrdinalIgnoreCase))
			return;

		var rel = Path.GetRelativePath(Path.GetDirectoryName(solution)!, previewCsproj).Replace('\\', '/');
		if (solution.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)) {
			TryAddToSlnx(solution, text, rel, previewName);
			return;
		}

		TryAddToLegacySln(solution, text, rel.Replace('/', '\\'), previewName);
	}

	private static string? FindFileUp(string start, string pattern, int maxHops) {
		var dir = new DirectoryInfo(start);
		for (var i = 0; i < maxHops && dir is not null; i++) {
			var hits = dir.GetFiles(pattern);
			if (hits.Length > 0)
				return hits[0].FullName;
			dir = dir.Parent;
		}

		return null;
	}

	private static void Write(string path, string contents, List<string> written) {
		File.WriteAllText(path, contents.Replace("\r\n", "\n").Replace("\n", Environment.NewLine));
		written.Add(path);
	}

	private static string Replace(string template, Dictionary<string, string> tokens) {
		foreach (var pair in tokens)
			template = template.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
		return template;
	}

	private static EditorPluginScaffoldResult Fail(string message)
		=> new() { Success = false, Message = message };

	private const string PageCs = """
		using Avalonia.Controls;
		using Ouse.Godonia;

		namespace __NS__;

		public sealed class __NAME__Page : IEditorPage {

			public string Id => "__PLUGIN_ID__";

			public string Title => "__TITLE__";

			public Control Create() => new __NAME__View();

		}
		""";

	private const string MvvmViewAxaml = """
		<UserControl xmlns="https://github.com/avaloniaui"
			xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
			xmlns:vm="using:__NS__"
			mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="450"
			x:Class="__NS__.__NAME__View"
			x:DataType="vm:__NAME__ViewModel">
			<ScrollViewer>
				<StackPanel Margin="16" Spacing="12">
					<TextBlock Text="{Binding Title}" FontSize="20" FontWeight="SemiBold" />
					<TextBlock Text="{Binding Status}" TextWrapping="Wrap" Opacity="0.85" />
					<TextBox Text="{Binding Notes}" PlaceholderText="Type here — this is a normal Avalonia MVVM view" />
					<Button Content="Ping" Command="{Binding PingCommand}" HorizontalAlignment="Left" />
				</StackPanel>
			</ScrollViewer>
		</UserControl>
		""";

	private const string MvvmViewCode = """
		using Avalonia.Controls;

		namespace __NS__;

		public partial class __NAME__View : UserControl {

			public __NAME__View() {
				InitializeComponent();
				DataContext ??= new __NAME__ViewModel();
			}

		}
		""";

	private const string ViewModelCs = """
		using System;
		using CommunityToolkit.Mvvm.ComponentModel;
		using CommunityToolkit.Mvvm.Input;

		namespace __NS__;

		public sealed partial class __NAME__ViewModel : ObservableObject {

			[ObservableProperty]
			private string _title = "__TITLE__";

			[ObservableProperty]
			private string _status = "Edit this XAML and ViewModel, then build this plugin project.";

			[ObservableProperty]
			private string _notes = "";

			[RelayCommand]
			private void Ping()
				=> Status = $"Ping {DateTime.Now:HH:mm:ss}";

		}
		""";

	private const string PlainViewAxaml = """
		<UserControl xmlns="https://github.com/avaloniaui"
			xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
			mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="450"
			x:Class="__NS__.__NAME__View">
			<ScrollViewer>
				<StackPanel Margin="16" Spacing="12">
					<TextBlock Text="__TITLE__" FontSize="20" FontWeight="SemiBold" />
					<TextBlock TextWrapping="Wrap"
						Text="Edit this AXAML and rebuild the plugin project. Focus Godot to reload the dock." />
					<TextBox PlaceholderText="Type here" />
				</StackPanel>
			</ScrollViewer>
		</UserControl>
		""";

	private const string PlainViewCode = """
		using Avalonia.Controls;

		namespace __NS__;

		public partial class __NAME__View : UserControl {

			public __NAME__View()
				=> InitializeComponent();

		}
		""";

	private const string PluginJson = """
		{
			"id": "__PLUGIN_ID__",
			"assembly": ".godot/godonia-plugins/__PROJECT__.dll",
			"pageType": "__NS__.__NAME__Page",
			"title": "__TITLE__",
			"dockSlot": "__SLOT__",
			"hostName": "__HOST_NAME__"
		}
		""";

}
