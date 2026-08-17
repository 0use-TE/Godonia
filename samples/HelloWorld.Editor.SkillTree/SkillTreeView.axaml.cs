using Avalonia.Controls;

namespace HelloWorld.Editor.SkillTree;

public partial class SkillTreeView : UserControl {

	public SkillTreeView() {
		InitializeComponent();
		DataContext ??= new SkillTreeViewModel();
	}

}