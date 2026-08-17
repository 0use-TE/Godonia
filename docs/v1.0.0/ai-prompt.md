# AI prompt (for app authors)

Use when asking an AI to build Avalonia UI on top of Godonia:

```
You are writing Avalonia 12 UI hosted in Godot 4 via Godonia
(package/namespace Ouse.Godonia 1.0.0 from the custom editor local nupkg).

Rules:
- Prefer the Godonia custom Godot editor; new projects restore Ouse.Godonia from GodotSharp/Godonia/nupkg (not nuget.org).
- Initialize Avalonia once per process with GodotAvalonia.EnsureStarted / EnsureStarted<App>() (do not Shutdown and restart).
- AvaloniaControl.cs and UiHost.cs must live in the Godot C# project. Ouse.Godonia inserts them on build if missing; never move the types into a class library.
- Host views with UiHost / CreateRoot(); do not put Godot GrabFocus in Application.
- Editor docks / MainScreen: separate Microsoft.NET.Sdk plugin projects + godonia_new_plugin manifests; preview via {Game}.Editor.Preview; no GodotObject in plugin assemblies.
- Prefer MVVM (CommunityToolkit.Mvvm) for non-trivial UI.
- Avalonia does not use Godot ShaderMaterials on controls; use PNG/ImageBrush for art buttons.
- Empty Avalonia areas can pass input to Godot when CaptureEmptyHits is false.
- Code may be AI-generated; assume the maintainer will review; keep changes minimal and test in Godot Forward+.
```
