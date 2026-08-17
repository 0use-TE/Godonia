using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Godot;
using AvTextBox = Avalonia.Controls.TextBox;
using AvButton = Avalonia.Controls.Button;
using AvComboBox = Avalonia.Controls.ComboBox;
using AvCheckBox = Avalonia.Controls.CheckBox;

namespace Ouse.Godonia;

/// <summary>
/// Built-in dock UI that scaffolds a new collectible Avalonia editor plugin.
/// Lives in this assembly (persistent ALC) so it does not need a plugin.json.
/// </summary>
public sealed class EditorPluginWizardView : UserControl {

	private readonly EditorPluginWizardL10n.Strings _t;
	private readonly (string Label, string Slot)[] _slots;
	private readonly AvTextBox _titleBox;
	private readonly AvTextBox _nameBox;
	private readonly AvTextBox _idBox;
	private readonly AvComboBox _slotBox;
	private readonly AvCheckBox _mvvmBox;
	private readonly AvCheckBox _slnBox;
	private readonly AvCheckBox _previewBox;
	private readonly TextBlock _status;
	private readonly AvButton _create;
	private readonly StackPanel _installedPanel;
	private bool _syncing;
	private string? _pendingUninstallId;

	public EditorPluginWizardView(string? editorLocale = null) {
		_t = EditorPluginWizardL10n.ForLocale(editorLocale);
		_slots = [
			(_t.SlotLeft, "Left"),
			(_t.SlotRight, "Right"),
			(_t.SlotBottom, "Bottom"),
			(_t.SlotMainScreen, "MainScreen")
		];

		_titleBox = Field();
		_nameBox = Field();
		_idBox = Field();
		_slotBox = new AvComboBox {
			ItemsSource = Array.ConvertAll(_slots, s => s.Label),
			SelectedIndex = 0,
			MinWidth = 160
		};
		_mvvmBox = new AvCheckBox { Content = _t.Mvvm, IsChecked = true };
		_slnBox = new AvCheckBox { Content = _t.AddToSln, IsChecked = true };
		_previewBox = new AvCheckBox { Content = _t.AddToPreview, IsChecked = true };
		_status = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.85 };
		_create = new AvButton { Content = _t.Create, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
		_installedPanel = new StackPanel { Spacing = 8 };

		_titleBox.Text = _t.DefaultTitle;
		SyncFromTitle();
		_titleBox.TextChanged += (_, _) => {
			if (!_syncing)
				SyncFromTitle();
		};

		_create.Click += (_, _) => Create();

		Content = new ScrollViewer {
			Content = new StackPanel {
				Margin = new Thickness(16),
				Spacing = 10,
				Children = {
					new TextBlock {
						Text = _t.Heading,
						FontSize = 20,
						FontWeight = FontWeight.SemiBold
					},
					new TextBlock {
						Text = _t.Intro,
						TextWrapping = TextWrapping.Wrap,
						Opacity = 0.85
					},
					Label(_t.LabelTitle),
					_titleBox,
					Label(_t.LabelName),
					_nameBox,
					Label(_t.LabelId),
					_idBox,
					Label(_t.LabelSlot),
					_slotBox,
					_mvvmBox,
					_slnBox,
					_previewBox,
					_create,
					_status,
					new Border {
						Height = 1,
						Margin = new Thickness(0, 8, 0, 4),
						Background = Brushes.White,
						Opacity = 0.2
					},
					new TextBlock {
						Text = _t.ManageHeading,
						FontSize = 20,
						FontWeight = FontWeight.SemiBold
					},
					new TextBlock {
						Text = _t.ManageIntro,
						TextWrapping = TextWrapping.Wrap,
						Opacity = 0.85
					},
					_installedPanel
				}
			}
		};

		RefreshInstalled();
	}

	private static TextBlock Label(string text)
		=> new() { Text = text, Opacity = 0.7, FontSize = 12 };

	private static AvTextBox Field()
		=> new() { MinWidth = 220 };

	private void SyncFromTitle() {
		_syncing = true;
		var pascal = EditorPluginScaffolder.SanitizePascal(_titleBox.Text ?? "");
		_nameBox.Text = pascal.Length > 0 ? pascal : "Tool";
		var game = "game";
		try {
			game = EditorPluginScaffolder.GameIdentifier(ProjectSettings.GlobalizePath("res://"));
		}
		catch {
			// Preview / tests.
		}

		_idBox.Text = EditorPluginScaffolder.DefaultPluginId(game, _nameBox.Text ?? "Tool");
		_syncing = false;
	}

	private string SelectedSlot() {
		var index = _slotBox.SelectedIndex;
		if (index < 0 || index >= _slots.Length)
			return "Left";
		return _slots[index].Slot;
	}

	private string SlotLabel(string slot) {
		foreach (var pair in _slots) {
			if (string.Equals(pair.Slot, slot, StringComparison.OrdinalIgnoreCase))
				return pair.Label;
		}

		return slot;
	}

	private static string? TryGodotRoot() {
		try {
			return ProjectSettings.GlobalizePath("res://");
		}
		catch {
			return null;
		}
	}

	private void RefreshInstalled() {
		_installedPanel.Children.Clear();
		var root = TryGodotRoot();
		if (root is null) {
			_installedPanel.Children.Add(new TextBlock {
				Text = _t.NoPlugins,
				Opacity = 0.7,
				TextWrapping = TextWrapping.Wrap
			});
			return;
		}

		var plugins = EditorPluginScaffolder.ListInstalled(root);
		if (plugins.Count == 0) {
			_installedPanel.Children.Add(new TextBlock {
				Text = _t.NoPlugins,
				Opacity = 0.7,
				TextWrapping = TextWrapping.Wrap
			});
			return;
		}

		foreach (var plugin in plugins)
			_installedPanel.Children.Add(InstalledRow(plugin));
	}

	private Avalonia.Controls.Control InstalledRow(InstalledEditorPlugin plugin) {
		var pending = string.Equals(_pendingUninstallId, plugin.Id, StringComparison.OrdinalIgnoreCase);
		var actions = new StackPanel {
			Orientation = Avalonia.Layout.Orientation.Horizontal,
			Spacing = 8
		};

		if (pending) {
			var confirm = new AvButton {
				Content = _t.ConfirmUninstall,
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
			};
			confirm.Click += (_, _) => Uninstall(plugin.Id);
			var cancel = new AvButton {
				Content = _t.Cancel,
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
			};
			cancel.Click += (_, _) => {
				_pendingUninstallId = null;
				RefreshInstalled();
			};
			actions.Children.Add(confirm);
			actions.Children.Add(cancel);
		}
		else {
			var uninstall = new AvButton {
				Content = _t.Uninstall,
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
			};
			uninstall.Click += (_, _) => {
				_pendingUninstallId = plugin.Id;
				RefreshInstalled();
			};
			actions.Children.Add(uninstall);
		}

		return new Border {
			Padding = new Thickness(10),
			CornerRadius = new CornerRadius(4),
			Background = new SolidColorBrush(Avalonia.Media.Color.FromArgb(28, 255, 255, 255)),
			Child = new StackPanel {
				Spacing = 4,
				Children = {
					new TextBlock {
						Text = plugin.Title,
						FontWeight = FontWeight.SemiBold
					},
					new TextBlock {
						Text = plugin.Id + " · " + SlotLabel(plugin.DockSlot),
						FontSize = 12,
						Opacity = 0.7,
						TextWrapping = TextWrapping.Wrap
					},
					actions
				}
			}
		};
	}

	private void Create() {
		_create.IsEnabled = false;
		_status.Text = _t.Creating;
		try {
			var root = ProjectSettings.GlobalizePath("res://");
			var result = EditorPluginScaffolder.Create(new EditorPluginScaffoldRequest {
				GodotProjectRoot = root,
				Title = _titleBox.Text ?? "",
				Name = _nameBox.Text ?? "",
				PluginId = _idBox.Text ?? "",
				DockSlot = SelectedSlot(),
				UseMvvm = _mvvmBox.IsChecked == true,
				AddToSolution = _slnBox.IsChecked == true,
				AddToPreview = _previewBox.IsChecked == true,
				EnableInProject = true
			});

			if (!result.Success) {
				_status.Text = result.Message;
				return;
			}

			var status = result.Message;
			if (result.PluginProjectDir is { } dir) {
				var csproj = System.IO.Directory.GetFiles(dir, "*.csproj");
				if (csproj.Length > 0) {
					var buildError = TryDotnetBuild(csproj[0]);
					if (buildError is not null)
						status += System.Environment.NewLine + buildError;
				}
			}

			EditorPluginCatalog.NotifyPagesChanged();
			_status.Text = status;
			_pendingUninstallId = null;
			RefreshInstalled();
		}
		catch (Exception ex) {
			_status.Text = ex.Message;
		}
		finally {
			_create.IsEnabled = true;
		}
	}

	private void Uninstall(string pluginId) {
		_status.Text = _t.Uninstalling;
		try {
			var root = ProjectSettings.GlobalizePath("res://");
			var result = EditorPluginScaffolder.Uninstall(root, pluginId);
			_status.Text = result.Message;
			_pendingUninstallId = null;
			RefreshInstalled();
		}
		catch (Exception ex) {
			_status.Text = ex.Message;
		}
	}

	private static string? TryDotnetBuild(string csproj) {
		try {
			using var process = Process.Start(new ProcessStartInfo {
				FileName = "dotnet",
				Arguments = $"build \"{csproj}\" --nologo",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			});
			if (process is null)
				return "Could not start dotnet build.";

			var stdout = process.StandardOutput.ReadToEnd();
			var stderr = process.StandardError.ReadToEnd();
			if (!process.WaitForExit(120_000)) {
				try {
					process.Kill(entireProcessTree: true);
				}
				catch {
				}
				return "dotnet build timed out.";
			}

			if (process.ExitCode == 0)
				return null;

			var detail = (stderr + stdout).Trim();
			return string.IsNullOrEmpty(detail)
				? $"dotnet build failed ({process.ExitCode})."
				: detail;
		}
		catch (Exception ex) {
			return ex.Message;
		}
	}

}
