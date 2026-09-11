using Godot;

/// <summary>
/// Controlador principal del jugador (núcleo) maneja el ciclo de vida de Godot,
/// las referencias a nodos, la máquina de estados central y el loop de física.
/// El resto de la lógica vive repartida en los demás archivos parciales:
///   PlayerMovement.cs -> Idle / Running / Jumping / Falling e input de movimiento
///   PlayerCombat.cs   -> Attack / AttackB / NormalAttack
///   PlayerDash.cs     -> Dash
///   PlayerParry.cs    -> Parry
///   PlayerAnimator.cs -> animación, escalas y offsets de sprite
///
/// Todos declaran "public partial class PlayerController : CharacterBody2D"
/// por lo que el compilador los une en un solo tipo: mismo nodo, mismas
/// señales, mismos campos accesibles entre archivos.
/// </summary>
public partial class PlayerController : CharacterBody2D
{
	// DANIO RECIBIDO knockback y duracion del stun al ser golpeado
	[ExportGroup("Hurt")]
	[Export] public float HurtDuration   { get; set; } = 0.40f;
	[Export] public float KnockbackForce { get; set; } = 220f;

	// ESTADOS todas las maquinas de estado del jugador viven aca
	public enum State
	{
		Idle, Running, Jumping, Falling,
		Attacking, AttackingB, NormalAttacking,
		Dashing, Parrying, Hurt, Dead
	}

	private State _state = State.Idle;
	public  State CurrentState => _state;

	private float _hurtTimer;
	private float _hitFreezeTimer;  // congela el frame tras parry exitoso

	// REFERENCIAS A NODOS todo lo que se cachea una vez en Ready
	private AnimatedSprite2D _sprite;
	private HealthSystem     _health;
	private PostureSystem    _posture;
	private Hitbox           _attackHitbox;   // onda hitbox LARGA
	private Hitbox           _attackHitboxB;  // cuerpo a cuerpo hitbox CORTA
	private Hurtbox          _hurtbox;
	private Area2D           _parryBox;
	private Marker2D         _projectileSpawn; // punto de spawn del proyectil punta de la espada

	// Proyectil cargado una sola vez al inicio ajusta la ruta si cambias de carpeta
	private static readonly PackedScene ProjectileScene =
		GD.Load<PackedScene>("res://Scenes/Attacks/Projectile.tscn");

	private float Gravity => ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

	// GODOT LIFECYCLE

	/// <summary>Inicializa nodos y conecta senales</summary>
	public override void _Ready()
	{
		_sprite          = GetNode<AnimatedSprite2D>("AnimatedSprite");
		_health          = GetNode<HealthSystem>("HealthSystem");
		_posture         = GetNode<PostureSystem>("PostureSystem");
		_attackHitbox    = GetNode<Hitbox>("AttackHitbox");
		_attackHitboxB   = GetNode<Hitbox>("AttackHitboxB");
		_hurtbox         = GetNode<Hurtbox>("Hurtbox");
		_parryBox        = GetNode<Area2D>("ParryBox");
		_projectileSpawn = GetNodeOrNull<Marker2D>("ProjectileSpawn");


		if (_attackHitbox  == null) GD.PrintErr("[Player] AttackHitbox NO encontrado!");
		if (_attackHitboxB == null) GD.PrintErr("[Player] AttackHitboxB NO encontrado!");
		if (_hurtbox       == null) GD.PrintErr("[Player] Hurtbox NO encontrado!");
		if (_parryBox      == null) GD.PrintErr("[Player] ParryBox NO encontrado!");

		_health.Died              += OnDied;
		_hurtbox.HitReceived      += OnHitReceived;
		_parryBox.AreaEntered     += OnParryBoxAreaEntered;
		_sprite.AnimationFinished += OnAnimationFinished;

		_parryBox.Monitoring  = false;
		_parryBox.Monitorable = false;

		ApplyPendingSpawnPoint();

		ApplySpriteOffset();
	}

	// busca el punto de spawn pendiente que dejo la escena anterior y teletransporta al jugador
	private void ApplyPendingSpawnPoint()
	{
		if (SceneTransition.Instance == null) return;

		string spawnName = SceneTransition.Instance.ConsumePendingSpawnPoint();
		if (string.IsNullOrEmpty(spawnName)) return;

		var spawn = GetTree().CurrentScene.FindChild(spawnName, recursive: true, owned: false) as Node2D;
		if (spawn != null)
		{
			GlobalPosition = spawn.GlobalPosition;
			GD.Print($"[Player] Teletransportado a '{spawnName}' (path: {spawn.GetPath()}) en {GlobalPosition}");
			GD.Print($"[Player] CurrentScene es: {GetTree().CurrentScene.Name} (path: {GetTree().CurrentScene.SceneFilePath})");
		}
		else
			GD.PrintErr($"[Player] Spawn point '{spawnName}' no encontrado en la escena.");
	}

	/// <summary>Logica principal por frame fisico</summary>
	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		GD.Print($"[Player] Pos={GlobalPosition} OnFloor={IsOnFloor()} VelY={Velocity.Y}");

		// congela todo durante el freeze de parry exitoso ni siquiera lee inputs
		if (_hitFreezeTimer > 0f) { _hitFreezeTimer -= dt; return; }

		// descuenta cooldowns de todas las acciones especiales cada frame
		if (_parryCdTimer > 0f) _parryCdTimer -= dt;
		if (_dashCdTimer  > 0f) _dashCdTimer  -= dt;

		// inputs de ataque se leen antes del switch para capturar clicks
		// en cualquier estado suelo o aire sin perder ninguno
		bool canAttack = _state != State.Attacking       &&
						 _state != State.AttackingB       &&
						 _state != State.NormalAttacking  &&
						 _state != State.Hurt             &&
						 _state != State.Dead             &&
						 _state != State.Dashing          &&
						 _state != State.Parrying;

		// en el aire cualquier ataque Attack o AttackB se convierte en
		// NormalAttack misma animacion siempre sin importar que boton se uso
		if (canAttack && Input.IsActionJustPressed("attack"))
		{
			if (IsOnFloor()) BeginAttack();
			else              BeginNormalAttack();
			ApplySpriteOffset();
			MoveAndSlide();
			return;
		}

		if (canAttack && Input.IsActionJustPressed("attack_b"))
		{
			if (IsOnFloor()) BeginAttackB();
			else              BeginNormalAttack();
			ApplySpriteOffset();
			MoveAndSlide();
			return;
		}

		if (canAttack && Input.IsActionJustPressed("normal_attack"))
		{ BeginNormalAttack(); ApplySpriteOffset(); MoveAndSlide(); return; }

		// dash se lee tambien de forma global igual que los ataques asi no se
		// pierde el input aunque el jugador este saltando o cayendo
		if (canAttack && _dashCdTimer <= 0f && Input.IsActionJustPressed("dash"))
		{ BeginDash(); ApplySpriteOffset(); MoveAndSlide(); return; }

		switch (_state)
		{
			case State.Idle:            StateIdle(dt);            break;
			case State.Running:         StateRunning(dt);         break;
			case State.Jumping:         StateJumping(dt);         break;
			case State.Falling:         StateFalling(dt);         break;
			case State.Attacking:       StateAttacking(dt);       break;
			case State.AttackingB:      StateAttackingB(dt);      break;
			case State.NormalAttacking: StateNormalAttacking(dt); break;
			case State.Dashing:         StateDashing(dt);         break;
			case State.Parrying:        StateParrying(dt);        break;
			case State.Hurt:            StateHurt(dt);            break;
		}

		ApplySpriteOffset();
		MoveAndSlide();
	}

	/// <summary>
	/// Recibio danio aplica knockback decreciente y espera HurtDuration
	/// antes de volver a Idle o Dead
	/// </summary>
	private void StateHurt(float dt)
	{
		ApplyGravity(dt);
		Velocity    = new Vector2(Mathf.MoveToward(Velocity.X, 0, KnockbackForce * dt * 4), Velocity.Y);
		_hurtTimer += dt;

		if (_hurtTimer >= HurtDuration)
		{
			_hurtTimer = 0;
			ChangeState(_health.IsDead ? State.Dead : State.Idle);
		}
	}

	// ANIMATION FINISHED

	/// <summary>
	/// Godot llama esto cuando una animacion sin loop termina
	/// maneja fin de ataque y congelado en muerte
	/// </summary>
	private void OnAnimationFinished()
	{
		// fin de cualquier ataque el state lo procesa en el siguiente frame
		if (_state == State.Attacking      ||
			_state == State.AttackingB     ||
			_state == State.NormalAttacking)
		{
			_attackFinished = true;
			return;
		}

		// muerte congela en el ultimo frame hasta que el jugador reinicie
		if (_state == State.Dead)
		{
			_sprite.Pause();
			return;
		}
	}

	// CALLBACKS DE SENIALES

	/// <summary>
	/// El hurtbox recibio un golpe cancela hitboxes activos aplica knockback
	/// y pasa al estado Hurt
	/// </summary>
	private void OnHitReceived(float damage, Vector2 hitPos)
	{
		if (_state == State.Dead) return;
		float dir  = GlobalPosition.X < hitPos.X ? -1 : 1;
		Velocity   = new Vector2(dir * KnockbackForce, -80);
		_hurtTimer = 0;
		_attackHitbox?.Deactivate();
		_attackHitboxB?.Deactivate();
		ChangeState(State.Hurt);
	}

	/// <summary>La salud llego a cero desactiva hitboxes y pasa a Dead</summary>
	private void OnDied()
	{
		_attackHitbox?.Deactivate();
		_attackHitboxB?.Deactivate();
		_parryBox.Monitoring = false;
		ChangeState(State.Dead);
	}

	// HELPERS

	/// <summary>Cambia estado y actualiza animacion</summary>
	private void ChangeState(State next)
	{
		if (_state == next) return;
		_state = next;
		UpdateAnimation();
	}

	/// <summary>Aplica la gravedad del proyecto resetea Y al tocar el suelo</summary>
	private void ApplyGravity(float dt)
	{
		if (!IsOnFloor())
			Velocity = new Vector2(Velocity.X, Velocity.Y + Gravity * dt);
		else if (Velocity.Y > 0)
			Velocity = new Vector2(Velocity.X, 0);
	}

	// PROPIEDADES PUBLICAS

	public HealthSystem  Health            => _health;
	public PostureSystem Posture           => _posture;
	public bool          ParryWindowActive => _parryActive;
}
