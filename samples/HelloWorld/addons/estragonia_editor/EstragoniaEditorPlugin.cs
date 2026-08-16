#if TOOLS
using Godot;
using JLeb.Estragonia;

namespace HelloWorld.Editor;

[Tool]
public partial class EstragoniaEditorPlugin : EditorPlugin {

	private EditorDock? _dock;
	private AvaloniaEditorHost? _host;

	public override void _EnterTree() {
		AvaloniaEditorRuntime.EnsureStarted();
		EditorPluginCatalog.RegisterManifest(ProjectSettings.GlobalizePath("res://addons/estragonia_editor/plugin.json"));
		EnsureDock();
	}

	public override void _ExitTree() {
		_host?.Detach();
		_host = null;

		if (_dock is not null) {
			RemoveDock(_dock);
			_dock.QueueFree();
			_dock = null;
		}
	}

	public override void _Notification(int what) {
		if (what == NotificationApplicationFocusIn)
			EditorPluginCatalog.ReloadChanged();
	}

	private void EnsureDock() {
		if (_host is not null && GodotObject.IsInstanceValid(_host)) {
			_host.PluginId = "estragonia.demo";
			return;
		}

		var existing = EditorInterface.Singleton.GetBaseControl().FindChild("EstragoniaHost", true, false) as AvaloniaEditorHost;
		if (existing is not null && GodotObject.IsInstanceValid(existing)) {
			_host = existing;
			_host.PluginId = "estragonia.demo";
			return;
		}

		_host = new AvaloniaEditorHost {
			Name = "EstragoniaHost",
			CustomMinimumSize = new Vector2(320, 240),
			PluginId = "estragonia.demo"
		};

		_dock = new EditorDock {
			Title = "Estragonia",
			ClipContents = true,
			DefaultSlot = EditorDock.DockSlot.LeftUl,
			AvailableLayouts = EditorDock.DockLayout.Vertical | EditorDock.DockLayout.Floating
		};
		_dock.AddChild(_host);
		AddDock(_dock);
	}

}
#endif
