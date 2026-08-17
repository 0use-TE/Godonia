using Avalonia.Controls;

namespace HelloWorld.Editor.WorldManager;

public partial class WorldManagerView : UserControl {

	public WorldManagerView() {
		InitializeComponent();
		DataContext ??= new WorldManagerViewModel();
	}

}