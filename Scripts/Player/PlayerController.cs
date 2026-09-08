using Godot;

/// <summary>
/// Controlador principal del jugador. Maneja movimiento, combate y animaciones.
/// Attack        (clic izquierdo)  → onda con proyectil — usa AttackHitbox.
/// AttackB       (clic derecho)    → cuerpo a cuerpo corto — usa AttackHitboxB.
/// NormalAttack  (normal_attack)   → ataque normal básico — usa AttackHitboxB.
/// Parry         (Q / "parry_")    → refleja ataques en frames 4-7 de la animación.
///
/// Idle: siempre reproduce "NormalIdle" en loop mientras el jugador está quieto.
///
/// Escala por animación para compensar diferencias de tamaño entre sprites:
///   - Base: NormalIdle (1.038, 1.0)
///   - Attack 2px más grande, AttackB 2px menos, Death 4px más grande.
/// </summary>
public partial class PlayerController : CharacterBody2D
{
	// MOVIMIENTO
	[ExportGroup("Movement")]
	[Export] public float MoveSpeed { get; set; } = 180f;
	[Export] public float JumpForce { get; set; } = -400f;

	// DODGE
	[ExportGroup("Dodge")]
	[Export] public float DodgeSpeed    { get; set; } = 360f;
	[Export] public float DodgeDuration { get; set; } = 0.25f;
	[Export] public float DodgeCooldown { get; set; } = 0.8f;
	[Export] public float DodgeIFrames  { get; set; } = 0.20f;

	// ATTACK — onda con proyectil (hitbox larga)
	[ExportGroup("Combat - Attack (onda)")]
	[Export] public float AttackDuration { get; set; } = 0.35f;
	[Export] public float AttackHitStart { get; set; } = 0.08f;
	[Export] public float AttackHitEnd   { get; set; } = 0.25f;

	// ATTACKB — cuerpo a cuerpo (hitbox corta)
	[ExportGroup("Combat - AttackB (cuerpo a cuerpo)")]
	[Export] public float AttackBDuration { get; set; } = 0.35f;
	[Export] public float AttackBHitStart { get; set; } = 0.08f;
	[Export] public float AttackBHitEnd   { get; set; } = 0.25f;

	// NORMALATTACK — ataque básico (comparte hitbox corta con AttackB)
	[ExportGroup("Combat - NormalAttack")]
	[Export] public float NormalAttackDuration { get; set; } = 0.35f;
	[Export] public float NormalAttackHitStart { get; set; } = 0.08f;
	[Export] public float NormalAttackHitEnd   { get; set; } = 0.25f;

	// PARRY — ventana activa en frames 4-7 de la animación de 9 frames
	[ExportGroup("Parry")]
	[Export] public float ParryWindowStart { get; set; } = 0.13f;  // inicio frame 4
	[Export] public float ParryWindowEnd   { get; set; } = 0.46f;  // fin frame 7
	[Export] public float ParryDuration    { get; set; } = 0.60f;  // duración total
	[Export] public float ParryCooldown    { get; set; } = 0.60f;
	[Export] public float ParryPostureDmg  { get; set; } = 40f;
	[Export] public float ParryFreezeTime  { get; set; } = 0.12f;

	// DAÑO RECIBIDO
	[ExportGroup("Hurt")]
	[Export] public float HurtDuration   { get; set; } = 0.40f;
	[Export] public float KnockbackForce { get; set; } = 220f;

	// SPRITE — offsets de posición por dirección
	[ExportGroup("Sprite")]
	[Export] public float SpriteOffsetRight { get; set; } = 0f;
	[Export] public float SpriteOffsetLeft  { get; set; } = 0f;

	// ESTADOS
	public enum State
	{
		Idle, Running, Jumping, Falling,
		Attacking, AttackingB, NormalAttacking,
		Dodging, Parrying, Hurt, Dead
	}

	private State _state = State.Idle;
	public  State CurrentState => _state;

	// TIMERS — uno por acción con duración propia
	private float _attackTimer;
	private float _attackBTimer;
	private float _normalAttackTimer;
	private float _dodgeTimer;
	private float _dodgeCdTimer;
	private float _parryTimer;
	private float _parryCdTimer;
	private float _hurtTimer;
	private float _hitFreezeTimer;  // congela el frame tras parry exitoso

	// FLAGS Y DIRECCIÓN
	private int   _facingDir      = 1;
	private float _dodgeDirX      = 1f;
	private bool  _parryActive    = false;  // true solo durante frames 4-7 del parry
	private bool  _attackFinished = false;  // true cuando AnimationFinished dispara

	// REFERENCIAS A NODOS
	private AnimatedSprite2D _sprite;
	private HealthSystem     _health;
	private PostureSystem    _posture;
	private Hitbox           _attackHitbox;   // onda — hitbox LARGA
	private Hitbox           _attackHitboxB;  // cuerpo a cuerpo — hitbox CORTA
	private Hurtbox          _hurtbox;
	private Area2D           _parryBox;
	private Marker2D         _projectileSpawn; // punto de spawn del proyectil (punta de la espada)

	// Proyectil — cargado una vez al inicio, ajusta la ruta si cambias de carpeta
	private static readonly PackedScene ProjectileScene =
		GD.Load<PackedScene>("res://Scenes/Attacks/Projectile.tscn");

	private float Gravity => ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

	// GODOT LIFECYCLE

	/// <summary>Inicializa nodos y conecta señales.</summary>
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
		_sprite.Offset = new Vector2(-1f, 0f);
	}

	/// <summary>Lógica principal por frame físico.</summary>
	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		// Congela todo durante el freeze de parry exitoso
		if (_hitFreezeTimer > 0f) { _hitFreezeTimer -= dt; return; }

		if (_dodgeCdTimer > 0f) _dodgeCdTimer -= dt;
		if (_parryCdTimer > 0f) _parryCdTimer -= dt;

		// Inputs de ataque — se leen antes del switch para capturar clicks
		// en cualquier estado (suelo o aire) sin perder ninguno
		bool canAttack = _state != State.Attacking       &&
						 _state != State.AttackingB       &&
						 _state != State.NormalAttacking  &&
						 _state != State.Hurt             &&
						 _state != State.Dead             &&
						 _state != State.Dodging          &&
						 _state != State.Parrying;

		if (canAttack && Input.IsActionJustPressed("attack"))
		{ BeginAttack(); MoveAndSlide(); return; }

		if (canAttack && Input.IsActionJustPressed("attack_b"))
		{ BeginAttackB(); MoveAndSlide(); return; }

		if (canAttack && Input.IsActionJustPressed("normal_attack"))
		{ BeginNormalAttack(); MoveAndSlide(); return; }

		switch (_state)
		{
			case State.Idle:            StateIdle(dt);            break;
			case State.Running:         StateRunning(dt);         break;
			case State.Jumping:         StateJumping(dt);         break;
			case State.Falling:         StateFalling(dt);         break;
			case State.Attacking:       StateAttacking(dt);       break;
			case State.AttackingB:      StateAttackingB(dt);      break;
			case State.NormalAttacking: StateNormalAttacking(dt); break;
			case State.Dodging:         StateDodging(dt);         break;
			case State.Parrying:        StateParrying(dt);        break;
			case State.Hurt:            StateHurt(dt);            break;
		}

		MoveAndSlide();
	}

	// ESTADOS

	/// <summary>Jugador quieto — reproduce "NormalIdle" en loop.</summary>
	private void StateIdle(float dt)
	{
		ApplyGravity(dt);

		if (TryTransitionFromGrounded()) return;

		if (_state != State.Idle) return;

		Velocity = new Vector2(0, Velocity.Y);

		if (_sprite.Animation != "NormalIdle")
			_sprite.Play("NormalIdle");
	}

	/// <summary>Jugador moviéndose horizontalmente.</summary>
	private void StateRunning(float dt)
	{
		ApplyGravity(dt);
		float dir = GetMoveInput();
		Velocity  = new Vector2(dir * MoveSpeed, Velocity.Y);
		if (dir == 0) ChangeState(State.Idle);
		else TryTransitionFromGrounded();
	}

	/// <summary>Jugador en el aire subiendo — pasa a Falling cuando Y empieza a bajar.</summary>
	private void StateJumping(float dt)
	{
		ApplyGravity(dt);
		Velocity = new Vector2(GetMoveInput() * MoveSpeed, Velocity.Y);
		if (Velocity.Y > 0) ChangeState(State.Falling);
	}

	/// <summary>Jugador cayendo — vuelve a Idle al tocar el suelo.</summary>
	private void StateFalling(float dt)
	{
		ApplyGravity(dt);
		Velocity = new Vector2(GetMoveInput() * MoveSpeed, Velocity.Y);
		if (IsOnFloor()) ChangeState(State.Idle);
	}

	/// <summary>
	/// Ataque onda — dispara proyectil al inicio y activa AttackHitbox
	/// durante la ventana AttackHitStart → AttackHitEnd.
	/// El jugador se frena gradualmente mientras ataca.
	/// </summary>
	private void StateAttacking(float dt)
	{
		ApplyGravity(dt);
		Velocity      = new Vector2(Mathf.MoveToward(Velocity.X, 0, MoveSpeed * dt * 6), Velocity.Y);
		_attackTimer += dt;

		if (_attackTimer >= AttackHitStart && _attackTimer < AttackHitEnd)
			_attackHitbox?.Activate();
		if (_attackTimer >= AttackHitEnd)
			_attackHitbox?.Deactivate();

		if (_attackFinished || _attackTimer >= AttackDuration)
		{
			_attackTimer    = 0;
			_attackFinished = false;
			_attackHitbox?.Deactivate();
			ChangeState(IsOnFloor() ? State.Idle : State.Falling);
		}
	}

	/// <summary>
	/// Ataque cuerpo a cuerpo — activa AttackHitboxB (corta)
	/// durante la ventana AttackBHitStart → AttackBHitEnd.
	/// </summary>
	private void StateAttackingB(float dt)
	{
		ApplyGravity(dt);
		Velocity       = new Vector2(Mathf.MoveToward(Velocity.X, 0, MoveSpeed * dt * 6), Velocity.Y);
		_attackBTimer += dt;

		if (_attackBTimer >= AttackBHitStart && _attackBTimer < AttackBHitEnd)
			_attackHitboxB?.Activate();
		if (_attackBTimer >= AttackBHitEnd)
			_attackHitboxB?.Deactivate();

		if (_attackFinished || _attackBTimer >= AttackBDuration)
		{
			_attackBTimer   = 0;
			_attackFinished = false;
			_attackHitboxB?.Deactivate();
			ChangeState(IsOnFloor() ? State.Idle : State.Falling);
		}
	}

	/// <summary>
	/// Ataque normal básico — activa AttackHitboxB (corta) igual que AttackB
	/// pero con su propia animación "NormalAttack" y timers independientes.
	/// </summary>
	private void StateNormalAttacking(float dt)
	{
		ApplyGravity(dt);
		Velocity             = new Vector2(Mathf.MoveToward(Velocity.X, 0, MoveSpeed * dt * 6), Velocity.Y);
		_normalAttackTimer  += dt;

		if (_normalAttackTimer >= NormalAttackHitStart && _normalAttackTimer < NormalAttackHitEnd)
			_attackHitboxB?.Activate();
		if (_normalAttackTimer >= NormalAttackHitEnd)
			_attackHitboxB?.Deactivate();

		if (_attackFinished || _normalAttackTimer >= NormalAttackDuration)
		{
			_normalAttackTimer = 0;
			_attackFinished    = false;
			_attackHitboxB?.Deactivate();
			ChangeState(IsOnFloor() ? State.Idle : State.Falling);
		}
	}

	/// <summary>
	/// Parry — ventana activa en frames 4-7 de la animación de 9 frames.
	/// Timeline:
	///   0s               → animación empieza (frames 1-3, anticipación)
	///   ParryWindowStart → ParryBox se activa (frame 4)
	///   ParryWindowEnd   → ParryBox se desactiva (después frame 7)
	///   ParryDuration    → estado termina, vuelve a Idle
	/// </summary>
	private void StateParrying(float dt)
	{
		Velocity     = new Vector2(0, Velocity.Y);
		ApplyGravity(dt);
		_parryTimer += dt;

		// Abre la ventana de parry en frame 4
		if (_parryTimer >= ParryWindowStart && _parryTimer < ParryWindowEnd && !_parryActive)
		{
			_parryActive          = true;
			_parryBox.Monitoring  = true;
			_parryBox.Monitorable = true;
		}

		// Cierra la ventana de parry en frame 8 (recuperación)
		if (_parryTimer >= ParryWindowEnd && _parryActive)
		{
			_parryActive          = false;
			_parryBox.Monitoring  = false;
			_parryBox.Monitorable = false;
		}

		// Termina el estado al completar los 9 frames
		if (_parryTimer >= ParryDuration)
		{
			_parryTimer = 0;
			ChangeState(State.Idle);
		}
	}

	/// <summary>Dodge con iframes activos — el jugador es invencible durante DodgeIFrames.</summary>
	private void StateDodging(float dt)
	{
		ApplyGravity(dt);
		Velocity     = new Vector2(_dodgeDirX * DodgeSpeed, Velocity.Y);
		_dodgeTimer += dt;

		if (_dodgeTimer >= DodgeDuration)
		{
			_dodgeTimer = 0;
			_hurtbox.SetInvincible(0);
			ChangeState(IsOnFloor() ? State.Idle : State.Falling);
		}
	}

	/// <summary>
	/// Recibió daño — aplica knockback decreciente y espera HurtDuration
	/// antes de volver a Idle o Dead.
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
	/// Godot llama esto cuando una animación sin loop termina.
	/// Maneja: fin de ataque, congelado en muerte.
	/// </summary>
	private void OnAnimationFinished()
	{
		// Fin de cualquier ataque — el state lo procesa en el siguiente frame
		if (_state == State.Attacking      ||
			_state == State.AttackingB     ||
			_state == State.NormalAttacking)
		{
			_attackFinished = true;
			return;
		}

		// Muerte — congela en el último frame hasta que el jugador reinicie
		if (_state == State.Dead)
		{
			_sprite.Pause();
			return;
		}
	}

	// TRANSICIONES E INPUTS

	/// <summary>
	/// Lee inputs de suelo (parry, dodge, jump, movimiento).
	/// Los ataques se leen globalmente en _PhysicsProcess para no perder clicks.
	/// Devuelve true si hubo transición.
	/// </summary>
	private bool TryTransitionFromGrounded()
	{
		float dir = GetMoveInput();

		// Parry — solo en suelo y con cooldown disponible (tecla Q → "parry_")
		if (Input.IsActionJustPressed("parry_") && _parryCdTimer <= 0 && IsOnFloor())
		{ BeginParry(); return true; }

		if (Input.IsActionJustPressed("dodge") && _dodgeCdTimer <= 0)
		{ BeginDodge(); return true; }

		if (Input.IsActionJustPressed("Jump") && IsOnFloor())
		{
			Velocity = new Vector2(Velocity.X, JumpForce);
			ChangeState(State.Jumping);
			return true;
		}

		if (!IsOnFloor() && Velocity.Y > 0)
		{ ChangeState(State.Falling); return true; }

		if (dir != 0)
		{ ChangeState(State.Running); return true; }

		return false;
	}

	// INICIO DE ACCIONES

	/// <summary>
	/// Ataque onda — calcula duración desde animación "Attack", ajusta ventana
	/// del hitbox a los últimos 2 frames, y dispara un proyectil desde el spawn.
	/// </summary>
	private void BeginAttack()
	{
		_attackTimer    = 0;
		_attackFinished = false;

		var frames     = _sprite.SpriteFrames;
		int frameCount = frames.GetFrameCount("Attack");
		float fps      = (float)frames.GetAnimationSpeed("Attack");
		AttackDuration = fps > 0f ? frameCount / fps : 0.60f;

		// Hitbox activo en los últimos 2 frames — ajusta el "2f" si quieres más o menos
		float frameDuration = fps > 0f ? 1f / fps : 0.08f;
		AttackHitStart = AttackDuration - (frameDuration * 2f);
		AttackHitEnd   = AttackDuration - (frameDuration * 0.5f);

		// Dispara proyectil desde el Marker2D o desde offset fijo como fallback
		if (ProjectileScene != null)
		{
			var projectile            = ProjectileScene.Instantiate<Projectile>();
			projectile.Direction      = _facingDir;
			projectile.GlobalPosition = _projectileSpawn != null
				? _projectileSpawn.GlobalPosition
				: GlobalPosition + new Vector2(_facingDir * 30f, -10f);
			GetParent().AddChild(projectile);
		}

		ChangeState(State.Attacking);
	}

	/// <summary>Ataque cuerpo a cuerpo — calcula duración desde animación "AttackB".</summary>
	private void BeginAttackB()
	{
		_attackBTimer   = 0;
		_attackFinished = false;
		var frames      = _sprite.SpriteFrames;
		int frameCount  = frames.GetFrameCount("AttackB");
		float fps       = (float)frames.GetAnimationSpeed("AttackB");
		AttackBDuration = fps > 0f ? frameCount / fps : 0.35f;
		ChangeState(State.AttackingB);
	}

	/// <summary>
	/// Ataque normal básico — calcula duración desde animación "NormalAttack".
	/// Comparte AttackHitboxB con AttackB pero tiene su propia animación y timers.
	/// </summary>
	private void BeginNormalAttack()
	{
		_normalAttackTimer = 0;
		_attackFinished    = false;
		var frames         = _sprite.SpriteFrames;
		int frameCount     = frames.GetFrameCount("NormalAttack");
		float fps          = (float)frames.GetAnimationSpeed("NormalAttack");
		NormalAttackDuration = fps > 0f ? frameCount / fps : 0.35f;

		// Hitbox activo en los últimos 2 frames igual que los otros ataques
		float frameDuration  = fps > 0f ? 1f / fps : 0.08f;
		NormalAttackHitStart = NormalAttackDuration - (frameDuration * 2f);
		NormalAttackHitEnd   = NormalAttackDuration - (frameDuration * 0.5f);

		ChangeState(State.NormalAttacking);
	}

	/// <summary>
	/// Parry — calcula ventana activa automáticamente desde los frames
	/// de la animación "Parry" (frames 4-7 son la ventana activa).
	/// </summary>
	private void BeginParry()
	{
		_parryTimer   = 0;
		_parryActive  = false;
		_parryCdTimer = ParryCooldown;

		var frames = _sprite.SpriteFrames;
		if (frames.HasAnimation("Parry"))
		{
			float fps      = (float)frames.GetAnimationSpeed("Parry");
			int   total    = frames.GetFrameCount("Parry");
			float frameDur = fps > 0f ? 1f / fps : 0.066f;

			// Ventana activa: frames 4-7 (índice base 0 → 3 al 6)
			ParryWindowStart = frameDur * 3f;
			ParryWindowEnd   = frameDur * 7f;
			ParryDuration    = fps > 0f ? total / fps : 0.60f;
		}

		ChangeState(State.Parrying);
	}

	/// <summary>Dodge — sale en la dirección del input o hacia donde mira si no hay input.</summary>
	private void BeginDodge()
	{
		float dir     = GetMoveInput();
		_dodgeDirX    = dir != 0 ? dir : _facingDir;
		_dodgeTimer   = 0;
		_dodgeCdTimer = DodgeCooldown;
		_hurtbox.SetInvincible(DodgeIFrames);
		ChangeState(State.Dodging);
	}

	// CALLBACKS DE SEÑALES

	/// <summary>
	/// El hurtbox recibió un golpe. Cancela hitboxes activos, aplica knockback
	/// y pasa al estado Hurt.
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

	/// <summary>La salud llegó a cero — desactiva hitboxes y pasa a Dead.</summary>
	private void OnDied()
	{
		_attackHitbox?.Deactivate();
		_attackHitboxB?.Deactivate();
		_parryBox.Monitoring = false;
		ChangeState(State.Dead);
	}

	/// <summary>
	/// ParryBox tocó un hitbox enemigo durante la ventana activa (frames 4-7).
	/// Aplica daño de postura al enemigo, otorga iframes y congela el frame.
	/// Filtra hitboxes propios para evitar auto-parry.
	/// </summary>
	private void OnParryBoxAreaEntered(Area2D area)
	{
		if (!_parryActive) return;
		if (area is not Hitbox enemyHitbox) return;
		if (enemyHitbox.OwnerTag == "player") return;

		var posture = enemyHitbox.GetParent().GetNodeOrNull<PostureSystem>("PostureSystem");
		posture?.AddParryPostureDamage(ParryPostureDmg);
		enemyHitbox.Deactivate();
		_hurtbox.SetInvincible(0.3f);
		_hitFreezeTimer = ParryFreezeTime;
		_sprite.Play("NormalIdle");
	}

	// HELPERS

	/// <summary>Cambia estado y actualiza animación.</summary>
	private void ChangeState(State next)
	{
		if (_state == next) return;
		_state = next;
		UpdateAnimation();
	}

	/// <summary>
	/// Reproduce la animación y ajusta la escala del sprite según el estado.
	/// La escala base es NormalIdle (1.038, 1.0) — cada animación se ajusta
	/// proporcionalmente para compensar diferencias de tamaño entre sprites.
	/// Ajusta los valores de offset Y si alguna animación queda más arriba o abajo.
	/// </summary>
	private void UpdateAnimation()
	{
		if (_sprite == null) return;

		string anim = _state switch
		{
			State.Idle            => "NormalIdle",
			State.Running         => "Running",
			State.Jumping         => "Jump",
			State.Falling         => "Jump",
			State.Attacking       => "Attack",
			State.AttackingB      => "AttackB",
			State.NormalAttacking => "NormalAttack",
			State.Dodging         => "Running",
			State.Parrying        => "Parry",
			State.Hurt            => "Hurt",
			State.Dead            => "Death",
			_                     => "NormalIdle"
		};

		if (_sprite.Animation != anim)
		{
			_sprite.Play(anim);

			// Escala por animación — base NormalIdle (1.038, 1.0)
			// Attack:       +2px más grande
			// AttackB:      -2px más pequeño
			// NormalAttack: base (igual que NormalIdle)
			// Death:        +4px más grande
			// Jump:         misma escala base, con offset Y ajustado más arriba
			_sprite.Scale = anim switch
			{
				"Attack"       => new Vector2(1.190f, 1.190f),
				"AttackB"      => new Vector2(1.150f, 1.150f),
				"NormalAttack" => new Vector2(1.038f, 1.0f),
				"Death"        => new Vector2(1.078f, 1.04f),
				"Jump"         => new Vector2(1.038f, 1.0f),
				"Running"      => new Vector2(1.040f, 1.040f),
				"Parry"        => new Vector2(1.100f, 1.100f),
				_              => new Vector2(1.038f, 1.0f)  // NormalIdle, Running, etc.
			};

			// Offset Y por animación — sube el sprite si queda más abajo que NormalIdle
			// Ajusta estos valores hasta que todas las animaciones queden a la misma altura
			_sprite.Offset = new Vector2(
				_facingDir < 0 ? -15f : -1f,
				anim switch
				{
					"Jump" => -5f,   // sube un poco
					"Running" => -4f,
					"Parry" => -5f,
					"NormalAttack" => -3f,
					"AttackB" => -10f,
					"Attack" => 1f, //baja
					_      => 0f
				}
			);
		}
	}

	/// <summary>
	/// Lee input horizontal, orienta el sprite y devuelve el eje (-1, 0, 1).
	/// FlipH voltea el sprite y el offset compensa el pivot al cambiar dirección.
	/// </summary>
	private float GetMoveInput()
	{
		float axis = 0;
		if (Input.IsActionPressed("move_right")) axis =  1;
		if (Input.IsActionPressed("move_left"))  axis = -1;

		if (axis != 0)
		{
			_facingDir = (int)axis;
			if (_sprite != null)
			{
				_sprite.FlipH  = axis < 0;
				_sprite.Offset = new Vector2(axis < 0 ? -15f : -1f, _sprite.Offset.Y);
			}
		}

		return axis;
	}

	/// <summary>Aplica la gravedad del proyecto. Resetea Y al tocar el suelo.</summary>
	private void ApplyGravity(float dt)
	{
		if (!IsOnFloor())
			Velocity = new Vector2(Velocity.X, Velocity.Y + Gravity * dt);
		else if (Velocity.Y > 0)
			Velocity = new Vector2(Velocity.X, 0);
	}

	// PROPIEDADES PÚBLICAS

	public HealthSystem  Health            => _health;
	public PostureSystem Posture           => _posture;
	public bool          ParryWindowActive => _parryActive;
}