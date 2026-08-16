# Getting started

## Links

| | URL |
|--|-----|
| Library | [Ouse.Estragonia](https://www.nuget.org/packages/Ouse.Estragonia/) |
| Template | [Ouse.Estragonia.Templates](https://www.nuget.org/packages/Ouse.Estragonia.Templates/) |
| Source | [0use-TE/Estragonia](https://github.com/0use-TE/Estragonia) |

Package id is **`Ouse.Estragonia`**; bridge namespaces remain **`JLeb.Estragonia`**.

## Requirements

- Godot **4.7+** (.NET build), renderer **Forward+** or **Mobile** (Vulkan)
- .NET SDK **10**
- Avalonia **12**

---

## Tutorial A — `dotnet new` template (recommended)

### 1. Install the template

```bash
dotnet new install Ouse.Estragonia.Templates
```

### 2. Create a project

```bash
dotnet new estragonia -n MySolution --GodotProjectName MyGame -o MySolution
cd MySolution
dotnet restore
```

| Flag | Meaning |
|------|---------|
| `-n` / `--name` | Solution name |
| `--GodotProjectName` | Godot / C# project name (valid C# identifier) |
| `-o` | Output folder |

Visual Studio: **Create a new project** → **Estragonia Godot App** (restart VS after installing the template).

### 3. Open in Godot

Open **`project.godot`** at the solution root with Godot 4.7+ (.NET).  
Do **not** open the `.godot/` cache folder.

Already wired:

- Autoload `AvaloniaLoader` → `UseGodot()` once
- **`AvaloniaControl.cs` + `UiHost.cs` in the Godot project** (required host scripts)
- `UserInterface` : `UiHost` → `CreateRoot()`
- `Designer.cs` for Avalonia XAML preview (`Main` + `BuildAvaloniaApp`)

### 4. Edit the UI

- View: `Views/MainView.axaml`
- ViewModel: `ViewModels/MainViewModel.cs`
- Theme: `App.axaml`

---

## Tutorial B — add the package to an existing Godot C# project

```bash
dotnet add package Ouse.Estragonia
dotnet add package Semi.Avalonia
dotnet build
```

`dotnet build` inserts `AvaloniaControl.cs` and `UiHost.cs` next to the csproj if they are missing (warning `ESTRAGONIA001` the first time). Reload Godot so it can generate `.uid` files.

The types still compile into **your Godot assembly**. Do not move them to a class library. To skip insert, set `EstragoniaInjectHostScripts` to `false` and copy the files yourself from the package `host-scripts/` folder.

### 1. Avalonia `Application` + theme

Create `App.axaml` / `App.axaml.cs` with a theme (e.g. Semi).

### 2. Autoload (once per process)

```csharp
using Godot;
using JLeb.Estragonia;

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
using JLeb.Estragonia;

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

Open `samples/HelloWorld` in Godot (uses a project reference to the library source).  
That sample already contains `AvaloniaControl.cs` and `UiHost.cs`. Editor docks: set `HelloWorld.Editor.Preview` as the startup project for a desktop preview; in Godot, build `HelloWorld.Editor.Demo` / `HelloWorld.Editor.Log`, then see [Editor plugins](editor-plugins.md).

## Hot reload

`AvaloniaControl` / `UiHost` now live in the Godot project so Godot can reload them as normal scripts.

You may still see **Failed to unload assemblies** when Avalonia (or other libraries) keep references across rebuilds. If the editor gets stuck: fully restart Godot; if needed, delete `.godot` and reopen.

## Disclaimer

This tree contains AI-assisted changes. Validate before shipping.
