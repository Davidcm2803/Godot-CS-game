using Godot;

/// <summary>
/// IA del enemigo (núcleo) maneja el ciclo de vida de Godot, las referencias
/// a nodos, la máquina de estados central, el loop de física y la barra de
/// vida flotante. El resto de la lógica vive repartida en los demás
/// archivos parciales:
///   EnemyMovement.cs  -> Patrol / Idle / Chase, gravedad
///   EnemyCombat.cs    -> PreAttack / Attack / Hurt / Stunned, daño y muerte
///   EnemyDetection.cs -> detección del jugador por distancia (Aggro/Deaggro/Attack range)
///   EnemyAnimator.cs  -> animación y orientación del sprite
///
/// Todos declaran "public partial class EnemyAI : CharacterBody2D" por lo
/// que el compilador los une en un solo tipo: mismo nodo, mismas señales,
/// mismos campos accesibles entre archivos.
/// </summary>
public partial class EnemyAI : CharacterBody2D
{
	// Posición vertical de la barra de vida sobre la cabeza del enemigo
	[Export] public float HealthBarOffsetY { get; set; } = -80f;

	public enum State { Patrol, Idle, Chase, PreAttack, Attack, Hurt, Stunned, Dead }

	private State _state = State.Patrol;
	public  State CurrentState => _state;

	private AnimatedSprite2D _sprite;
	private HealthSystem     _health;
	private PostureSystem    _posture;
	private Hitbox           _attackHitbox;
	private Hurtbox          _hurtbox;
	private ProgressBar      _healthBar;

	public override void _Ready()
	{
		_sprite       = GetNode<AnimatedSprite2D>("AnimatedSprite");
		_health       = GetNode<HealthSystem>("HealthSystem");
		_posture      = GetNode<PostureSystem>("PostureSystem");
		_attackHitbox = GetNodeOrNull<Hitbox>("AttackHitbox");
		_hurtbox      = GetNodeOrNull<Hurtbox>("Hurtbox");

		if (_attackHitbox == null) GD.PrintErr("[Enemy] AttackHitbox sin script");
		if (_hurtbox      == null) GD.PrintErr("[Enemy] Hurtbox sin script");

		// Calcula la duración real del ataque desde la animación
		var frames     = _sprite.SpriteFrames;
		int frameCount = frames.GetFrameCount("Attack");
		float fps      = (float)frames.GetAnimationSpeed("Attack");
		AttackDuration = (fps > 0f) ? (frameCount / fps) : 0.60f;

		_sprite.AnimationFinished += OnAnimationFinished;

		// Busca la referencia al jugador por grupo, para la detección por distancia.
		InitPlayerReference();

		// IMPORTANTE: guarda el origen DESPUÉS de que el nodo esté colocado en la escena.
		// CallDeferred garantiza que GlobalPosition ya tiene el valor final del editor.
		CallDeferred(MethodName.InitPatrolOrigin);

		// Conecta señales
		_health.Died           += OnDied;
		_health.HealthChanged  += OnHealthChanged;
		if (_hurtbox != null)
			_hurtbox.HitReceived += OnHitReceived;
		_posture.PostureBroken   += OnPostureBroken;
		_posture.PostureRestored += OnPostureRestored;

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

		// Actualiza _playerInSight / _playerInRange según distancia real al jugador
		UpdateDetection();

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

	// Cambia el estado y resetea el flag de animación terminada
	private void ChangeState(State next)
	{
		if (_state == next) return;
		if (next != State.Attack) _attackFinished = false;
		_state = next;
		UpdateAnimation();
	}

	public HealthSystem  Health  => _health;
	public PostureSystem Posture => _posture;
}
