using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HelloWorld.Editor.QuestManager;

public sealed partial class QuestRow : ObservableObject {

	public required string Id { get; init; }

	[ObservableProperty]
	private string _name = "";

	[ObservableProperty]
	private string _giver = "";

	[ObservableProperty]
	private string _state = "Active";

}

public sealed partial class QuestManagerViewModel : ObservableObject {

	private int _next = 4;

	[ObservableProperty]
	private string _title = "Quest Manager";

	[ObservableProperty]
	private string _status = "Bottom dock sample — track quests while editing the world.";

	[ObservableProperty]
	private QuestRow? _selected;

	public ObservableCollection<QuestRow> Quests { get; } = [
		new() { Id = "Q01", Name = "Find the lost beacon", Giver = "Harbor Master", State = "Active" },
		new() { Id = "Q02", Name = "Clear the east ruins", Giver = "Captain Rhea", State = "Active" },
		new() { Id = "Q03", Name = "Deliver crystal shipment", Giver = "Trader Lin", State = "Done" }
	];

	[RelayCommand]
	private void Add() {
		var id = $"Q{_next:00}";
		_next++;
		var row = new QuestRow { Id = id, Name = "New side quest", Giver = "Board", State = "Active" };
		Quests.Add(row);
		Selected = row;
		Status = $"Added {id}";
	}

	[RelayCommand]
	private void Complete() {
		if (Selected is null)
			return;
		Selected.State = "Done";
		Status = $"Completed {Selected.Id}";
	}

}