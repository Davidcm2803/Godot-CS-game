using Godot;

// IA del enemigo. Maneja patrulla, detección, combate y daño.
// Animaciones: Idle, Running, Attack, Death.
// Patrulla automáticamente desde su posición inicial usando PatrolRange.
// Cuando detecta al jugador se obsesiona: lo persigue, se queda en rango y ataca sin parar.
public partial class EnemyAI : CharacterBody2D
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

	[ExportGroup("Combat")]
	// Duración total del ataque, se calcula automáticamente desde la animación
	[Export] public float AttackDuration { get; set; } = 0.60f;
	// Momento en que el hitbox se activa durante el ataque
	[Export] public float AttackHitStart { get; set; } = 0.15f;
	// Momento en que el hitbox se desactiva durante el ataque
	[Export] public float AttackHitEnd   { get; set; } = 0.45f;
	// Tiempo de espera entre ataques
	[Export] public float AttackCooldown { get; set; } = 0.8f;
	// Duración del estado de daño recibido
	[Export] public float HurtDuration   { get; set; } = 0.35f;
	// Fuerza del empuje al recibir un golpe
	[Export] public float KnockbackForce { get; set; } = 180f;

	// Posición vertical de la barra de vida sobre la cabeza del enemigo
	[Export] public float HealthBarOffsetY { get; set; } = -80f;

	public enum State { Patrol, Idle, Chase, PreAttack, Attack, Hurt, Stunned, Dead }

	private State _state = State.Patrol;
	public  State CurrentState => _state;

	private float _attackTimer    = 0f;
	private float _attackCdTimer  = 0f;
	private float _hurtTimer      = 0f;
	private float _idleTimer      = 0f;
	private float _preAttackTimer = 0f;
	private float _startDelayTimer = 0f;   // evita que el enemigo corra al arrancar
	private bool  _attackFinished = false;
	private bool  _startReady     = false; // true cuando el delay inicial terminó

	// Tiempo de telegrafía antes de atacar
	private const float PreAttackDuration = 0.15f;

	private int    _facingDir     = -1;
	private int    _patrolDir     =  1;
	private float  _patrolOriginX =  0f;
	private bool   _playerInSight = false;
	private bool   _playerInRange = false;
	private Node2D _player        = null;

	// Posiciones locales originales de las zonas — se usan para voltearlas
	// junto con el sprite cuando el enemigo cambia de dirección.
	private Vector2 _detectionZoneBaseLocalPos;
	private Vector2 _attackZoneBaseLocalPos;

	private AnimatedSprite2D _sprite;
	private HealthSystem     _health;
	private PostureSystem    _posture;
	private Hitbox           _attackHitbox;
	private Hurtbox          _hurtbox;
	private Area2D           _detectionZone;
	private Area2D           _attackZone;
	private ProgressBar      _healthBar;

	public override void _Ready()
	{
		_sprite        = GetNode<AnimatedSprite2D>("AnimatedSprite");
		_health        = GetNode<HealthSystem>("HealthSystem");
		_posture       = GetNode<PostureSystem>("PostureSystem");
		_attackHitbox  = GetNodeOrNull<Hitbox>("AttackHitbox");
		_hurtbox       = GetNodeOrNull<Hurtbox>("Hurtbox");
		_detectionZone = GetNode<Area2D>("DetectionZone");
		_attackZone    = GetNode<Area2D>("AttackZone");

		if (_attackHitbox == null) GD.PrintErr("[Enemy] AttackHitbox sin script");
		if (_hurtbox      == null) GD.PrintErr("[Enemy] Hurtbox sin script");

		// Guarda la posición local original de las zonas ANTES de tocarlas,
		// para poder reflejarlas en X cuando el enemigo mire hacia la izquierda.
		_detectionZoneBaseLocalPos = _detectionZone.Position;
		_attackZoneBaseLocalPos    = _attackZone.Position;

		// Calcula la duración real del ataque desde la animación
		var frames     = _sprite.SpriteFrames;
		int frameCount = frames.GetFrameCount("Attack");
		float fps      = (float)frames.GetAnimationSpeed("Attack");
		AttackDuration = (fps > 0f) ? (frameCount / fps) : 0.60f;

		_sprite.AnimationFinished += OnAnimationFinished;

		// IMPORTANTE: guarda el origen DESPUÉS de que el nodo esté colocado en la escena.
		// CallDeferred garantiza que GlobalPosition ya tiene el valor final del editor.
		CallDeferred(MethodName.InitPatrolOrigin);

		// Conecta señales
		_health.Died               += OnDied;
		_health.HealthChanged      += OnHealthChanged;
		if (_hurtbox != null)
			_hurtbox.HitReceived   += OnHitReceived;
		_posture.PostureBroken     += OnPostureBroken;
		_posture.PostureRestored   += OnPostureRestored;
		_detectionZone.BodyEntered += OnDetectionEntered;
		_detectionZone.BodyExited  += OnDetectionExited;
		_attackZone.BodyEntered    += OnAttackZoneEntered;
		_attackZone.BodyExited     += OnAttackZoneExited;

		// Barra de vida flotante
		_healthBar = new ProgressBar();
		_healthBar.MaxValue          = _health.MaxHealth;
		_healthBar.Value             = _health.MaxHealth;
		_healthBar.ShowPercentage    = false;
		_healthBar.CustomMinimumSize = new Vector2(60, 8);

		var redFill = new StyleBoxFlat();
		redFill.BgColor = new Color(0.85f, 0.1f, 0.1f);
		_healthBar.AddThemeStyleboxOverride("fill", redFill);

		var redBg = new StyleBoxFlat();
		redBg.BgColor = new Color(0.2f, 0.0f, 0.0f);
		_healthBar.AddThemeStyleboxOverride("background", redBg);

		AddChild(_healthBar);

		// Empieza quieto — el delay evita que salga corriendo al primer frame
		_startDelayTimer = StartDelay;
		_sprite.Play("Idle");

		// Aplica el flip inicial de sprite y zonas según _facingDir de arranque
		UpdateSpriteFacing();
	}

	// Guarda la posición real del nodo como centro de patrulla — se llama diferido
	private void InitPatrolOrigin()
	{
		_patrolOriginX = GlobalPosition.X;
		_startReady    = true;
		ChangeState(State.Patrol);
	}

	// Mantiene la barra de vida centrada sobre el enemigo cada frame
	public override void _Process(double delta)
	{
		if (_healthBar != null)
			_healthBar.Position = new Vector2(-_healthBar.Size.X / 2f, HealthBarOffsetY);
	}

	// Se dispara cuando la animación sin loop termina
	private void OnAnimationFinished()
	{
		if (_state == State.Attack)
			_attackFinished = true;

		// Cuando la animación de muerte termina, el enemigo queda congelado en el último frame
		// No hace nada más — el nodo puede borrarse desde el exterior si se desea
	}

	// Actualiza la barra de vida cuando el enemigo recibe daño
	private void OnHealthChanged(float current, float max)
	{
		if (_healthBar != null)
		{
			_healthBar.MaxValue = max;
			_healthBar.Value    = current;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// Muerto — no hace nada, sin física, sin movimiento
		if (_state == State.Dead) return;

		float dt = (float)delta;

		// Delay inicial: el enemigo espera quieto antes de empezar a patrullar
		if (!_startReady)
		{
			_startDelayTimer -= dt;
			ApplyGravity(dt);
			MoveAndSlide();
			return;
		}

		if (_attackCdTimer > 0f) _attackCdTimer -= dt;

		switch (_state)
		{
			case State.Patrol:    StatePatrol(dt);    break;
			case State.Idle:      StateIdle(dt);      break;
			case State.Chase:     StateChase(dt);     break;
			case State.PreAttack: StatePreAttack(dt); break;
			case State.Attack:    StateAttack(dt);    break;
			case State.Hurt:      StateHurt(dt);      break;
			case State.Stunned:   StateStunned(dt);   break;
		}

		ApplyGravity(dt);
		MoveAndSlide();
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
		// Perdió de vista al jugador — vuelve a patrullar
		if (_player == null || !_playerInSight) { ChangeState(State.Patrol); return; }

		// En rango de ataque — se frena, se orienta y ataca
		if (_playerInRange)
		{
			Velocity = new Vector2(0f, Velocity.Y);
			// Se orienta hacia el jugador aunque esté quieto
			float dirX = _player.GlobalPosition.X - GlobalPosition.X;
			_facingDir = dirX > 0 ? 1 : -1;
			UpdateSpriteFacing();

			// Ataca si el cooldown lo permite
			if (_attackCdTimer <= 0f)
				ChangeState(State.PreAttack);

			return;
		}

		// Fuera de rango — corre hacia el jugador
		float chaseDir = _player.GlobalPosition.X - GlobalPosition.X;
		_facingDir = chaseDir > 0 ? 1 : -1;
		Velocity   = new Vector2(_facingDir * ChaseSpeed, Velocity.Y);
		UpdateSpriteFacing();
	}

	// Pausa muy breve antes de atacar para telegrafiar el golpe
	private void StatePreAttack(float dt)
	{
		Velocity         = new Vector2(0f, Velocity.Y);
		_preAttackTimer += dt;

		// Sigue orientándose al jugador durante la telegrafía
		if (_player != null)
		{
			float dirX = _player.GlobalPosition.X - GlobalPosition.X;
			_facingDir = dirX > 0 ? 1 : -1;
			UpdateSpriteFacing();
		}

		if (_preAttackTimer >= PreAttackDuration)
		{
			_preAttackTimer = 0f;
			ChangeState(State.Attack);
		}
	}

	// Activa el hitbox durante la ventana de golpe y espera que termine la animación
	private void StateAttack(float dt)
	{
		Velocity      = new Vector2(0f, Velocity.Y);
		_attackTimer += dt;

		if (_attackTimer >= AttackHitStart && _attackTimer < AttackHitEnd)
			_attackHitbox?.Activate();
		if (_attackTimer >= AttackHitEnd)
			_attackHitbox?.Deactivate();

		// Termina cuando la animación avisa o el timer vence
		if (_attackFinished || _attackTimer >= AttackDuration)
		{
			_attackTimer    = 0f;
			_attackFinished = false;
			_attackCdTimer  = AttackCooldown;
			_attackHitbox?.Deactivate();

			// Obsesión: si el jugador sigue en rango vuelve a Chase para reagrupar
			// Chase se encargará de atacar de nuevo cuando el cooldown termine
			if (_playerInSight)
				ChangeState(State.Chase);
			else
				ChangeState(State.Patrol);
		}
	}

	// Aplica knockback hacia atrás y espera antes de volver a actuar
	private void StateHurt(float dt)
	{
		Velocity    = new Vector2(Mathf.MoveToward(Velocity.X, 0f, KnockbackForce * dt * 5f), Velocity.Y);
		_hurtTimer += dt;

		if (_hurtTimer >= HurtDuration)
		{
			_hurtTimer = 0f;
			ChangeState(_playerInSight ? State.Chase : State.Patrol);
		}
	}

	// El enemigo queda paralizado hasta que PostureSystem restaure la postura
	private void StateStunned(float dt)
	{
		Velocity = new Vector2(0f, Velocity.Y);
	}

	// Recibe un golpe, aplica knockback y cambia al estado de daño
	private void OnHitReceived(float damage, Vector2 hitPos)
	{
		if (_state == State.Dead || _state == State.Stunned) return;
		float knockDir = GlobalPosition.X < hitPos.X ? -1f : 1f;
		Velocity       = new Vector2(knockDir * KnockbackForce, -60f);
		_hurtTimer     = 0f;
		_attackHitbox?.Deactivate();
		ChangeState(State.Hurt);
	}

	// El enemigo muere — se congela completamente y reproduce la animación de muerte
	private void OnDied()
	{
		_attackHitbox?.Deactivate();

		// Detiene todo movimiento inmediatamente
		Velocity = Vector2.Zero;

		// Desactiva colisiones para que no bloquee al jugador
		SetDeferred(CharacterBody2D.PropertyName.CollisionMask, 0u);
		SetDeferred(CharacterBody2D.PropertyName.CollisionLayer, 0u);

		// Oculta la barra de vida
		if (_healthBar != null) _healthBar.Visible = false;

		ChangeState(State.Dead);
	}

	// La postura se rompe, el enemigo queda stunneado
	private void OnPostureBroken()
	{
		_attackHitbox?.Deactivate();
		ChangeState(State.Stunned);
	}

	// La postura se restaura, el enemigo vuelve a actuar agresivamente
	private void OnPostureRestored()
	{
		if (_state == State.Stunned)
			ChangeState(_playerInSight ? State.Chase : State.Patrol);
	}

	// El jugador entra en el campo de visión
	private void OnDetectionEntered(Node2D body)
	{
		if (body.IsInGroup("player")) { _player = body; _playerInSight = true; }
	}

	// El jugador sale del campo de visión
	private void OnDetectionExited(Node2D body)
	{
		if (body.IsInGroup("player")) _playerInSight = false;
	}

	// El jugador entra en rango de ataque
	private void OnAttackZoneEntered(Node2D body)
	{
		if (body.IsInGroup("player")) _playerInRange = true;
	}

	// El jugador sale del rango de ataque
	private void OnAttackZoneExited(Node2D body)
	{
		if (body.IsInGroup("player")) _playerInRange = false;
	}

	// Cambia el estado y resetea el flag de animación terminada
	private void ChangeState(State next)
	{
		if (_state == next) return;
		if (next != State.Attack) _attackFinished = false;
		_state = next;
		UpdateAnimation();
	}

	// Reproduce la animación correspondiente al estado actual
	private void UpdateAnimation()
	{
		if (_sprite == null) return;

		string anim = _state switch
		{
			State.Attack                => "Attack",
			State.Patrol or State.Chase => "Running",
			State.Dead                  => "Death",   // animación de muerte sin loop
			_                           => "Idle"
		};

		if (_sprite.Animation != anim)
			_sprite.Play(anim);
	}

	// Voltea el sprite Y las zonas de detección/ataque para que sigan la dirección.
	// BUG ORIGINAL: solo se volteaba el sprite (FlipH). DetectionZone y AttackZone
	// se quedaban con su posición local fija (mirando siempre hacia la derecha),
	// así que al perseguir hacia la izquierda el AttackZone quedaba detrás del
	// enemigo, nunca detectaba al jugador, y el enemigo seguía corriendo derecho
	// contra el CharacterBody2D del jugador — chocando como si fuera una pared
	// invisible hasta que el jugador lo tocaba.
	private void UpdateSpriteFacing()
	{
		if (_sprite == null) return;
		_sprite.FlipH  = _facingDir < 0;
		_sprite.Offset = new Vector2(_facingDir < 0 ? -15f : -1f, 0f);

		if (_detectionZone != null)
			_detectionZone.Position = new Vector2(
				_detectionZoneBaseLocalPos.X * _facingDir,
				_detectionZoneBaseLocalPos.Y);

		if (_attackZone != null)
			_attackZone.Position = new Vector2(
				_attackZoneBaseLocalPos.X * _facingDir,
				_attackZoneBaseLocalPos.Y);
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

	public HealthSystem  Health  => _health;
	public PostureSystem Posture => _posture;
}