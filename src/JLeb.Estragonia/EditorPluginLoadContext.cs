using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace JLeb.Estragonia;

/// <summary>
/// Collectible ALC for one editor plugin assembly. Shared framework assemblies
/// (Avalonia, Estragonia, GodotSharp) resolve to the already-loaded context.
/// Assemblies are loaded from memory so the DLL on disk is not locked (rebuild while Godot is open).
/// </summary>
public sealed class EditorPluginLoadContext : AssemblyLoadContext {

	private readonly AssemblyLoadContext _parent;
	private readonly string _pluginDirectory;
	private readonly AssemblyDependencyResolver _resolver;

	public EditorPluginLoadContext(string pluginAssemblyPath)
		: base(name: "EstragoniaPlugin:" + CanonicalAssemblyName(pluginAssemblyPath), isCollectible: true) {
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

		var resolved = _resolver.ResolveAssemblyToPath(assemblyName);
		if (resolved is not null && File.Exists(resolved))
			return LoadUnlocked(resolved);

		var sibling = Path.Combine(_pluginDirectory, assemblyName.Name + ".dll");
		if (File.Exists(sibling))
			return LoadUnlocked(sibling);

		return null;
	}

	protected override IntPtr LoadUnmanagedDll(string unmanagedDllName) {
		var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
		if (path is not null)
			return LoadUnmanagedDllFromPath(path);

		return IntPtr.Zero;
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

	private static Assembly? TryGetLoaded(AssemblyLoadContext context, AssemblyName requested) {
		foreach (var assembly in context.Assemblies) {
			var loaded = assembly.GetName();
			if (string.Equals(loaded.Name, requested.Name, StringComparison.OrdinalIgnoreCase))
				return assembly;
		}

		return null;
	}

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
