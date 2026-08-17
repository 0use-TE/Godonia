# Release notes

## 1.0.0 (Godonia)

First release under the **Godonia** product name (fork of Estragonia):

- Package / namespace **`Ouse.Godonia`** at version **1.0.0**
- Restored from the **custom editor** feed `GodotSharp/Godonia/nupkg` (unpublished by default)
- Avalonia 12 / Godot 4.7 / .NET 10
- Host scripts (`AvaloniaControl` / `UiHost`) live in the Godot project; `dotnet build` can insert them when missing (`GodoniaInjectHostScripts`)
- Editor plugins: process-wide `EnsureStarted` / collectible ALC catalog (`IEditorPage`, `EditorPluginCatalog`)
- `godonia_new_plugin` host + wizard scaffolder (docks + MainScreen)
- `{Game}.Editor.Preview` desktop preview project in the project template
- Solution format **`.slnx`** for new workspaces
- DocFX documentation site

Based on [MrJul/Estragonia](https://github.com/MrJul/Estragonia) (MIT). Author attribution for Julien Lebosquain is retained.
