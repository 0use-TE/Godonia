using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Godot;
using Ouse.Godonia;

namespace GodotGame;

/// <summary>Autoload: initializes Avalonia once per process (platform + IME).</summary>
public partial class AvaloniaLoader : Node {

	[ModuleInitializer]
	internal static void HookGameAssemblyUnload() {
		var alc = AssemblyLoadContext.GetLoadContext(typeof(AvaloniaLoader).Assembly);
		if (alc is null || !alc.IsCollectible)
			return;

		alc.Unloading += _ => GodotAvalonia.ReleaseGameReferences();
	}

	public override void _Ready() {
		if (Engine.IsEditorHint())
			GodotAvalonia.EnsureStarted();
		else
			GodotAvalonia.EnsureStarted<App>();

		GetWindow()?.SetImeActive(true);
	}

}
