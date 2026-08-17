using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using Godot;
using AvControl = Avalonia.Controls.Control;

namespace Ouse.Godonia;

/// <summary>
/// Loads editor plugin pages from collectible ALCs and swaps them onto registered hosts.
/// Does not store plugin concrete types or subscribe to plugin instance events.
/// </summary>
public static class EditorPluginCatalog {

	private static readonly object s_lock = new();
	private static readonly Dictionary<string, EditorPluginManifest> s_manifests = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<string, LoadedPlugin> s_loaded = new(StringComparer.OrdinalIgnoreCase);
	private static readonly List<IAvaloniaEditorHost> s_hosts = [];
	private static bool s_insideGodot;
	private static bool s_releasingGameAlc;
	private static AssemblyLoadContext? s_hookedGameAlc;
	private static DispatcherTimer? s_watchTimer;

	private static readonly JsonSerializerOptions s_json = new() {
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	/// <summary>Host plugin subscribes so new <c>pages/*.json</c> docks appear without a Godot rebuild.</summary>
	public static event Action? PagesChanged;

	public static void NotifyPagesChanged()
		=> PagesChanged?.Invoke();

	/// <summary>Reads a <c>plugin.json</c> and registers it by id. <paramref name="manifestPath"/> is a filesystem path.</summary>
	public static void RegisterManifest(string manifestPath) {
		MarkRunningInsideGodot();

		if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath)) {
			GD.PrintErr($"Godonia: plugin manifest not found: {manifestPath}");
			return;
		}

		EditorPluginManifest? manifest;
		try {
			manifest = JsonSerializer.Deserialize<EditorPluginManifest>(File.ReadAllText(manifestPath), s_json);
		}
		catch (Exception ex) {
			GD.PrintErr($"Godonia: failed to parse {manifestPath}: {ex.Message}");
			return;
		}

		if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id) || string.IsNullOrWhiteSpace(manifest.Assembly) || string.IsNullOrWhiteSpace(manifest.PageType)) {
			GD.PrintErr($"Godonia: invalid plugin manifest {manifestPath}");
			return;
		}

		var projectRoot = ProjectSettings.GlobalizePath("res://");
		manifest.ResolvedAssemblyPath = Path.GetFullPath(Path.Combine(projectRoot, manifest.Assembly));

		lock (s_lock)
			s_manifests[manifest.Id] = manifest;
	}

	/// <summary>Registers every <c>*.json</c> manifest in <paramref name="directory"/>.</summary>
	public static void RegisterManifestsInDirectory(string directory) {
		if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
			return;

		foreach (var json in Directory.EnumerateFiles(directory, "*.json"))
			RegisterManifest(json);
	}

	/// <summary>
	/// Marks this process as the Godot editor. Desktop preview never calls this,
	/// so <see cref="Reload"/> stays a no-op without touching Godot native APIs.
	/// </summary>
	public static void MarkRunningInsideGodot()
		=> s_insideGodot = true;

	/// <summary>Registers a Godot-side host. Called from <c>AvaloniaEditorHost._Ready</c>.</summary>
	public static void RegisterHost(IAvaloniaEditorHost host) {
		MarkRunningInsideGodot();
		ArgumentNullException.ThrowIfNull(host);
		s_releasingGameAlc = false;
		HookGameAssemblyUnload(host.GetType().Assembly);

		lock (s_lock) {
			if (!s_hosts.Contains(host))
				s_hosts.Add(host);
		}

		ShowPage(host);
		EnsureWatchTimer();
	}

	/// <summary>Drops a host so Reload will not assign pages to a freed dock.</summary>
	public static void UnregisterHost(IAvaloniaEditorHost host) {
		if (host is null)
			return;

		lock (s_lock)
			s_hosts.Remove(host);
	}

	/// <summary>Creates (or reuses) the plugin page and assigns it to <paramref name="host"/>.</summary>
	public static void ShowPage(IAvaloniaEditorHost host) {
		if (host is null || string.IsNullOrEmpty(host.PluginId))
			return;

		if (host.Control is not null)
			return;

		if (s_releasingGameAlc)
			return;

		var control = CreatePage(host.PluginId);
		if (control is not null)
			host.Control = control;
	}

	/// <summary>Instantiates the page for <paramref name="id"/> without storing the plugin type.</summary>
	public static AvControl? CreatePage(string id) {
		if (!TryGetManifest(id, out var manifest))
			return null;

		try {
			var loaded = EnsureLoaded(manifest);
			var expectedName = Path.GetFileNameWithoutExtension(manifest.ResolvedAssemblyPath);
			var assembly = loaded.LoadContext.Assemblies.FirstOrDefault(a =>
					string.Equals(a.GetName().Name, expectedName, StringComparison.OrdinalIgnoreCase))
				?? loaded.LoadContext.LoadPluginFromPath(ResolveNewestAssembly(manifest.ResolvedAssemblyPath));

			var type = assembly.GetType(manifest.PageType, throwOnError: false);
			if (type is null || Activator.CreateInstance(type) is not IEditorPage page) {
				GD.PrintErr($"Godonia: type '{manifest.PageType}' was not found or does not implement IEditorPage.");
				return null;
			}

			var control = page.Create();
			return control;
		}
		catch (Exception ex) {
			GD.PrintErr($"Godonia: failed to create page '{id}': {ex}");
			return null;
		}
	}

	/// <summary>Unloads this plugin ALC and shows a fresh page on hosts that still display it.</summary>
	public static void Reload(string id) {
		if (string.IsNullOrEmpty(id))
			return;

		if (!IsRunningInsideGodot) {
			EditorLogHub.Write($"Reload('{id}') skipped: not running inside Godot.");
			return;
		}

		DetachHosts(id);
		UnloadPlugin(id, recreate: true);
		AssignPages(id);
	}

	/// <summary>Reloads plugins whose DLL timestamp changed since last load.</summary>
	public static void ReloadChanged() {
		if (!IsRunningInsideGodot)
			return;

		List<string> changed;
		lock (s_lock) {
			changed = s_manifests.Values
				.Where(IsDllNewerThanLoaded)
				.Select(m => m.Id)
				.ToList();
		}

		foreach (var id in changed)
			Reload(id);
	}

	private static void EnsureWatchTimer() {
		if (s_watchTimer is not null || !s_insideGodot || Application.Current is null)
			return;

		s_watchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
		s_watchTimer.Tick += (_, _) => {
			try {
				ReloadChanged();
			}
			catch (Exception ex) {
				GD.PrintErr($"Godonia: plugin watch failed: {ex.Message}");
			}
		};
		s_watchTimer.Start();
	}

	/// <summary>Reloads every registered plugin.</summary>
	public static void ReloadAll() {
		List<string> ids;
		lock (s_lock)
			ids = s_manifests.Keys.ToList();

		foreach (var id in ids)
			Reload(id);
	}

	/// <summary>Unloads every plugin ALC without creating new pages (game ALC is going away).</summary>
	public static void UnloadAll() {
		List<string> ids;
		lock (s_lock)
			ids = s_loaded.Keys.ToList();

		foreach (var id in ids)
			UnloadPlugin(id, recreate: false);
	}

	/// <summary>
	/// Drops Godot-side hosts and plugin pages before the game collectible ALC unloads.
	/// Avalonia / this catalog stay loaded in the persistent runtime ALC.
	/// </summary>
	public static void ReleaseForGameReload() {
		s_releasingGameAlc = true;
		try {
			IAvaloniaEditorHost[] hosts;
			lock (s_lock)
				hosts = s_hosts.ToArray();

			foreach (var host in hosts) {
				try {
					host.Control = null;
				}
				catch {
					// Host may already be disposing with the game ALC.
				}
			}

			lock (s_lock)
				s_hosts.Clear();

			UnloadAll();
		}
		finally {
			s_releasingGameAlc = false;
		}
	}

	/// <summary>
	/// False in Avalonia desktop preview / unit tests, where Godot native APIs are not available.
	/// Set by <see cref="MarkRunningInsideGodot"/> from Godot-side plugin code.
	/// </summary>
	public static bool IsRunningInsideGodot
		=> s_insideGodot;

	private static bool IsDllNewerThanLoaded(EditorPluginManifest manifest) {
		var path = ResolveNewestAssembly(manifest.ResolvedAssemblyPath);
		if (!File.Exists(path))
			return false;

		if (!s_loaded.TryGetValue(manifest.Id, out var loaded))
			return true;

		if (!string.Equals(path, loaded.LoadedPath, StringComparison.OrdinalIgnoreCase))
			return true;

		return File.GetLastWriteTimeUtc(path) > loaded.Manifest.LastWriteTimeUtc;
	}

	private static LoadedPlugin EnsureLoaded(EditorPluginManifest manifest) {
		lock (s_lock) {
			if (s_loaded.TryGetValue(manifest.Id, out var existing))
				return existing;
		}

		var path = ResolveNewestAssembly(manifest.ResolvedAssemblyPath);
		if (!File.Exists(path))
			throw new FileNotFoundException(
				$"Godonia plugin DLL not found: {manifest.ResolvedAssemblyPath}. Build the editor plugin project first.",
				manifest.ResolvedAssemblyPath);

		var alc = new EditorPluginLoadContext(path);
		alc.LoadPluginFromPath(path);
		manifest.LastWriteTimeUtc = File.GetLastWriteTimeUtc(path);

		var loaded = new LoadedPlugin(manifest, alc, path);
		lock (s_lock)
			s_loaded[manifest.Id] = loaded;

		EditorLogHub.Write($"Loaded plugin '{manifest.Id}' from {Path.GetFileName(path)}");
		return loaded;
	}

	private static void DetachHosts(string id) {
		IAvaloniaEditorHost[] hosts;
		lock (s_lock)
			hosts = s_hosts.Where(h => string.Equals(h.PluginId, id, StringComparison.OrdinalIgnoreCase)).ToArray();

		foreach (var host in hosts)
			host.Control = null;
	}

	private static void AssignPages(string id) {
		IAvaloniaEditorHost[] hosts;
		lock (s_lock)
			hosts = s_hosts.Where(h => string.Equals(h.PluginId, id, StringComparison.OrdinalIgnoreCase)).ToArray();

		foreach (var host in hosts)
			ShowPage(host);
	}

	private static void UnloadPlugin(string id, bool recreate) {
		LoadedPlugin? loaded;
		lock (s_lock) {
			if (!s_loaded.TryGetValue(id, out loaded))
				return;
			s_loaded.Remove(id);
		}

		try {
			PrepareUnload(loaded.LoadContext);
			loaded.LoadContext.Unload();
		}
		catch (Exception ex) {
			GD.PrintErr($"Godonia: error unloading plugin '{id}': {ex.Message}");
		}

		WaitForUnload(loaded.AlcWeak, id);

		if (recreate && TryGetManifest(id, out var manifest)) {
			try {
				EnsureLoaded(manifest);
			}
			catch (Exception ex) {
				GD.PrintErr($"Godonia: failed to reload plugin '{id}': {ex.Message}");
			}
		}
	}

	private static void PrepareUnload(AssemblyLoadContext alc) {
		Assembly[] assemblies;
		try {
			assemblies = alc.Assemblies.ToArray();
		}
		catch {
			return;
		}

		foreach (var assembly in assemblies) {
			try {
				AvaloniaPropertyRegistry.Instance.UnregisterByModule(assembly.GetTypes());
			}
			catch (ReflectionTypeLoadException ex) {
				AvaloniaPropertyRegistry.Instance.UnregisterByModule(ex.Types.OfType<Type>());
			}
			catch (Exception ex) {
				GD.PrintErr($"Godonia: UnregisterByModule failed for {assembly.GetName().Name}: {ex.Message}");
			}

			RemoveStylesFrom(assembly);
			AssetLoader.InvalidateAssemblyCache(assembly.GetName().Name ?? assembly.FullName ?? "");
		}

		GodotPlatform.EnsureAssetLoader(typeof(EditorAvaloniaApp).Assembly);
	}

	private static void RemoveStylesFrom(Assembly assembly) {
		if (Application.Current is not { } app)
			return;

		for (var i = app.Styles.Count - 1; i >= 0; i--) {
			if (app.Styles[i].GetType().Assembly == assembly)
				app.Styles.RemoveAt(i);
		}
	}

	private static void WaitForUnload(WeakReference alcWeak, string id) {
		for (var i = 0; i < 3 && alcWeak.IsAlive; i++) {
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}

		if (alcWeak.IsAlive)
			GD.Print($"Godonia: plugin ALC for '{id}' was not collected. The page was still replaced; Godot C# scripts remain usable.");
	}

	private static bool TryGetManifest(string id, out EditorPluginManifest manifest) {
		lock (s_lock)
			return s_manifests.TryGetValue(id, out manifest!);
	}

	private static void HookGameAssemblyUnload(Assembly assembly) {
		var alc = AssemblyLoadContext.GetLoadContext(assembly);
		if (alc is null || !alc.IsCollectible)
			return;
		if (ReferenceEquals(s_hookedGameAlc, alc))
			return;

		s_hookedGameAlc = alc;
		alc.Unloading += OnGameAlcUnloading;
	}

	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	private static void OnGameAlcUnloading(AssemblyLoadContext alc) {
		s_hookedGameAlc = null;
		GodotAvalonia.ReleaseGameReferences();
		UnregisterAvaloniaFor(alc);
	}

	private static void UnregisterAvaloniaFor(AssemblyLoadContext alc) {
		Assembly[] assemblies;
		try {
			assemblies = alc.Assemblies.ToArray();
		}
		catch {
			return;
		}

		foreach (var assembly in assemblies) {
			try {
				AvaloniaPropertyRegistry.Instance.UnregisterByModule(assembly.GetTypes());
			}
			catch (ReflectionTypeLoadException ex) {
				AvaloniaPropertyRegistry.Instance.UnregisterByModule(ex.Types.OfType<Type>());
			}
			catch (Exception ex) {
				GD.PrintErr($"Godonia: UnregisterByModule failed for {assembly.GetName().Name}: {ex.Message}");
			}

			RemoveStylesFrom(assembly);
			AssetLoader.InvalidateAssemblyCache(assembly.GetName().Name ?? assembly.FullName ?? "");
		}

		GodotPlatform.EnsureAssetLoader(typeof(EditorAvaloniaApp).Assembly);
	}

	/// <summary>
	/// Picks the newest <c>Name.dll</c> or <c>Name.&lt;ticks&gt;.dll</c> so a rebuild can
	/// write a new file when Godot still has the previous copy open.
	/// </summary>
	internal static string ResolveNewestAssembly(string configuredPath) {
		var dir = Path.GetDirectoryName(configuredPath);
		if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
			return configuredPath;

		var name = Path.GetFileNameWithoutExtension(configuredPath);
		var ext = Path.GetExtension(configuredPath);
		string? newest = null;
		var newestTime = DateTime.MinValue;

		foreach (var file in Directory.EnumerateFiles(dir, name + "*" + ext)) {
			if (!IsCanonicalOrShadowCopy(name, ext, Path.GetFileName(file)))
				continue;

			var time = File.GetLastWriteTimeUtc(file);
			if (time < newestTime)
				continue;

			newestTime = time;
			newest = file;
		}

		return newest ?? configuredPath;
	}

	private static bool IsCanonicalOrShadowCopy(string assemblyName, string extension, string fileName) {
		if (fileName.Equals(assemblyName + extension, StringComparison.OrdinalIgnoreCase))
			return true;

		var prefix = assemblyName + ".";
		if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
			|| !fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
			return false;

		var middleLength = fileName.Length - prefix.Length - extension.Length;
		if (middleLength <= 0)
			return false;

		for (var i = 0; i < middleLength; i++) {
			if (!char.IsDigit(fileName[prefix.Length + i]))
				return false;
		}

		return true;
	}

}
