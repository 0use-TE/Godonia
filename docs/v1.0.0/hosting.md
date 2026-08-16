# Hosting UI

## Critical rule — host scripts stay in the Godot project

`AvaloniaControl` and `UiHost` are **Godot node scripts**. They **must** be `.cs` files inside your Godot C# project (same assembly as `project.godot`).

| Location | OK? |
|----------|-----|
| Your Godot project (`res://AvaloniaControl.cs`, `res://UiHost.cs`, …) | Yes |
| Copied from `templates/estragonia-godot` or `samples/HelloWorld` | Yes |
| Only referencing `Ouse.Estragonia` NuGet / library assembly | **No** |

Godot’s C# editor hot-reload **does not support** Godot node types that live only in an external assembly (NuGet / `ProjectReference`). That is an engine limitation ([godot#111881](https://github.com/godotengine/godot/issues/111881), [godot#98094](https://github.com/godotengine/godot/issues/98094)).

The NuGet package (`Ouse.Estragonia`) provides the platform bridge (`UseGodot`, Vulkan/Skia, `AvaloniaControlEngine`, …). The two host `.cs` files **must still compile into the Godot project assembly** (class name = file name). They can come from:

1. The template / HelloWorld sample (already present)
2. **Automatic insert** on `dotnet build` when the package is referenced and the files are missing
3. A manual copy from `host-scripts/` in the package

Inserted files are **project files** (commit them). Package upgrades **do not** overwrite them. If a host API changes, merge from `src/JLeb.Estragonia/host-scripts/` by hand.

Opt out:

```xml
<EstragoniaInjectHostScripts>false</EstragoniaInjectHostScripts>
```

Extension libraries that only reference `Ouse.Estragonia` must **not** pack or inject these two files; the game Godot project gets them via a direct or transitive reference. After insert, reload Godot so it can generate `.uid` files.

## Split responsibilities

| Concern | Where |
|---------|--------|
| `UseGodot` / asset loader / IME | Autoload (`AvaloniaLoader`) once |
| Rendering + input host | `AvaloniaControl.cs` **in your Godot project** |
| Focus / mouse filter / `CreateRoot` | `UiHost.cs` **in your Godot project** |
| Your scene script | `UserInterface : UiHost` → implement `CreateRoot()` |
| Theme / app resources | Avalonia `Application` |

Do **not** call `GrabFocus` / `GetWindow` from Avalonia `App`.

## Required project files

```
YourGodotProject/
├── AvaloniaControl.cs   ← template, auto-insert, or copy (required)
├── UiHost.cs            ← template, auto-insert, or copy (required)
├── AvaloniaLoader.cs    ← Autoload
├── UserInterface.cs     ← your host: CreateRoot()
├── App.axaml (+ .cs)
└── Views/ …
```

## UiHost

```csharp
// UiHost.cs — must be in the Godot project
namespace JLeb.Estragonia;

public abstract partial class UiHost : AvaloniaControl
{
    protected abstract Control CreateRoot();

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        Control = CreateRoot();
        base._Ready();
        GrabFocus();
    }
}
```

```csharp
// UserInterface.cs — your scene script
using JLeb.Estragonia;

public partial class UserInterface : UiHost
{
    protected override Control CreateRoot()
        => new MainView { DataContext = new MainViewModel() };
}
```

## Hit testing

By default `AvaloniaControl.CaptureEmptyHits` is `false`: only Avalonia-hittable pixels capture the mouse; empty areas pass through to Godot (e.g. a `Sprite2D` behind the host).

Set `CaptureEmptyHits = true` to capture the whole control rect.

Editor docks (collectible plugin ALCs, no Avalonia restart) are documented in [Editor plugins](editor-plugins.md).
