using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Ouse.Godonia;

/// <summary>
/// Collectible ALC for one editor plugin assembly. Shared framework assemblies
/// (Avalonia, Godonia, GodotSharp) resolve to the already-loaded context.
/// Assemblies are loaded from memory so the DLL on disk is not locked (rebuild while Godot is open).
/// </summary>
public sealed class EditorPluginLoadContext : AssemblyLoadContext {

	private readonly AssemblyLoadContext _parent;
	private readonly string _pluginDirectory;
	private readonly AssemblyDependencyResolver _resolver;

	public EditorPluginLoadContext(string pluginAssemblyPath)
		: base(name: "GodoniaPlugin:" + CanonicalAssemblyName(pluginAssemblyPath), isCollectible: true) {
		_pluginDirectory = Path.GetDirectoryName(pluginAssemblyPath)
			?? throw new ArgumentException("Plugin assembly path has no directory.", nameof(pluginAssemblyPath));
		_resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
		_parent = AssemblyLoadContext.GetLoadContext(typeof(EditorPluginCatalog).Assembly)
			?? Default;
	}

	/// <summary>Reads the PE (and PDB if present) then loads from memory so the file is not kept open.</summary>
	public Assembly LoadPluginFromPath(string assemblyPath)
		=> LoadUnlocked(assemblyPath);

	protected override Assembly? Load(AssemblyName assemblyName) {
		if (assemblyName.Name is null)
			return null;

		if (TryGetLoaded(_parent, assemblyName) is { } fromParent)
			return fromParent;

		if (!ReferenceEquals(_parent, Default) && TryGetLoaded(Default, assemblyName) is { } fromDefault)
			return fromDefault;

		if (TryGetLoadedAnywhere(assemblyName) is { } already)
			return already;

		if (IsSharedFramework(assemblyName.Name))
			return null;

		var resolved = _resolver.ResolveAssemblyToPath(assemblyName);
		if (resolved is not null && File.Exists(resolved))
			return LoadUnlocked(resolved);

		foreach (var candidate in ProbePaths(assemblyName.Name)) {
			if (File.Exists(candidate))
				return LoadUnlocked(candidate);
		}

		return null;
	}

	protected override IntPtr LoadUnmanagedDll(string unmanagedDllName) {
		var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
		if (path is not null)
			return LoadUnmanagedDllFromPath(path);

		return IntPtr.Zero;
	}

	private IEnumerable<string> ProbePaths(string simpleName) {
		var fileName = simpleName + ".dll";
		yield return Path.Combine(_pluginDirectory, fileName);

		var dotGodot = Path.GetDirectoryName(_pluginDirectory);
		if (dotGodot is null)
			yield break;

		yield return Path.Combine(dotGodot, "mono", "temp", "bin", "Debug", fileName);
		yield return Path.Combine(dotGodot, "mono", "temp", "bin", "Release", fileName);
	}

	private Assembly LoadUnlocked(string assemblyPath) {
		using var pe = ReadShared(assemblyPath);
		var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
		using var pdb = File.Exists(pdbPath) ? ReadShared(pdbPath) : null;
		return LoadFromStream(pe, pdb);
	}

	private static MemoryStream ReadShared(string path) {
		using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		var memory = new MemoryStream();
		file.CopyTo(memory);
		memory.Position = 0;
		return memory;
	}

	private static Assembly? TryGetLoadedAnywhere(AssemblyName requested) {
		foreach (var alc in All) {
			if (TryGetLoaded(alc, requested) is { } found)
				return found;
		}

		return null;
	}

	private static Assembly? TryGetLoaded(AssemblyLoadContext context, AssemblyName requested) {
		foreach (var assembly in context.Assemblies) {
			var loaded = assembly.GetName();
			if (string.Equals(loaded.Name, requested.Name, StringComparison.OrdinalIgnoreCase))
				return assembly;
		}

		return null;
	}

	private static bool IsSharedFramework(string simpleName)
		=> simpleName.Equals("Ouse.Godonia", StringComparison.OrdinalIgnoreCase)
			|| simpleName.Equals("GodotSharp", StringComparison.OrdinalIgnoreCase)
			|| simpleName.Equals("GodotSharpEditor", StringComparison.OrdinalIgnoreCase)
			|| simpleName.Equals("MicroCom.Runtime", StringComparison.OrdinalIgnoreCase)
			|| simpleName.StartsWith("Avalonia", StringComparison.OrdinalIgnoreCase)
			|| simpleName.StartsWith("SkiaSharp", StringComparison.OrdinalIgnoreCase)
			|| simpleName.StartsWith("HarfBuzzSharp", StringComparison.OrdinalIgnoreCase);

	private static string CanonicalAssemblyName(string pluginAssemblyPath) {
		var name = Path.GetFileNameWithoutExtension(pluginAssemblyPath);
		var dot = name.LastIndexOf('.');
		if (dot <= 0)
			return name;

		var suffix = name.AsSpan(dot + 1);
		if (suffix.IsEmpty)
			return name;

		foreach (var c in suffix) {
			if (!char.IsDigit(c))
				return name;
		}

		return name[..dot];
	}

}
