using System;
using System.Reflection;
using System.Threading;
using Avalonia;
using Godot;

namespace Ouse.Godonia;

/// <summary>Public helpers for hosting Avalonia inside Godot.</summary>
public static class GodotAvalonia {

	private static bool s_started;
	private static int s_releasingGameRefs;

	/// <summary>
	/// Whether <see cref="AppBuilderExtensions.UseGodot"/> has completed for this process.
	/// </summary>
	public static bool IsStarted
		=> s_started || Application.Current is not null;

	/// <summary>
	/// Starts Avalonia with <see cref="EditorAvaloniaApp"/> (this assembly). Prefer this in the editor
	/// so the game project assembly is not the <see cref="Application"/> type.
	/// </summary>
	public static void EnsureStarted()
		=> EnsureStarted<EditorAvaloniaApp>();

	/// <summary>
	/// Starts Avalonia on the Godot platform. No-op if already started.
	/// One process, one <see cref="Application"/>. Do not <c>Shutdown</c> and restart.
	/// </summary>
	public static void EnsureStarted<TApp>()
		where TApp : Application, new() {
		if (s_started)
			return;

		if (Application.Current is not null) {
			s_started = true;
			return;
		}

		if (RenderingServer.GetRenderingDevice() is null)
			throw new NotSupportedException("Godonia requires a Vulkan renderer (Forward+ or Mobile).");

		AppBuilder
			.Configure<TApp>()
			.UseGodot()
			.SetupWithoutStarting();

		EnsureAssetLoader(typeof(TApp).Assembly);
		s_started = true;
	}

	/// <summary>
	/// Drops references into the game collectible ALC so Godot can unload it.
	/// Does not shut down Avalonia.
	/// </summary>
	public static void ReleaseGameReferences() {
		if (Interlocked.Exchange(ref s_releasingGameRefs, 1) == 1)
			return;

		try {
			AvaloniaControlEngine.DisposeAll();
			EditorPluginCatalog.ReleaseForGameReload();
		}
		catch (Exception ex) {
			GD.PrintErr($"Godonia: ReleaseGameReferences failed: {ex.Message}");
		}
		finally {
			Interlocked.Exchange(ref s_releasingGameRefs, 0);
		}
	}

	/// <summary>
	/// Ensures Avalonia can load <c>avares</c> assets and XAML image sources.
	/// Call this after <c>UseGodot().SetupWithoutStarting()</c> and before creating views that load assets.
	/// </summary>
	public static void EnsureAssetLoader(Assembly? defaultAssembly = null)
		=> GodotPlatform.EnsureAssetLoader(defaultAssembly);

}
