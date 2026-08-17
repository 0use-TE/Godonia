# Editor plugins

Godonia can host Avalonia UI in Godot **docks** and **MainScreen** tabs. The goal is: **change a plugin view in the IDE, return to Godot, and see the new page** — without Godot unloading the game assembly, and without restarting Avalonia.

New projects created with the Godonia editor include a **`godonia_new_plugin`** host plus a wizard dock to scaffold pages. HelloWorld shows the same pattern manually.

## Constraints

| Item | Rule |
|------|------|
| Godot scripts | `EditorPlugin`, `AvaloniaControl`, `AvaloniaEditorHost` live in the Godot project. Class name = file name. `#if TOOLS` + `[Tool]`. |
| Plugin views | Separate `Microsoft.NET.Sdk` csproj. Reference **Ouse.Godonia** only. **Do not** add `Avalonia.Desktop` / Fluent (Preview owns those). **Do not** `ProjectReference` the plugin from the Godot project. **Do not** use Godot.NET.Sdk. **No** `GodotObject` types in the plugin assembly. |
| Reload | Collectible ALC + `host.Control = new page`. Not a Godot Build. |
| Avalonia | `SetupWithoutStarting` once per editor process. Never `Shutdown`. |
| Plugins | Each plugin is a thin `EditorPlugin`. Do not introduce an abstract `EditorPlugin` base. |

Plugin sources must sit **outside** the Godot project tree (or be `Compile Remove`d). Otherwise focusing Godot rebuilds the game project and you hit [godot#78513](https://github.com/godotengine/godot/issues/78513).

## Layout (HelloWorld sample)

```
samples/
  HelloWorld/                              Godot project
    addons/godonia_new_plugin/             host + wizard + pages/*.json
    AvaloniaControl.cs / UiHost.cs / AvaloniaEditorHost.cs
  HelloWorld.Editor.SkillTree/             Left dock — skill tree
  HelloWorld.Editor.QuestManager/          Bottom dock — quest list
  HelloWorld.Editor.WorldManager/          MainScreen — region streaming tool
  HelloWorld.Editor.Preview/               F5 desktop tabs for all three
```

HelloWorld ships three page manifests under `addons/godonia_new_plugin/pages/`. New game projects use the same host; the in-editor wizard can scaffold more.

`plugin.json` / pages manifest example:

```json
{
  "id": "mygame.inspector",
  "assembly": ".godot/godonia-plugins/MyGame.Editor.Inspector.dll",
  "pageType": "MyGame.Editor.Inspector.InspectorPage",
  "dockSlot": "LeftUL"
}
```

`dockSlot` may be a dock position (`LeftUL`, `RightUL`, …) or **`MainScreen`** (top editor button / tab).

The Godot project must **not** reference the editor csproj. The solution still can, so you can build the plugin from the IDE.

`AvaloniaEditorHost` lives at the Godot project root and is shared — do not copy it into each addon.

## Wizard (scaffolder)

With `godonia_new_plugin` enabled:

1. Use the **Godonia** wizard dock to create a page (name, dock slot / MainScreen).
2. The scaffolder creates a sibling `{Game}.Editor.{Name}` project, a manifest under `addons/godonia_new_plugin/pages/`, and wires **Preview** `ProjectReference` when requested.
3. Build the editor project so the DLL lands under `.godot/godonia-plugins/`.
4. Focus Godot (or reload) so the host picks up the new manifest.

## Preview project

`{Game}.Editor.Preview` is a **WinExe** Avalonia desktop app (not Godot.NET.Sdk). Set it as the solution startup project and press F5 to preview dock / MainScreen pages without opening Godot.

- Plugin projects do **not** reference `Avalonia.Desktop`; do not open the XAML designer on those projects alone.
- Generated views include `mc:Ignorable="d"` design size attributes for the Preview designer.

## Workflow

1. Set **`{Game}.Editor.Preview`** as the solution startup project and press F5 to preview pages.
2. Open the Godot project in the **Godonia** editor. Enable `godonia_new_plugin` if needed.
3. Build editor plugin projects once so DLLs exist under `.godot/godonia-plugins/` (building Preview builds them too).
4. Change a view `.axaml`, **build only that editor project**. If Godot still has the previous DLL open, the build writes a timestamped copy instead of failing; focus Godot to load it.
5. Focus Godot or use the host reload path.
6. Only the changed plugin page updates.

Do not press Godot’s build hammer to refresh plugin UI. Changing thin `EditorPlugin` / `AvaloniaEditorHost` scripts still requires a Godot C# build.

## API

- `AvaloniaEditorRuntime.EnsureStarted()` — process-wide `EditorAvaloniaApp` (Fluent dark). No-op the second time.
- `EditorPluginCatalog.RegisterManifest(path)` — load manifest JSON.
- `EditorPluginCatalog.Reload(id)` / `ReloadChanged()` / `UnloadAll()`.
- `IEditorPage` — implement in the plugin assembly; `Create()` returns a `UserControl`.
- `EditorPluginScaffolder` — creates sibling editor projects + manifests (used by the wizard).
- `EditorLogHub` — framework-owned log buffer. Plugins should snapshot it, not subscribe to their own static events.

`AvaloniaEditorHost` (in-project `[Tool]` script) sets `PluginId` and registers itself with the catalog. `Detach()` clears the Avalonia page and keeps the Godot node / TopLevel.

## What this is not

- Avalonia XAML Hot Reload (optional later; assembly swap is the reload).
- Putting `EditorPlugin` in a class library.
- Guaranteeing every complex visual tree is 100% GC’d. Reload still replaces the page; a warning is printed if the ALC stays alive.
