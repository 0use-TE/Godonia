using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HelloWorld.Editor.WorldManager;

public sealed partial class WorldRegion : ObservableObject {

	public required string Name { get; init; }

	public required string Biome { get; init; }

	public string Summary => $"{Biome} · chunk {ChunkSize:0}";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Summary))]
	private double _chunkSize = 64;

	[ObservableProperty]
	private double _lodBias = 1.5;

	[ObservableProperty]
	private decimal _spawnBudget = 120;

}

public sealed partial class WorldManagerViewModel : ObservableObject {

	[ObservableProperty]
	private string _title = "World Manager";

	[ObservableProperty]
	private string _status = "MainScreen sample — full-page Avalonia tool for region streaming.";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(DetailText))]
	private WorldRegion? _selectedRegion;

	public string DetailText => SelectedRegion is null
		? ""
		: $"{SelectedRegion.Name}: chunk {SelectedRegion.ChunkSize:0}, LOD {SelectedRegion.LodBias:0.0}, spawn budget {SelectedRegion.SpawnBudget}.";

	public ObservableCollection<WorldRegion> Regions { get; } = [
		new() { Name = "Harbor District", Biome = "Coastal / Urban", ChunkSize = 48, LodBias = 1.0, SpawnBudget = 80 },
		new() { Name = "East Ruins", Biome = "Arid ruins", ChunkSize = 64, LodBias = 2.0, SpawnBudget = 140 },
		new() { Name = "Crystal Caves", Biome = "Underground", ChunkSize = 32, LodBias = 0.5, SpawnBudget = 60 }
	];

	public WorldManagerViewModel()
		=> SelectedRegion = Regions[0];

	[RelayCommand]
	private void MarkDirty() {
		if (SelectedRegion is null)
			return;
		Status = $"{SelectedRegion.Name} marked dirty for rebake.";
	}

	[RelayCommand]
	private void Bake() {
		if (SelectedRegion is null)
			return;
		Status = $"Baked lighting for {SelectedRegion.Name} (demo only).";
	}

}
