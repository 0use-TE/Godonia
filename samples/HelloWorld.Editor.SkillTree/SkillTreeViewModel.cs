using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HelloWorld.Editor.SkillTree;

public sealed partial class SkillNode : ObservableObject {

	public required string Id { get; init; }

	public required string Name { get; init; }

	public required string Description { get; init; }

	public int MaxRank { get; init; } = 3;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(RankText))]
	[NotifyPropertyChangedFor(nameof(CanUnlock))]
	private int _rank;

	public string RankText => $"Rank {Rank}/{MaxRank}";

	public bool CanUnlock => Rank < MaxRank;

}

public sealed partial class SkillTreeViewModel : ObservableObject {

	[ObservableProperty]
	private string _title = "Skill Tree";

	[ObservableProperty]
	private string _status = "Left dock sample — unlock skills, then rebuild this project to hot-reload.";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PointsText))]
	private int _points = 5;

	public string PointsText => $"Skill points: {Points}";

	public ObservableCollection<SkillNode> Skills { get; } = [
		new() { Id = "slash", Name = "Slash", Description = "Basic melee. Each rank +10% damage." },
		new() { Id = "dash", Name = "Dash", Description = "Short invuln dodge. Rank 2 adds a second charge." },
		new() { Id = "barrier", Name = "Barrier", Description = "Absorb hit for allies. Costs 1 point per rank." },
		new() { Id = "focus", Name = "Focus Fire", Description = "Mark a target; team crit chance up." }
	];

	[RelayCommand]
	private void Unlock(SkillNode? node) {
		if (node is null || !node.CanUnlock || Points <= 0)
			return;
		node.Rank++;
		Points--;
		Status = $"Unlocked {node.Name} → {node.RankText}";
	}

	[RelayCommand]
	private void Reset() {
		foreach (var s in Skills)
			s.Rank = 0;
		Points = 5;
		Status = "Tree reset.";
	}

}