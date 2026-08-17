using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using HelloWorld.Editor.QuestManager;
using HelloWorld.Editor.SkillTree;
using HelloWorld.Editor.WorldManager;

namespace HelloWorld.Editor.Preview;

/// <summary>
/// Desktop host for Avalonia designer + quick F5 tab preview of sample docks.
/// </summary>
public sealed class MainWindow : Window {

	public MainWindow() {
		Title = "Godonia HelloWorld — editor preview";
		Width = 960;
		Height = 640;

		var tabs = new TabControl {
			Items = {
				new TabItem { Header = "Skill Tree (Left)", Content = new SkillTreeView() },
				new TabItem { Header = "Quest Manager (Bottom)", Content = new QuestManagerView() },
				new TabItem { Header = "World Manager (MainScreen)", Content = new WorldManagerView() }
			}
		};

		Content = tabs;
	}

}