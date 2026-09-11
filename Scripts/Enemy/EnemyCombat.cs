using Godot;

/// <summary>
/// Parte parcial de EnemyAI: combate. Telegrafía el golpe (PreAttack), activa
/// el hitbox durante la ventana de ataque, y maneja recibir daño, quedar
/// stunneado por rotura de postura, y la muerte.
/// </summary>
public partial class EnemyAI
{
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

	private float _attackTimer     = 0f;
	private float _attackCdTimer   = 0f;
	private float _hurtTimer       = 0f;
	private float _preAttackTimer  = 0f;
	private bool  _attackFinished  = false;

	// Tiempo de telegrafía antes de atacar
	private const float PreAttackDuration = 0.15f;

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
}