# 更新说明

## 1.0.0（Godonia）

以 **Godonia** 产品名发布的首个版本（Estragonia 分支）：

- 包名 / 命名空间 **`Ouse.Godonia`**，版本 **1.0.0**
- 默认从**自定义编辑器**旁的 `GodotSharp/Godonia/nupkg` 还原（默认不发布到 nuget.org）
- Avalonia 12 / Godot 4.7 / .NET 10
- 宿主脚本（`AvaloniaControl` / `UiHost`）放在 Godot 工程内；缺失时可在 `dotnet build` 时插入（`GodoniaInjectHostScripts`）
- 编辑器插件：进程级 `EnsureStarted` / 可收集 ALC 目录（`IEditorPage`、`EditorPluginCatalog`）
- `godonia_new_plugin` 宿主 + 向导脚手架（Dock + MainScreen）
- 工程模板含 `{Game}.Editor.Preview` 桌面预览工程
- 新建工作区使用 **`.slnx`**
- DocFX 文档站点

基于 [MrJul/Estragonia](https://github.com/MrJul/Estragonia)（MIT）。保留 Julien Lebosquain 署名。
