using AvControl = Avalonia.Controls.Control;

namespace JLeb.Estragonia;

/// <summary>
/// A plugin page hosted in a collectible ALC. Implement this in a class library that does
/// <b>not</b> use Godot.NET.Sdk and must not contain any <c>GodotObject</c> subclass.
/// </summary>
public interface IEditorPage {

	/// <summary>Stable id matching the plugin manifest.</summary>
	string Id { get; }

	/// <summary>Dock title.</summary>
	string Title { get; }

	/// <summary>Creates a new Avalonia root. Called after each reload.</summary>
	AvControl Create();

}
