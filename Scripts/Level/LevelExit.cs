using Godot;

// Área en el borde del mapa. Al entrar el jugador, carga la escena
// destino y lo posiciona en el spawn point indicado.
public partial class LevelExit : Area2D
{
	// Ruta al .tscn de la escena a cargar (ej: "res://Scenes/Level/Level2.tscn")
	[Export] public string TargetScenePath { get; set; } = "";

	// Nombre del Marker2D en la escena destino donde debe aparecer el jugador
	[Export] public string TargetSpawnPoint { get; set; } = "SpawnLeft";

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player")) return;
		if (string.IsNullOrEmpty(TargetScenePath)) return;

		SceneTransition.Instance.ChangeScene(TargetScenePath, TargetSpawnPoint);
	}
}
