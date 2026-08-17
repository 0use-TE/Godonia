using Avalonia;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Ouse.Godonia;

/// <summary>
/// Process-wide Avalonia <see cref="Application"/> for the Godot <b>editor</b> process.
/// Lives in this assembly so editor plugins do not pin the game project assembly.
/// Fluent + Dark is the editor-process default. The game's <c>UI/App.axaml</c> runs in
/// the separate play process and can use Semi / other themes. Do not subclass this from
/// game code or Prism: there is one Application per process and it is never Shutdown/reloaded.
/// </summary>
public sealed class EditorAvaloniaApp : Application {

	public override void Initialize() {
		Styles.Add(new FluentTheme());
		RequestedThemeVariant = ThemeVariant.Dark;
	}

}
