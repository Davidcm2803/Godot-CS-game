using Godot;

/// <summary>
/// Parte parcial de PlayerController: parry. Refleja ataques enemigos
/// durante una ventana activa en los frames 4-7 de la animacion "Parry"
/// (9 frames en total). La ventana se recalcula automaticamente a partir
/// de los fps y el frame count reales de la animacion.
/// </summary>
public partial class PlayerController
{
	// PARRY ventana activa en frames 4-7 de la animacion de 9 frames
	[ExportGroup("Parry")]
	[Export] public float ParryWindowStart { get; set; } = 0.13f;  // inicio frame 4
	[Export] public float ParryWindowEnd   { get; set; } = 0.46f;  // fin frame 7
	[Export] public float ParryDuration    { get; set; } = 0.60f;  // duracion total
	[Export] public float ParryCooldown    { get; set; } = 0.60f;
	[Export] public float ParryPostureDmg  { get; set; } = 40f;
	[Export] public float ParryFreezeTime  { get; set; } = 0.12f;

	private float _parryTimer;
	private float _parryCdTimer;
	private bool  _parryActive = false;  // true solo durante frames 4-7 del parry

	// ESTADOS

	/// <summary>
	/// Parry ventana activa en frames 4-7 de la animacion de 9 frames
	/// Timeline
	///   0s               animacion empieza frames 1-3 anticipacion
	///   ParryWindowStart ParryBox se activa frame 4
	///   ParryWindowEnd   ParryBox se desactiva despues frame 7
	///   ParryDuration    estado termina vuelve a Idle
	/// </summary>
	private void StateParrying(float dt)
	{
		Velocity     = new Vector2(0, Velocity.Y);
		ApplyGravity(dt);
		_parryTimer += dt;

		// abre la ventana de parry en frame 4
		if (_parryTimer >= ParryWindowStart && _parryTimer < ParryWindowEnd && !_parryActive)
		{
			_parryActive          = true;
			_parryBox.Monitoring  = true;
			_parryBox.Monitorable = true;
		}

		// cierra la ventana de parry en frame 8 recuperacion
		if (_parryTimer >= ParryWindowEnd && _parryActive)
		{
			_parryActive          = false;
			_parryBox.Monitoring  = false;
			_parryBox.Monitorable = false;
		}

		// termina el estado al completar los 9 frames
		if (_parryTimer >= ParryDuration)
		{
			_parryTimer = 0;
			ChangeState(State.Idle);
		}
	}

	// INICIO DE ACCIONES

	/// <summary>
	/// Parry calcula ventana activa automaticamente desde los frames
	/// de la animacion "Parry" frames 4-7 son la ventana activa
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

			// ventana activa frames 4-7 indice base 0 osea 3 al 6
			ParryWindowStart = frameDur * 3f;
			ParryWindowEnd   = frameDur * 7f;
			ParryDuration    = fps > 0f ? total / fps : 0.60f;
		}

		ChangeState(State.Parrying);
	}

	// CALLBACKS DE SENIALES

	/// <summary>
	/// ParryBox toco un hitbox enemigo durante la ventana activa frames 4-7
	/// aplica danio de postura al enemigo otorga iframes y congela el frame
	/// filtra hitboxes propios para evitar auto-parry
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
}
