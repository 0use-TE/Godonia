using Avalonia.Controls;
using HelloWorld.Editor.Demo;
using HelloWorld.Editor.Log;

namespace HelloWorld.Editor.Preview;

public sealed class MainWindow : Window {

	public MainWindow() {
		Title = "Estragonia editor preview";
		Width = 760;
		Height = 540;
		Content = new TabControl {
			ItemsSource = new[] {
				new TabItem {
					Header = "Demo",
					Content = new DemoPage().Create()
				},
				new TabItem {
					Header = "Log",
					Content = new LogPage().Create()
				}
			}
		};
	}

}
