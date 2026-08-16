using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JLeb.Estragonia;

namespace HelloWorld.Editor.Demo;

public partial class DemoView : UserControl {

	public DemoView() {
		InitializeComponent();

		var assembly = typeof(DemoView).Assembly;
		var written = File.Exists(assembly.Location)
			? File.GetLastWriteTime(assembly.Location).ToString("yyyy-MM-dd HH:mm:ss")
			: "n/a";
		BuildStamp.Text = $"HelloWorld.Editor.Demo  dll {written}  created {DateTime.Now:HH:mm:ss}";
	}

	private void OnReloadClick(object? sender, RoutedEventArgs e)
		=> EditorPluginCatalog.Reload("estragonia.demo");

	private void OnLogClick(object? sender, RoutedEventArgs e)
		=> EditorLogHub.Write("Demo button clicked.");

}
