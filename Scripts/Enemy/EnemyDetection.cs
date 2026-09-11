using Godot;

/// <summary>
/// Parte parcial de EnemyAI: detección del jugador por distancia real, no
/// por Area2D. Usa histéresis (AggroRange/DeaggroRange) para evitar
/// parpadeo de estado en el borde del rango, y AttackRange define qué tan
/// pegado debe estar para golpear.
/// </summary>
public partial class EnemyAI
{
	[ExportGroup("Detection")]
	// Rango en el que el enemigo "despierta" y empieza a perseguir
	[Export] public float AggroRange { get; set; } = 90f;
	// Rango en el que deja de perseguir — debe ser MAYOR a AggroRange para evitar parpadeo
	[Export] public float DeaggroRange { get; set; } = 130f;
	// Distancia de golpe: qué tan cerca debe estar del jugador para atacar
	[Export] public float AttackRange { get; set; } = 60f;

	private bool   _playerInSight = false; // sabe dónde está el jugador, lo persigue
	private bool   _playerInRange = false; // está lo bastante cerca para golpear
	private Node2D _player        = null;

	// Se llama una vez desde _Ready del core para encontrar al jugador por grupo.
	private void InitPlayerReference()
	{
		var candidates = GetTree().GetNodesInGroup("player");
		if (candidates.Count > 0)
			_player = candidates[0] as Node2D;

		if (_player == null)
			GD.PrintErr("[Enemy] No se encontró ningún nodo en el grupo 'player'.");
	}

	// Llamar cada physics frame, ANTES del switch de estados, desde _PhysicsProcess del core.
	private void UpdateDetection()
	{
		if (_player == null) return;

		float distance = GlobalPosition.DistanceTo(_player.GlobalPosition);

		// Histéresis en la detección de largo alcance
		if (!_playerInSight && distance <= AggroRange)
			_playerInSight = true;
		else if (_playerInSight && distance > DeaggroRange)
			_playerInSight = false;

		// Rango de ataque: sin histéresis, es binario y de corto alcance
		_playerInRange = distance <= AttackRange;
	}
}
