using HelloWorld.ViewModels;
using HelloWorld.Views;
using Ouse.Godonia;
using AvControl = Avalonia.Controls.Control;

namespace HelloWorld;

/// <summary>Default Avalonia host. Swap the root view / view-model as needed.</summary>
public partial class UserInterface : UiHost {

	protected override AvControl CreateRoot()
		=> new MainView { DataContext = new MainViewModel() };

}
