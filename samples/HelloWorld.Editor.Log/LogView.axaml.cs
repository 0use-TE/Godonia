using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using JLeb.Estragonia;

namespace HelloWorld.Editor.Log;

public partial class LogView : UserControl {

	private readonly DispatcherTimer _timer;

	public LogView() {
		InitializeComponent();
		_timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
		_timer.Tick += (_, _) => Refresh();
		AttachedToVisualTree += (_, _) => {
			Refresh();
			_timer.Start();
		};
		DetachedFromVisualTree += (_, _) => _timer.Stop();
	}

	private void Refresh()
		=> LogList.ItemsSource = EditorLogHub.Snapshot();

	private void OnClearClick(object? sender, RoutedEventArgs e) {
		EditorLogHub.Clear();
		Refresh();
	}

	private void OnReloadClick(object? sender, RoutedEventArgs e)
		=> EditorPluginCatalog.Reload("estragonia.log");

}
