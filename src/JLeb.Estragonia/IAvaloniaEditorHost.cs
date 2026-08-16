using AvControl = Avalonia.Controls.Control;

namespace JLeb.Estragonia;

/// <summary>
/// Godot-side dock host that Catalog can swap pages on without storing plugin types.
/// Implemented by the in-project <c>AvaloniaEditorHost</c> script.
/// </summary>
public interface IAvaloniaEditorHost {

	string PluginId { get; }

	AvControl? Control { get; set; }

}
