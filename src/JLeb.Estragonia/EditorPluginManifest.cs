using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JLeb.Estragonia;

/// <summary>JSON manifest for an editor plugin page (read from the Godot project, never references plugin types).</summary>
public sealed class EditorPluginManifest {

	[JsonPropertyName("id")]
	public string Id { get; set; } = "";

	[JsonPropertyName("assembly")]
	public string Assembly { get; set; } = "";

	[JsonPropertyName("pageType")]
	public string PageType { get; set; } = "";

	internal string ResolvedAssemblyPath { get; set; } = "";

	internal DateTime LastWriteTimeUtc { get; set; }

}

internal sealed class LoadedPlugin {

	public EditorPluginManifest Manifest { get; }
	public EditorPluginLoadContext LoadContext { get; }
	public WeakReference AlcWeak { get; }

	public LoadedPlugin(EditorPluginManifest manifest, EditorPluginLoadContext loadContext) {
		Manifest = manifest;
		LoadContext = loadContext;
		AlcWeak = new WeakReference(loadContext);
	}

}
