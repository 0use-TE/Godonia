#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using Ouse.Godonia;

namespace Ouse.Godonia.Addons;

[Tool]
public partial class GodoniaNewPlugin : EditorPlugin {

	private const string PluginCfg = "res://addons/godonia_new_plugin/plugin.cfg";

	private EditorDock? _wizardDock;
	private AvaloniaEditorHost? _wizardHost;
	private readonly Dictionary<string, PageDock> _pages = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<MainScreenPage> _mainScreens = [];
	private TabContainer? _mainScreenTabs;
	private bool _bounceScheduled;
	private int _lastMainScreenCount = -1;

	public override void _EnterTree() {
		AvaloniaEditorRuntime.EnsureStarted();
		EditorPluginCatalog.PagesChanged += LoadPageManifests;
		EnsureWizardDock();
		LoadPageManifests();
	}

	public override void _ExitTree() {
		EditorPluginCatalog.PagesChanged -= LoadPageManifests;

		if (_wizardHost is not null && GodotObject.IsInstanceValid(_wizardHost))
			_wizardHost.Control = null;
		_wizardHost = null;

		if (_wizardDock is not null) {
			RemoveDock(_wizardDock);
			_wizardDock.QueueFree();
			_wizardDock = null;
		}

		foreach (var page in _pages.Values)
			TearDown(page);
		_pages.Clear();

		TearDownMainScreen();
	}

	public override void _Notification(int what) {
		if (what == NotificationApplicationFocusIn) {
			LoadPageManifests();
			EditorPluginCatalog.ReloadChanged();
		}
	}

	public override bool _HasMainScreen()
		// Godot calls this in add_editor_plugin() BEFORE add_child/_EnterTree,
		// so peek manifests on disk when runtime list is still empty.
		=> _mainScreens.Count > 0 || PeekMainScreenTitles().Count > 0;

	public override string _GetPluginName() {
		if (_mainScreens.Count == 1)
			return _mainScreens[0].Title;
		if (_mainScreens.Count > 1)
			return MultiMainScreenName();

		var peeked = PeekMainScreenTitles();
		if (peeked.Count == 1)
			return peeked[0];
		if (peeked.Count > 1)
			return MultiMainScreenName();
		return "Avalonia";
	}

	private string MultiMainScreenName()
		=> EditorPluginWizardL10n.IsChinese(ReadEditorLocale())
			? "Avalonia 工具"
			: "Avalonia";

	private static List<string> PeekMainScreenTitles() {
		var titles = new List<string>();
		try {
			var dir = ProjectSettings.GlobalizePath("res://addons/godonia_new_plugin/pages");
			if (!Directory.Exists(dir))
				return titles;

			foreach (var json in Directory.EnumerateFiles(dir, "*.json")) {
				try {
					var manifest = JsonSerializer.Deserialize<EditorPluginManifest>(File.ReadAllText(json));
					if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id))
						continue;
					if (!EditorPluginScaffolder.IsMainScreenSlot(manifest.DockSlot))
						continue;
					titles.Add(string.IsNullOrWhiteSpace(manifest.Title) ? manifest.Id : manifest.Title);
				}
				catch {
				}
			}
		}
		catch {
		}

		return titles;
	}

	private static string ReadEditorLocale() {
		try {
			return EditorInterface.Singleton.GetEditorSettings()
				.GetSetting("interface/editor/editor_language").AsString();
		}
		catch {
			return EditorPluginWizardL10n.DetectLocale();
		}
	}

	public override void _MakeVisible(bool visible) {
		if (_mainScreenTabs is not null && GodotObject.IsInstanceValid(_mainScreenTabs))
			_mainScreenTabs.Visible = visible;
	}

	private void EnsureWizardDock() {
		var locale = ReadEditorLocale();
		var title = EditorPluginWizardL10n.ForLocale(locale).DockTitle;
		var existing = EditorInterface.Singleton.GetBaseControl().FindChild("GodoniaNewPluginHost", true, false) as AvaloniaEditorHost;
		if (existing is not null && GodotObject.IsInstanceValid(existing)) {
			_wizardHost = existing;
			_wizardHost.Control ??= new EditorPluginWizardView(locale);
			return;
		}

		_wizardHost = new AvaloniaEditorHost {
			Name = "GodoniaNewPluginHost",
			CustomMinimumSize = new Vector2(320, 280)
		};
		_wizardHost.Control = new EditorPluginWizardView(locale);

		_wizardDock = new EditorDock {
			Title = title,
			ClipContents = true,
			DefaultSlot = EditorDock.DockSlot.LeftBl,
			AvailableLayouts = EditorDock.DockLayout.Vertical | EditorDock.DockLayout.Floating
		};
		_wizardDock.AddChild(_wizardHost);
		AddDock(_wizardDock);
	}

	private void LoadPageManifests() {
		var dir = ProjectSettings.GlobalizePath("res://addons/godonia_new_plugin/pages");
		var dockSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var mainScreenSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		if (Directory.Exists(dir)) {
			foreach (var json in Directory.EnumerateFiles(dir, "*.json")) {
				EditorPluginCatalog.RegisterManifest(json);
				var manifest = TryReadManifest(json);
				if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id))
					continue;

				if (EditorPluginScaffolder.IsMainScreenSlot(manifest.DockSlot)) {
					mainScreenSeen.Add(manifest.Id);
					EnsureMainScreenPage(manifest);
				}
				else {
					dockSeen.Add(manifest.Id);
					EnsurePageDock(manifest);
				}
			}
		}

		foreach (var id in _pages.Keys.ToArray()) {
			if (dockSeen.Contains(id))
				continue;
			TearDown(_pages[id]);
			_pages.Remove(id);
		}

		for (var i = _mainScreens.Count - 1; i >= 0; i--) {
			if (!mainScreenSeen.Contains(_mainScreens[i].Id)) {
				TearDownMainScreenPage(_mainScreens[i]);
				_mainScreens.RemoveAt(i);
			}
		}

		SyncMainScreenTabs();
		MaybeBounceForMainScreenButton();
	}

	private static EditorPluginManifest? TryReadManifest(string manifestPath) {
		try {
			return JsonSerializer.Deserialize<EditorPluginManifest>(File.ReadAllText(manifestPath));
		}
		catch {
			return null;
		}
	}

	private void EnsurePageDock(EditorPluginManifest manifest) {
		if (_pages.TryGetValue(manifest.Id, out var existing) && existing.Host is not null && GodotObject.IsInstanceValid(existing.Host)) {
			existing.Host.PluginId = manifest.Id;
			return;
		}

		var hostName = string.IsNullOrWhiteSpace(manifest.HostName)
			? "Godonia" + manifest.Id.Replace(".", "", StringComparison.Ordinal) + "Host"
			: manifest.HostName;
		var title = string.IsNullOrWhiteSpace(manifest.Title) ? manifest.Id : manifest.Title;
		var slot = ParseDockSlot(manifest.DockSlot);

		var found = EditorInterface.Singleton.GetBaseControl().FindChild(hostName, true, false) as AvaloniaEditorHost;
		if (found is not null && GodotObject.IsInstanceValid(found)) {
			found.PluginId = manifest.Id;
			_pages[manifest.Id] = new PageDock { Host = found };
			return;
		}

		var host = new AvaloniaEditorHost {
			Name = hostName,
			CustomMinimumSize = slot == EditorDock.DockSlot.Bottom ? new Vector2(420, 160) : new Vector2(320, 240),
			PluginId = manifest.Id
		};

		var dock = new EditorDock {
			Title = title,
			ClipContents = true,
			DefaultSlot = slot,
			AvailableLayouts = slot == EditorDock.DockSlot.Bottom
				? EditorDock.DockLayout.Horizontal | EditorDock.DockLayout.Floating
				: EditorDock.DockLayout.Vertical | EditorDock.DockLayout.Floating
		};
		dock.AddChild(host);
		AddDock(dock);
		_pages[manifest.Id] = new PageDock { Dock = dock, Host = host };
	}

	private void EnsureMainScreenPage(EditorPluginManifest manifest) {
		var existing = _mainScreens.FirstOrDefault(p => string.Equals(p.Id, manifest.Id, StringComparison.OrdinalIgnoreCase));
		if (existing is not null) {
			existing.Title = string.IsNullOrWhiteSpace(manifest.Title) ? manifest.Id : manifest.Title;
			if (existing.Host is not null && GodotObject.IsInstanceValid(existing.Host))
				existing.Host.PluginId = manifest.Id;
			return;
		}

		var hostName = string.IsNullOrWhiteSpace(manifest.HostName)
			? "Godonia" + manifest.Id.Replace(".", "", StringComparison.Ordinal) + "MainHost"
			: manifest.HostName + "Main";
		var title = string.IsNullOrWhiteSpace(manifest.Title) ? manifest.Id : manifest.Title;

		var host = new AvaloniaEditorHost {
			Name = hostName,
			PluginId = manifest.Id,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		_mainScreens.Add(new MainScreenPage {
			Id = manifest.Id,
			Title = title,
			Host = host
		});
	}

	private void SyncMainScreenTabs() {
		if (_mainScreens.Count == 0) {
			TearDownMainScreen();
			return;
		}

		if (_mainScreenTabs is null || !GodotObject.IsInstanceValid(_mainScreenTabs)) {
			_mainScreenTabs = new TabContainer {
				Name = "GodoniaMainScreenTabs",
				Visible = false,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill
			};
			_mainScreenTabs.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			EditorInterface.Singleton.GetEditorMainScreen().AddChild(_mainScreenTabs);
		}

		var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var page in _mainScreens) {
			keep.Add(page.Id);
			if (page.Host is null || !GodotObject.IsInstanceValid(page.Host))
				continue;

			if (page.Host.GetParent() != _mainScreenTabs) {
				page.Host.GetParent()?.RemoveChild(page.Host);
				_mainScreenTabs.AddChild(page.Host);
			}

			var index = page.Host.GetIndex();
			_mainScreenTabs.SetTabTitle(index, page.Title);
		}

		for (var i = _mainScreenTabs.GetChildCount() - 1; i >= 0; i--) {
			if (_mainScreenTabs.GetChild(i) is not AvaloniaEditorHost host)
				continue;
			if (keep.Contains(host.PluginId))
				continue;
			_mainScreenTabs.RemoveChild(host);
			host.Detach();
			host.QueueFree();
		}

		_mainScreenTabs.TabsVisible = _mainScreens.Count > 1;
	}

	private void MaybeBounceForMainScreenButton() {
		var count = _mainScreens.Count;
		var previous = _lastMainScreenCount;
		_lastMainScreenCount = count;
		// First enable: has_main_screen already peeked manifests (see _HasMainScreen).
		// Bounce only when the first MainScreen appears while the plugin was already enabled.
		if (previous == 0 && count > 0 && !_bounceScheduled) {
			_bounceScheduled = true;
			CallDeferred(nameof(BouncePluginEnabled));
		}
	}

	private void BouncePluginEnabled() {
		_bounceScheduled = false;
		try {
			EditorInterface.Singleton.SetPluginEnabled(PluginCfg, false);
			EditorInterface.Singleton.SetPluginEnabled(PluginCfg, true);
		}
		catch (Exception ex) {
			GD.PrintErr($"Godonia: could not refresh main screen button: {ex.Message}");
		}
	}

	private static EditorDock.DockSlot ParseDockSlot(string? slot)
		=> slot switch {
			"RightUr" or "Right" or "right" => EditorDock.DockSlot.RightUr,
			"Bottom" or "bottom" => EditorDock.DockSlot.Bottom,
			_ => EditorDock.DockSlot.LeftUl
		};

	private void TearDown(PageDock page) {
		if (page.Host is not null && GodotObject.IsInstanceValid(page.Host))
			page.Host.Detach();
		if (page.Dock is not null) {
			RemoveDock(page.Dock);
			page.Dock.QueueFree();
		}
	}

	private void TearDownMainScreenPage(MainScreenPage page) {
		if (page.Host is not null && GodotObject.IsInstanceValid(page.Host)) {
			page.Host.GetParent()?.RemoveChild(page.Host);
			page.Host.Detach();
			page.Host.QueueFree();
		}
	}

	private void TearDownMainScreen() {
		foreach (var page in _mainScreens)
			TearDownMainScreenPage(page);
		_mainScreens.Clear();

		if (_mainScreenTabs is not null && GodotObject.IsInstanceValid(_mainScreenTabs)) {
			_mainScreenTabs.GetParent()?.RemoveChild(_mainScreenTabs);
			_mainScreenTabs.QueueFree();
		}

		_mainScreenTabs = null;
	}

	private sealed class PageDock {
		public EditorDock? Dock { get; init; }
		public AvaloniaEditorHost? Host { get; init; }
	}

	private sealed class MainScreenPage {
		public required string Id { get; init; }
		public required string Title { get; set; }
		public AvaloniaEditorHost? Host { get; init; }
	}

}
#endif
