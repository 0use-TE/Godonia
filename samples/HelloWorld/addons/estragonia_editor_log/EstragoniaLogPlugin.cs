#if TOOLS
using Godot;
using JLeb.Estragonia;

namespace HelloWorld.EditorLog;

[Tool]
public partial class EstragoniaLogPlugin : EditorPlugin {

	private EditorDock? _dock;
	private AvaloniaEditorHost? _host;

	public override void _EnterTree() {
		AvaloniaEditorRuntime.EnsureStarted();
		EditorPluginCatalog.RegisterManifest(ProjectSettings.GlobalizePath("res://addons/estragonia_editor_log/plugin.json"));
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
			_host.PluginId = "estragonia.log";
			return;
		}

		var existing = EditorInterface.Singleton.GetBaseControl().FindChild("EstragoniaLogHost", true, false) as AvaloniaEditorHost;
		if (existing is not null && GodotObject.IsInstanceValid(existing)) {
			_host = existing;
			_host.PluginId = "estragonia.log";
			return;
		}

		_host = new AvaloniaEditorHost {
			Name = "EstragoniaLogHost",
			CustomMinimumSize = new Vector2(420, 160),
			PluginId = "estragonia.log"
		};

		_dock = new EditorDock {
			Title = "Estragonia Log",
			ClipContents = true,
			DefaultSlot = EditorDock.DockSlot.Bottom,
			AvailableLayouts = EditorDock.DockLayout.Horizontal | EditorDock.DockLayout.Floating
		};
		_dock.AddChild(_host);
		AddDock(_dock);
	}

}
#endif
