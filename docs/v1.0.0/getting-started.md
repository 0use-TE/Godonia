# Getting started

## Identity

| | |
|--|--|
| Product | **Godonia** |
| Package / namespace | **`Ouse.Godonia`** |
| Package version | **1.0.0** |
| Package feed | Editor-local: `GodotSharp/Godonia/nupkg` (not nuget.org) |

> **AI-assisted codebase.** Stability is **not** guaranteed. Test before shipping.  
> Based on [Estragonia](https://github.com/MrJul/Estragonia) by Julien Lebosquain (MIT).

## Requirements

- **Godonia custom Godot editor** (this repo: `godot/bin/godot.windows.editor.x86_64.mono.exe`)
- Renderer **Forward+** or **Mobile** (Vulkan)
- .NET SDK **10**
- Avalonia **12**

---

## Tutorial A — New project in the Godonia editor (recommended)

### 1. Run the custom editor

```text
godot/bin/godot.windows.editor.x86_64.mono.exe
```

Next to the exe you should have:

```text
GodotSharp/Godonia/
  runtime/          Ouse.Godonia.dll (editor host)
  nupkg/            Ouse.Godonia.1.0.0.nupkg
  project-template/ Avalonia starter copied into new projects
```

Rebuild / refresh those folders with:

```powershell
.\scripts\deploy-godonia-editor.ps1
```

### 2. Create a project

In the Project Manager, create a **new Godot C# project**. The editor installs the Godonia Avalonia template (nested workspace with `.slnx`, game project, and `{Game}.Editor.Preview`).

Already wired:

- Autoload `AvaloniaLoader` → `GodotAvalonia.EnsureStarted` / `UseGodot()` once
- **`AvaloniaControl.cs` + `UiHost.cs` in the Godot project** (required host scripts)
- `UserInterface : UiHost` → `CreateRoot()`
- `nuget.config` pointing at the editor-local `Godonia/nupkg` feed
- `{Game}.Editor.Preview` for Avalonia desktop designer / F5 preview of editor pages

### 3. Edit the game UI

- View: `Views/MainView.axaml`
- ViewModel: `ViewModels/MainViewModel.cs`
- Theme: `App.axaml`

Open `project.godot` with the **same** custom editor. Do **not** open the `.godot/` cache folder.

---

## Tutorial B — add the package to an existing Godot C# project

Point NuGet at the editor nupkg folder (absolute path), then:

```bash
dotnet add package Ouse.Godonia --version 1.0.0
dotnet build
```

`dotnet build` inserts `AvaloniaControl.cs` and `UiHost.cs` next to the csproj if they are missing (warning `GODONIA001` the first time). Reload Godot so it can generate `.uid` files.

The types still compile into **your Godot assembly**. Do not move them to a class library. To skip insert, set `GodoniaInjectHostScripts` to `false` and copy the files yourself from the package `host-scripts/` folder.

### 1. Avalonia `Application` + theme

Create `App.axaml` / `App.axaml.cs` with a theme (e.g. Semi or Fluent).

### 2. Autoload (once per process)

```csharp
using Godot;
using Ouse.Godonia;

public partial class AvaloniaLoader : Node
{
    public override void _Ready()
    {
        if (Engine.IsEditorHint())
            GodotAvalonia.EnsureStarted();
        else
            GodotAvalonia.EnsureStarted<App>();

        GetWindow()?.SetImeActive(true);
    }
}
```

Register it as an Autoload in `project.godot`. `EnsureStarted` is a no-op if Avalonia is already running — do not `Shutdown` and start again.

### 3. Scene host script

```csharp
using Avalonia.Controls;
using Ouse.Godonia;

public partial class UserInterface : UiHost
{
    protected override Control CreateRoot()
        => new MainView { DataContext = new MainViewModel() };
}
```

Attach `UserInterface.cs` to a full-rect `Control` in your main scene.

See [Hosting UI](hosting.md) for the full file checklist.

---

## Sample in this repo

Open `samples/HelloWorld` in the Godonia editor (project reference to the library source).  
That sample already contains `AvaloniaControl.cs` and `UiHost.cs`, plus `godonia_new_plugin` with three editor pages: **Skill Tree** (left), **Quest Manager** (bottom), **World Manager** (MainScreen). Set `HelloWorld.Editor.Preview` as the startup project for a desktop tab preview; see [Editor plugins](editor-plugins.md).

## Hot reload

`AvaloniaControl` / `UiHost` live in the Godot project so Godot can reload them as normal scripts.

You may still see **Failed to unload assemblies** when Avalonia (or other libraries) keep references across rebuilds. If the editor gets stuck: fully restart Godot; if needed, delete `.godot` and reopen.

## Disclaimer

This tree contains AI-assisted changes. Validate before shipping.
