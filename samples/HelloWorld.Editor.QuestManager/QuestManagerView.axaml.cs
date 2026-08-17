using Avalonia.Controls;

namespace HelloWorld.Editor.QuestManager;

public partial class QuestManagerView : UserControl {

	public QuestManagerView() {
		InitializeComponent();
		DataContext ??= new QuestManagerViewModel();
	}

}