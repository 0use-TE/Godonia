# Editor plugins

Estragonia can host Avalonia UI in Godot editor docks. The goal is: **change a plugin view in the IDE, return to Godot, and see the new page** — without Godot unloading the game assembly, and without restarting Avalonia.

## Constraints

| Item | Rule |
|------|------|
| Godot scripts | `EditorPlugin`, `AvaloniaControl`, `AvaloniaEditorHost` live in the Godot project. Class name = file name. `#if TOOLS` + `[Tool]`. |
| Plugin views | Separate `Microsoft.NET.Sdk` csproj. Reference Estragonia only. **Do not** add `Avalonia.Desktop` / Fluent (Preview owns those). **Do not** `ProjectReference` it from the Godot project. **Do not** use Godot.NET.Sdk. **No** `GodotObject` types in the plugin assembly. |
| Reload | Collectible ALC + `host.Control = new page`. Not a Godot Build. |
| Avalonia | `SetupWithoutStarting` once per editor process. Never `Shutdown`. |
| Plugins | Each plugin is a thin `EditorPlugin`. Do not introduce an abstract `EditorPlugin` base. |

Plugin sources must sit **outside** the Godot project tree (or be `Compile Remove`d). Otherwise focusing Godot rebuilds the game project and you hit [godot#78513](https://github.com/godotengine/godot/issues/78513).

## Layout (HelloWorld sample)

```
samples/
  HelloWorld/                         Godot project
    addons/estragonia_editor/         thin shell only: plugin.cfg + plugin.json + EditorPlugin
    addons/estragonia_editor_log/
    AvaloniaControl.cs / UiHost.cs / AvaloniaEditorHost.cs
  HelloWorld.Editor.Demo/             plugin views; output copied to .godot/estragonia-plugins/
  HelloWorld.Editor.Log/
  HelloWorld.Editor.Preview/          solution startup project: desktop preview of both docks (no Godot)
```

`plugin.json` example:

```json
{
  "id": "estragonia.demo",
  "assembly": ".godot/estragonia-plugins/HelloWorld.Editor.Demo.dll",
  "pageType": "HelloWorld.Editor.Demo.DemoPage"
}
```

The Godot project must **not** reference the editor csproj. The solution still can, so you can build the plugin from the IDE.

Do **not** delete the thin `EditorPlugin` scripts under `addons/`: Godot requires `plugin.cfg` to point at a script in the game assembly (class name = file name). Views / `IEditorPage` do not live in addons. `AvaloniaEditorHost` lives at the Godot project root and is shared by both docks — do not copy it into each addon.

## Workflow

1. Set **HelloWorld.Editor.Preview** as the solution startup project and press F5 to preview Demo / Log. Plugin projects do not reference desktop Avalonia; do not use the XAML designer on those projects.
2. Open `samples/HelloWorld` in Godot 4.7+ (.NET). Enable the two Estragonia editor plugins if needed.
3. Build `HelloWorld.Editor.Demo` (and Log) once so DLLs exist under `.godot/estragonia-plugins/` (building Preview builds them too).
4. Change `DemoView.axaml`, **build only that editor project**. If Godot still has the previous DLL open, the build writes a timestamped copy instead of failing; focus Godot to load it.
5. Focus Godot or click **Reload this plugin**.
6. The left dock updates; the log dock is left alone if its DLL did not change.

Do not press Godot’s build hammer to refresh plugin UI. Changing `EstragoniaEditorPlugin.cs` / `AvaloniaEditorHost.cs` still requires a Godot C# build.

## API

- `AvaloniaEditorRuntime.EnsureStarted()` — process-wide `EditorAvaloniaApp` (Fluent dark). No-op the second time.
- `EditorPluginCatalog.RegisterManifest(path)` — load `plugin.json`.
- `EditorPluginCatalog.Reload(id)` / `ReloadChanged()` / `UnloadAll()`.
- `IEditorPage` — implement in the plugin assembly; `Create()` returns a `UserControl`.
- `EditorLogHub` — framework-owned log buffer. Plugins should snapshot it, not subscribe to their own static events.

`AvaloniaEditorHost` (in-project `[Tool]` script) sets `PluginId` and registers itself with the catalog. `Detach()` clears the Avalonia page and keeps the Godot node / TopLevel.

## What this is not

- Avalonia XAML Hot Reload (optional later; assembly swap is the reload).
- Putting `EditorPlugin` in a class library.
- Guaranteeing every complex visual tree is 100% GC’d. Reload still replaces the page; a warning is printed if the ALC stays alive.
