using Godot;

/// <summary>
/// Parte parcial de PlayerController: dash. Recorre siempre la misma
/// distancia sin importar el framerate porque la velocidad se calcula
/// como distancia sobre duracion. Mientras dura, el jugador es intocable
/// (iframes en el hurtbox) y se apaga la colision fisica contra la capa
/// de enemigos, asi los atraviesa de verdad en vez de chocar como pared.
/// </summary>
public partial class PlayerController
{
	// DASH desplazamiento largo el jugador queda intocable y traspasa enemigos
	// se activa con L o accion "dash" hay que crearla en el Input Map
	[ExportGroup("Dash")]
	[Export] public float DashDistance { get; set; } = 260f;  // px totales que recorre el dash
	[Export] public float DashDuration { get; set; } = 0.10f; // tiempo en cubrir esa distancia
	[Export] public float DashCooldown { get; set; } = 1.0f;  // tiempo antes de poder volver a dashear
	[Export] public float DashSpeed    { get; set; }          // se calcula solo en BeginDash no tocar a mano
	// numero de capa fisica que usan los enemigos se apaga un instante durante
	// el dash para poder atravesarlos de verdad y no quedar frenado como pared
	[Export] public int DashEnemyCollisionLayer { get; set; } = 2;

	private float _dashTimer;
	private float _dashCdTimer;
	private float _dashDirX = 1f;
	private uint  _dashOriginalMask; // mascara de colision guardada antes de dashear para restaurarla despues

	// ESTADOS

	/// <summary>
	/// Dash largo recorre DashDistance en DashDuration a velocidad constante
	/// ignora la gravedad mientras dura para que la distancia sea siempre la
	/// misma sin importar si se activo en el piso o en el aire
	/// es intocable iframes en el hurtbox mas colision fisica desactivada
	/// contra la capa de enemigos asi atraviesa cualquier enemigo de verdad
	/// </summary>
	private void StateDashing(float dt)
	{
		Velocity    = new Vector2(_dashDirX * DashSpeed, 0f);
		_dashTimer += dt;
		GD.Print($"[Dash] timer={_dashTimer:F3} dur={DashDuration:F3} anim={_sprite.Animation} frame={_sprite.Frame}");


		if (_dashTimer >= DashDuration)
		{
			_dashTimer = 0;
			EndDash();
			ChangeState(IsOnFloor() ? State.Idle : State.Falling);
		}
	}

	// INICIO DE ACCIONES

	/// <summary>
	/// Dash sale en la direccion del input o hacia donde mira si no hay input
	/// calcula la velocidad a partir de distancia sobre duracion para que el
	/// dash siempre cubra la misma distancia sin importar el framerate
	/// tambien apaga por un rato la colision fisica contra la capa de enemigos
	/// para poder atravesarlos de verdad y no quedar frenado como si fueran pared
	/// </summary>
	private void BeginDash()
	{
		float dir    = GetMoveInput();
		_dashDirX    = dir != 0 ? dir : _facingDir;
		_dashTimer   = 0;
		_dashCdTimer = DashCooldown;

		// sincroniza la duración del dash con la animación real, si existe
		var frames = _sprite.SpriteFrames;
		if (frames.HasAnimation("Dash"))
		{
			float fps      = (float)frames.GetAnimationSpeed("Dash");
			int   frameCnt = frames.GetFrameCount("Dash");
			if (fps > 0f)
				DashDuration = frameCnt / fps;
		}

		DashSpeed = DashDuration > 0f ? DashDistance / DashDuration : DashSpeed;

		_hurtbox.SetInvincible(DashDuration);
		_dashOriginalMask = CollisionMask;
		SetCollisionMaskValue(DashEnemyCollisionLayer, false);

		ChangeState(State.Dashing);
	}

	/// <summary>Restaura la mascara de colision original al terminar el dash</summary>
	private void EndDash()
	{
		CollisionMask = _dashOriginalMask;
	}
}
