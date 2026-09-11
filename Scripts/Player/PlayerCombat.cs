using Godot;

/// <summary>
/// Parte parcial de PlayerController: los tres ataques del jugador.
///   Attack        (clic izquierdo) onda con proyectil, usa AttackHitbox
///   AttackB       (clic derecho)   cuerpo a cuerpo corto, usa AttackHitboxB
///   NormalAttack  (normal_attack)  ataque basico, usa AttackHitboxB, y es
///                 tambien el que se dispara siempre que se ataca en el aire
///                 (attack/attack_b se redirigen a este cuando IsOnFloor() es false)
/// </summary>
public partial class PlayerController
{
	// ATTACK onda con proyectil hitbox larga
	[ExportGroup("Combat - Attack (onda)")]
	[Export] public float AttackDuration { get; set; } = 0.35f;
	[Export] public float AttackHitStart { get; set; } = 0.08f;
	[Export] public float AttackHitEnd   { get; set; } = 0.25f;

	// ATTACKB cuerpo a cuerpo hitbox corta
	[ExportGroup("Combat - AttackB (cuerpo a cuerpo)")]
	[Export] public float AttackBDuration { get; set; } = 0.35f;
	[Export] public float AttackBHitStart { get; set; } = 0.08f;
	[Export] public float AttackBHitEnd   { get; set; } = 0.25f;

	// NORMALATTACK ataque basico comparte hitbox corta con AttackB
	[ExportGroup("Combat - NormalAttack")]
	[Export] public float NormalAttackDuration { get; set; } = 0.35f;
	[Export] public float NormalAttackHitStart { get; set; } = 0.08f;
	[Export] public float NormalAttackHitEnd   { get; set; } = 0.25f;

	// TIMERS
	private float _attackTimer;
	private float _attackBTimer;
	private float _normalAttackTimer;
	private bool  _attackFinished = false;  // true cuando AnimationFinished dispara

	// ESTADOS

	/// <summary>
	/// Ataque onda dispara proyectil al inicio y activa AttackHitbox
	/// durante la ventana AttackHitStart a AttackHitEnd
	/// en el suelo el jugador se frena gradualmente en el aire conserva
	/// su velocidad horizontal y sigue el arco del salto
	/// </summary>
	private void StateAttacking(float dt)
	{
		ApplyGravity(dt);
		float velX = IsOnFloor()
			? Mathf.MoveToward(Velocity.X, 0, MoveSpeed * dt * 6)
			: Velocity.X;
		Velocity      = new Vector2(velX, Velocity.Y);
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
	/// Ataque cuerpo a cuerpo activa AttackHitboxB corta
	/// durante la ventana AttackBHitStart a AttackBHitEnd
	/// en el suelo el jugador se frena gradualmente en el aire conserva
	/// su velocidad horizontal y sigue el arco del salto
	/// </summary>
	private void StateAttackingB(float dt)
	{
		ApplyGravity(dt);
		float velX = IsOnFloor()
			? Mathf.MoveToward(Velocity.X, 0, MoveSpeed * dt * 6)
			: Velocity.X;
		Velocity       = new Vector2(velX, Velocity.Y);
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
	/// Ataque normal basico activa AttackHitboxB corta igual que AttackB
	/// pero con su propia animacion "NormalAttack" y timers independientes
	/// es el unico ataque que se reproduce cuando se ataca en el aire en ese
	/// caso no se frena la velocidad horizontal para que el personaje siga
	/// el arco del salto en vez de caer en seco al atacar
	/// </summary>
	private void StateNormalAttacking(float dt)
	{
		ApplyGravity(dt);
		float velX = IsOnFloor()
			? Mathf.MoveToward(Velocity.X, 0, MoveSpeed * dt * 6)
			: Velocity.X;
		Velocity             = new Vector2(velX, Velocity.Y);
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

	// INICIO DE ACCIONES

	/// <summary>
	/// Ataque onda calcula duracion desde animacion "Attack" ajusta ventana
	/// del hitbox a los ultimos 2 frames y dispara un proyectil desde el spawn
	/// solo se llama estando en el suelo en el aire se usa BeginNormalAttack
	/// </summary>
	private void BeginAttack()
	{
		_attackTimer    = 0;
		_attackFinished = false;

		var frames     = _sprite.SpriteFrames;
		int frameCount = frames.GetFrameCount("Attack");
		float fps      = (float)frames.GetAnimationSpeed("Attack");
		AttackDuration = fps > 0f ? frameCount / fps : 0.60f;

		// hitbox activo en los ultimos 2 frames ajusta el "2f" si queres mas o menos
		float frameDuration = fps > 0f ? 1f / fps : 0.08f;
		AttackHitStart = AttackDuration - (frameDuration * 2f);
		AttackHitEnd   = AttackDuration - (frameDuration * 0.5f);

		// dispara proyectil desde el Marker2D o desde offset fijo como fallback
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

	/// <summary>
	/// Ataque cuerpo a cuerpo calcula duracion desde animacion "AttackB"
	/// solo se llama estando en el suelo en el aire se usa BeginNormalAttack
	/// </summary>
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
	/// Ataque normal basico calcula duracion desde animacion "NormalAttack"
	/// comparte AttackHitboxB con AttackB pero tiene su propia animacion y timers
	/// es tambien el ataque que se usa siempre que se ataca en el aire
	/// </summary>
	private void BeginNormalAttack()
	{
		_normalAttackTimer = 0;
		_attackFinished    = false;
		var frames         = _sprite.SpriteFrames;
		int frameCount     = frames.GetFrameCount("NormalAttack");
		float fps          = (float)frames.GetAnimationSpeed("NormalAttack");
		NormalAttackDuration = fps > 0f ? frameCount / fps : 0.35f;

		// hitbox activo en los ultimos 2 frames igual que los otros ataques
		float frameDuration  = fps > 0f ? 1f / fps : 0.08f;
		NormalAttackHitStart = NormalAttackDuration - (frameDuration * 2f);
		NormalAttackHitEnd   = NormalAttackDuration - (frameDuration * 0.5f);

		ChangeState(State.NormalAttacking);
	}
}
