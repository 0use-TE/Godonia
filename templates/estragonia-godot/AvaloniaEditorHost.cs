// Estragonia host script. Do not move to a class library. Package version: 1.1.0
#if TOOLS
using Godot;

namespace JLeb.Estragonia;

/// <summary>
/// Godot dock host for an Estragonia editor plugin page.
/// Must live in this Godot project (class name = file name). Each dock has its own host / TopLevel;
/// the Avalonia <c>Application</c> is process-wide.
/// </summary>
[Tool]
public partial class AvaloniaEditorHost : AvaloniaControl, IAvaloniaEditorHost {

	/// <summary>Catalog plugin id (matches <c>plugin.json</c>).</summary>
	public string PluginId { get; set; } = "";

	public override void _Ready() {
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ExpandFill;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		ClipContents = true;
		CaptureEmptyHits = true;
		FocusMode = FocusModeEnum.All;
		MouseFilter = MouseFilterEnum.Stop;

		if (!string.IsNullOrEmpty(PluginId))
			EditorPluginCatalog.RegisterHost(this);

		if (Size.X > 1f && Size.Y > 1f)
			base._Ready();
	}

	public override void _ExitTree() {
		if (!string.IsNullOrEmpty(PluginId))
			EditorPluginCatalog.UnregisterHost(this);

		base._ExitTree();
	}

	public override void _Process(double delta) {
		if (!IsInitialized && Size.X > 1f && Size.Y > 1f)
			base._Ready();

		if (IsInitialized && Control is null && !string.IsNullOrEmpty(PluginId))
			EditorPluginCatalog.ShowPage(this);

		base._Process(delta);
		QueueRedraw();
	}

	/// <summary>Unloads and reloads this plugin's collectible ALC, then assigns a new page.</summary>
	public void Reload() {
		if (string.IsNullOrEmpty(PluginId))
			return;

		EditorPluginCatalog.Reload(PluginId);
	}

}
#endif
