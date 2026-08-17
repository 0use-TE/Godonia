using System;
using Godot;

namespace Ouse.Godonia;

/// <summary>Localized strings for the built-in Godonia dock wizard.</summary>
public static class EditorPluginWizardL10n {

	public sealed record Strings(
		string DockTitle,
		string Heading,
		string Intro,
		string LabelTitle,
		string LabelName,
		string LabelId,
		string LabelSlot,
		string SlotLeft,
		string SlotRight,
		string SlotBottom,
		string SlotMainScreen,
		string Mvvm,
		string AddToSln,
		string AddToPreview,
		string Create,
		string Creating,
		string DefaultTitle
	);

	public static Strings ForLocale(string? locale)
		=> Resolve(string.IsNullOrWhiteSpace(locale) ? DetectLocale() : locale!);

	public static Strings Current
		=> ForLocale(null);

	public static string DetectLocale() {
		try {
			var locale = TranslationServer.GetLocale();
			if (!string.IsNullOrWhiteSpace(locale))
				return locale;
		}
		catch {
		}

		try {
			var lang = OS.GetLocaleLanguage();
			if (!string.IsNullOrWhiteSpace(lang))
				return lang;
		}
		catch {
		}

		return "en";
	}

	public static bool IsChinese(string locale)
		=> locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

	public static Strings Resolve(string locale)
		=> IsChinese(locale) ? Zh : En;

	private static readonly Strings En = new(
		DockTitle: "Godonia",
		Heading: "New editor dock",
		Intro: "Creates an Avalonia project (XAML + C#). After this, only edit that project — no Godot addon or extra Build.",
		LabelTitle: "Dock title",
		LabelName: "C# name",
		LabelId: "Plugin id",
		LabelSlot: "Placement",
		SlotLeft: "Left",
		SlotRight: "Right",
		SlotBottom: "Bottom",
		SlotMainScreen: "Main screen",
		Mvvm: "MVVM (CommunityToolkit)",
		AddToSln: "Add to .slnx",
		AddToPreview: "Reference from Editor.Preview",
		Create: "Create",
		Creating: "Creating…",
		DefaultTitle: "Inspector"
	);

	private static readonly Strings Zh = new(
		DockTitle: "Godonia",
		Heading: "新建编辑器面板",
		Intro: "会创建一个 Avalonia 项目（XAML + C#）。之后只需编辑并编译该项目，不必再写 Godot 插件，也不必点 Godot 的 Build。",
		LabelTitle: "面板标题",
		LabelName: "C# 名称",
		LabelId: "插件 ID",
		LabelSlot: "位置",
		SlotLeft: "左侧",
		SlotRight: "右侧",
		SlotBottom: "底部",
		SlotMainScreen: "主屏幕",
		Mvvm: "MVVM（CommunityToolkit）",
		AddToSln: "加入解决方案",
		AddToPreview: "引用到 Editor.Preview",
		Create: "创建",
		Creating: "正在创建…",
		DefaultTitle: "Inspector"
	);

}
