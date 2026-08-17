using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace GodotGame.Editor.Preview;

/// <summary>
/// Desktop host so Avalonia designer can preview editor-dock AXAML.
/// Set this project as the solution startup project for design-time preview.
/// </summary>
public sealed class MainWindow : Window {

	public MainWindow() {
		Title = "Godonia editor preview";
		Width = 800;
		Height = 560;
		Content = new TextBlock {
			Text = "Avalonia designer target.\n\nOpen an editor-dock *.axaml and use the preview pane.\nNew docks created in Godot are ProjectReferenced here automatically.",
			Margin = new Thickness(24),
			TextWrapping = TextWrapping.Wrap,
			VerticalAlignment = VerticalAlignment.Center
		};
	}

}
