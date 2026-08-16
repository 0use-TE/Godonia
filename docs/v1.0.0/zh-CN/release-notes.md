# 更新说明

## 1.1.0

- **宿主脚本：** Godot 工程 `dotnet add package Ouse.Estragonia` 后执行 `dotnet build`，若缺少 `AvaloniaControl.cs` / `UiHost.cs` 会自动插入。已有文件永不覆盖。可用 `EstragoniaInjectHostScripts=false` 关闭。
- **编辑器插件：** 进程级 `EnsureStarted` / `EditorAvaloniaApp`、可收集 ALC 目录（`IEditorPage`、`EditorPluginCatalog`），以及 HelloWorld 中不 Shutdown Avalonia 即可换页的 Dock。见 [编辑器插件](editor-plugins.md)。
- 宿主脚本权威源是 `src/JLeb.Estragonia/host-scripts/`。升级包 **不会** 改已插入的文件；若宿主 API 有 breaking，请对照该目录手动合并。

## 1.0.1

- **对包消费者的破坏性变更：** `AvaloniaControl` / `UiHost` 不再作为 Godot 节点类型打进 `Ouse.Estragonia`。请从模板/示例复制进 Godot 工程（或用 `dotnet new estragonia`）。实现逻辑在 NuGet 内的 `AvaloniaControlEngine`。
- 规避宿主类型在外部程序集时 Godot ScriptTypeBiMap 重复 key 热重载错误。

## 1.0.0

本维护分支的首个版本：

- Avalonia 12 / Godot 4.7 / .NET 10
- CPM 统一包版本（`Directory.Packages.props`）
- 输入穿透、Avalonia 12 Dispatcher / 资源加载修复
- DocFX 文档 + GitHub Pages 工作流
- `dotnet new estragonia` 项目模板
- NuGet：[Ouse.Estragonia](https://www.nuget.org/packages/Ouse.Estragonia/) + [Ouse.Estragonia.Templates](https://www.nuget.org/packages/Ouse.Estragonia.Templates/)
- 源码：[0use-TE/Estragonia](https://github.com/0use-TE/Estragonia)

基于 [MrJul/Estragonia](https://github.com/MrJul/Estragonia)（MIT）。上游包名仍为 `JLeb.Estragonia`。
