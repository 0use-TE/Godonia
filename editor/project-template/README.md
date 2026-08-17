# GodoniaApp

Godot 4 + Avalonia starter from **[Ouse.Godonia.Templates](https://www.nuget.org/packages/Ouse.Godonia.Templates/)**.

- Library: [Ouse.Godonia](https://www.nuget.org/packages/Ouse.Godonia/)
- Docs / source: [0use-TE/Godonia](https://github.com/0use-TE/Godonia)

## Open in Godot

1. Install [.NET 10 SDK](https://dotnet.microsoft.com/download) and **Godot 4.7+ (.NET)**.
2. Run `dotnet restore` in this folder.
3. Open **`GodotGame/project.godot`** with Godot (folder name matches `--GodotProjectName`).

Autoload `AvaloniaLoader` and the default `UserInterface` (`UiHost`) are already configured.

**`GodotGame/UI/AvaloniaControl.cs` and `GodotGame/UI/UiHost.cs` must stay in the Godot project.**  
`Ouse.Godonia` can insert them on build if they are missing; they still compile into this assembly.

The `.sln`, git files, and NuGet config sit in this folder. The Godot project is the inner `GodotGame/` directory so editor plugin projects can live as siblings (`GodotGame.Editor.Inspector`, …).

## Layout

| Path | Role |
|------|------|
| `GodoniaApp.sln` | Solution (parent of the Godot project) |
| `GodotGame/project.godot` | Open this in Godot |
| `GodotGame/UI/Views/` / `ViewModels/` | Avalonia MVVM UI |
| `GodotGame/UI/AvaloniaControl.cs` | Godot host — renders Avalonia |
| `GodotGame/UI/UiHost.cs` | Godot host — focus + `CreateRoot()` |
| `GodotGame/UI/AvaloniaLoader.cs` | Autoload — `UseGodot()` once |
| `GodotGame/UI/UserInterface.cs` | Your `UiHost` — implement `CreateRoot()` |
| `Directory.Packages.props` | NuGet versions (shared with editor plugins) |

Edit `GodotGame/UI/Views/MainView.axaml` and `GodotGame/UI/ViewModels/MainViewModel.cs` to build your UI.

## Visual Studio / previewer

- Install the **Avalonia for Visual Studio** extension.
- `GodotGame/UI/Designer.cs` provides `Main` + `BuildAvaloniaApp` (Debug `OutputType=Exe`) so AXAML preview works.
  (Keep that file free of C# `#if …` directives — `dotnet new` would strip them and leave an empty file.)
- Runtime still runs inside Godot via `project.godot` — do not start the C# project as a normal console app.
