using Godot;

/// <summary>
/// Parte parcial de EnemyAI: locomoción básica. Patrulla automáticamente
/// desde su posición inicial usando PatrolRange, espera en cada extremo,
/// y persigue al jugador (Chase) cuando lo tiene a la vista.
/// </summary>
public partial class EnemyAI
{
	[ExportGroup("Movement")]
	[Export] public float PatrolSpeed  { get; set; } = 60f;
	[Export] public float ChaseSpeed   { get; set; } = 140f;
	[Export] public float Gravity      { get; set; } = 900f;
	[Export] public float MaxFallSpeed { get; set; } = 600f;

	[ExportGroup("Patrol")]
	// Tiempo de espera al llegar a un extremo de la patrulla
	[Export] public float IdleWaitTime { get; set; } = 1.5f;
	// Distancia en píxeles que patrulla hacia cada lado desde su posición inicial
	[Export] public float PatrolRange  { get; set; } = 150f;
	// Cuántos frames espera antes de empezar a patrullar — evita que salga corriendo al arrancar
	[Export] public float StartDelay   { get; set; } = 0.3f;

	private float _idleTimer       = 0f;
	private float _startDelayTimer = 0f;   // evita que el enemigo corra al arrancar
	private bool  _startReady      = false; // true cuando el delay inicial terminó

	private int   _facingDir     = -1;
	private int   _patrolDir     =  1;
	private float _patrolOriginX =  0f;

	// Guarda la posición real del nodo como centro de patrulla — se llama diferido
	private void InitPatrolOrigin()
	{
		_patrolOriginX = GlobalPosition.X;
		_startReady    = true;
		ChangeState(State.Patrol);
	}

	// Mueve al enemigo de un extremo al otro dentro del rango de patrulla
	private void StatePatrol(float dt)
	{
		// Si detecta al jugador, abandona la patrulla inmediatamente
		if (_playerInSight) { ChangeState(State.Chase); return; }

		float leftLimit  = _patrolOriginX - PatrolRange;
		float rightLimit = _patrolOriginX + PatrolRange;
		float targetX    = _patrolDir > 0 ? rightLimit : leftLimit;
		float distX      = targetX - GlobalPosition.X;

		if (Mathf.Abs(distX) < 8f)
		{
			// Llegó al extremo — espera antes de dar la vuelta
			Velocity   = new Vector2(0f, Velocity.Y);
			_idleTimer = IdleWaitTime;
			ChangeState(State.Idle);
		}
		else
		{
			_facingDir = distX > 0 ? 1 : -1;
			Velocity   = new Vector2(_facingDir * PatrolSpeed, Velocity.Y);
			UpdateSpriteFacing();
		}
	}

	// Pausa en el extremo de la patrulla antes de invertir dirección
	private void StateIdle(float dt)
	{
		Velocity    = new Vector2(0f, Velocity.Y);
		_idleTimer -= dt;

		// Si ve al jugador durante la pausa, lo persigue de inmediato
		if (_playerInSight) { ChangeState(State.Chase); return; }

		if (_idleTimer <= 0f)
		{
			_patrolDir = -_patrolDir;
			ChangeState(State.Patrol);
		}
	}

	// Persigue al jugador — si ya está en rango, se frena y ataca
	private void StateChase(float dt)
	{
		if (_attackCdTimer > 0f)
		{
			_attackCdTimer -= dt;
		}


		// Perdió de vista al jugador — vuelve a patrullar
	if (_player == null || !_playerInSight) { ChangeState(State.Patrol); return; }

		// En rango de ataque — se frena, se orienta y ataca
		if (_playerInRange)
		{
			Velocity = new Vector2(0f, Velocity.Y);
			float dirX = _player.GlobalPosition.X - GlobalPosition.X;
			_facingDir = dirX > 0 ? 1 : -1;
			UpdateSpriteFacing();

			// Ataca apenas el cooldown lo permite
			if (_attackCdTimer <= 0f)
				ChangeState(State.PreAttack);

			return;
		}

		// Fuera de rango — corre hacia el jugador a velocidad completa desde el primer frame
		float chaseDir = _player.GlobalPosition.X - GlobalPosition.X;
		_facingDir = chaseDir > 0 ? 1 : -1;

		// Bonus de velocidad cuanto más cerca está — cierre de distancia explosivo,
		// no una persecución pareja tipo "trote".
		float distance   = Mathf.Abs(chaseDir);
		float closeBoost = Mathf.Clamp(1f - (distance / AggroRange), 0f, 1f); // 0 lejos -> 1 cerca
		float speed      = ChaseSpeed * (1f + closeBoost * 0.5f); // hasta +50% al estar cerca

		Velocity = new Vector2(_facingDir * speed, Velocity.Y);
		UpdateSpriteFacing();
	}

	private void ApplyGravity(float dt)
	{
		if (!IsOnFloor())
		{
			float newVY = Velocity.Y + Gravity * dt;
			Velocity = new Vector2(Velocity.X, Mathf.Min(newVY, MaxFallSpeed));
		}
		else if (Velocity.Y > 0f)
		{
			Velocity = new Vector2(Velocity.X, 0f);
		}
	}
}
