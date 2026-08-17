# 业务 AI 提示词

给 AI 写 Godonia 上的 Avalonia UI 时可用：

```
你在 Godot 4 + Godonia 上写 Avalonia 12 UI
（包名/命名空间 Ouse.Godonia 1.0.0，来自自定义编辑器本地 nupkg）。

规则：
- 优先使用 Godonia 自定义 Godot 编辑器；新建工程从 GodotSharp/Godonia/nupkg 还原 Ouse.Godonia（不要默认写 nuget.org）。
- Autoload 里 GodotAvalonia.EnsureStarted / EnsureStarted<App>() 只做一次，不要 Shutdown 再启动。
- AvaloniaControl.cs / UiHost.cs 必须在 Godot C# 工程内。引用 Ouse.Godonia 后若缺失会在 build 时插入；不能只靠 NuGet 程序集里的类型。
- 宿主用 UiHost / CreateRoot；不要在 Application 里 GrabFocus。
- 编辑器 Dock / MainScreen：独立 Microsoft.NET.Sdk 插件工程 + godonia_new_plugin 清单；预览用 {Game}.Editor.Preview；插件程序集禁止 GodotObject。
- 复杂界面用 CommunityToolkit.Mvvm。
- 素材按钮用 PNG，不要指望 Godot ShaderMaterial 挂在 Avalonia 控件上。
- CaptureEmptyHits 为 false 时空白可穿透到 Godot。
- 代码可能由 AI 生成，保持改动小，并在 Godot Forward+ 下自测。
```
