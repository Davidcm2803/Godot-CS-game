using Godot;
//test
// Autoload (singleton) que maneja el cambio entre escenas/mapas.
// Guarda el nombre del spawn point donde debe aparecer el jugador
// en la escena destino, para no depender de coordenadas fijas.
public partial class SceneTransition : Node
{
	public static SceneTransition Instance { get; private set; }

	// Nombre del Marker2D (dentro de la escena destino) donde debe
	// aparecer el jugador al cargar. Se lee en el _Ready() del Player.
	public string PendingSpawnPoint { get; private set; } = "";

	public override void _Ready()
	{
		Instance = this;
	}

	// Llamado desde el trigger de borde de mapa (LevelExit).
	// scenePath: ruta al archivo .tscn del mapa destino.
	// spawnPointName: nombre del Marker2D donde debe aparecer el jugador.
	public void ChangeScene(string scenePath, string spawnPointName)
	{
		PendingSpawnPoint = spawnPointName;
		GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, scenePath);
	}

	// El Player la llama en su _Ready() de la nueva escena para
	// saber si tiene que reposicionarse en un spawn point específico.
	public string ConsumePendingSpawnPoint()
	{
		string spawn = PendingSpawnPoint;
		PendingSpawnPoint = "";
		return spawn;
	}
}
