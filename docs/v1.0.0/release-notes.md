# Release notes

## 1.1.0

- **Host scripts:** `dotnet add package Ouse.Estragonia` followed by `dotnet build` copies `AvaloniaControl.cs` / `UiHost.cs` into a Godot project when they are missing. Existing files are never overwritten. Opt out with `EstragoniaInjectHostScripts=false`.
- **Editor plugins:** process-wide `EnsureStarted` / `EditorAvaloniaApp`, collectible ALC catalog (`IEditorPage`, `EditorPluginCatalog`), and HelloWorld docks that reload without shutting Avalonia down. See [Editor plugins](editor-plugins.md).
- Host script source of truth is `src/JLeb.Estragonia/host-scripts/`. Package upgrades do not patch already-copied files; merge from that folder if a host API changes.

## 1.0.1

- **Breaking for package consumers:** `AvaloniaControl` / `UiHost` are no longer Godot node types inside `Ouse.Estragonia`. Copy them from the template/sample into your Godot project (or use `dotnet new estragonia`). Logic lives in `AvaloniaControlEngine` in the NuGet package.
- Avoids Godot ScriptTypeBiMap duplicate-key errors when hot-reloading with host types in an external assembly.

## 1.0.0

Initial release of this maintained fork:

- Avalonia 12 / Godot 4.7 / .NET 10
- Central Package Management (`Directory.Packages.props`)
- Input pass-through, Avalonia 12 dispatcher / asset-loader fixes
- DocFX documentation + GitHub Pages workflow
- `dotnet new estragonia` project template
- NuGet: [Ouse.Estragonia](https://www.nuget.org/packages/Ouse.Estragonia/) + [Ouse.Estragonia.Templates](https://www.nuget.org/packages/Ouse.Estragonia.Templates/)
- Source: [0use-TE/Estragonia](https://github.com/0use-TE/Estragonia)

Based on [MrJul/Estragonia](https://github.com/MrJul/Estragonia) (MIT). Upstream package id remains `JLeb.Estragonia`.
