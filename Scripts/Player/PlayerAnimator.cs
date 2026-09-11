using Godot;

/// <summary>
/// Parte parcial de PlayerController: animacion, escala y offset de sprite.
/// Cada animacion tiene su propio offset X/Y exportado al Inspector, asi se
/// puede ajustar la posicion de cada animacion a ojo sin recompilar,
/// compensando que cada sprite de pixel art tiene un centro distinto.
/// El offset se recalcula una sola vez por frame en ApplySpriteOffset
/// evitando que quede desincronizado entre animacion y direccion.
///
/// Escala por animacion para compensar diferencias de tamanio entre sprites
///   Base NormalIdle (1038 100)
///   Attack 2px mas grande AttackB 2px menos Death 4px mas grande
/// </summary>
public partial class PlayerController
{
	// SPRITE offset por animacion mirando a la DERECHA el eje X se
	// invierte o ajusta automaticamente cuando el personaje mira a la izquierda
	// usando LeftFacingExtraOffsetX ajusta estos valores en el Inspector
	// hasta que todas las animaciones queden alineadas entre si
	[ExportGroup("Sprite Offsets (por animación, mirando derecha)")]
	[Export] public Vector2 OffsetIdle         { get; set; } = new Vector2(-1f, 0f);
	[Export] public Vector2 OffsetRunning      { get; set; } = new Vector2(-1f, -4f);
	[Export] public Vector2 OffsetJump         { get; set; } = new Vector2(-1f, -5f);
	[Export] public Vector2 OffsetAttack       { get; set; } = new Vector2(-1f, 1f);
	[Export] public Vector2 OffsetAttackB      { get; set; } = new Vector2(-1f, -10f);
	[Export] public Vector2 OffsetNormalAttack { get; set; } = new Vector2(-1f, -3f);
	[Export] public Vector2 OffsetDash         { get; set; } = new Vector2(-1f, 0f);
	[Export] public Vector2 OffsetParry        { get; set; } = new Vector2(-1f, -5f);
	[Export] public Vector2 OffsetHurt         { get; set; } = new Vector2(-1f, 0f);
	[Export] public Vector2 OffsetDeath        { get; set; } = new Vector2(-1f, 0f);
	[Export] public float OffsetAttackForwardPush  { get; set; } = 50f; // ataques click izquierdo y derecho
	[Export] public float OffsetAttackBForwardPush { get; set; } = 30f;
	// cuanto se le suma al offset X cuando el personaje mira a la izquierda
	// equivalente al viejo -15 vs -1 es decir una diferencia de -14
	[Export] public float LeftFacingExtraOffsetX { get; set; } = -14f;

	/// <summary>
	/// Reproduce la animacion y ajusta la escala del sprite segun el estado
	/// la escala base es NormalIdle 1038 100 cada animacion se ajusta
	/// proporcionalmente para compensar diferencias de tamanio entre sprites
	/// el offset ya no se toca aca lo maneja ApplySpriteOffset cada frame
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
			State.Dashing         => "Dash",
			State.Parrying        => "Parry",
			State.Hurt            => "Hurt",
			State.Dead            => "Death",
			_                     => "NormalIdle"
		};

		if (_sprite.Animation != anim)
		{
			_sprite.Play(anim);

			// escala por animacion base NormalIdle
			// Attack mas grande AttackB mas chico Death mas grande
			// NormalAttack y Dash quedan en la escala base
			// Jump misma escala base con offset Y ajustado mas arriba
			_sprite.Scale = anim switch
			{
				"Attack"       => new Vector2(1.190f, 1.190f),
				"AttackB"      => new Vector2(1.150f, 1.150f),
				"NormalAttack" => new Vector2(1.038f, 1.0f),
				"Death"        => new Vector2(1.078f, 1.04f),
				"Jump"         => new Vector2(1.038f, 1.0f),
				"Running"      => new Vector2(1.040f, 1.040f),
				"Parry"        => new Vector2(1.100f, 1.100f),
				"Dash"         => new Vector2(1.038f, 1.0f),
				_              => new Vector2(1.038f, 1.0f)
			};
		}

		ApplySpriteOffset();
	}

	/// <summary>
	/// Unico punto donde se calcula y aplica el offset del sprite combina el
	/// offset propio de la animacion actual exportado al Inspector con el
	/// ajuste por direccion mirando izquierda o derecha se llama cada frame
	/// para que animacion y direccion nunca queden desincronizadas entre si
	/// </summary>
	private void ApplySpriteOffset()
	{
		if (_sprite == null) return;

		string animName = _sprite.Animation.ToString();

		Vector2 baseOffset = animName switch
		{
			"NormalIdle"   => OffsetIdle,
			"Running"      => OffsetRunning,
			"Jump"         => OffsetJump,
			"Attack"       => OffsetAttack,
			"AttackB"      => OffsetAttackB,
			"NormalAttack" => OffsetNormalAttack,
			"Dash"         => OffsetDash,
			"Parry"        => OffsetParry,
			"Hurt"         => OffsetHurt,
			"Death"        => OffsetDeath,
			_              => OffsetIdle
		};

		// empuje extra hacia adelante solo para Attack y AttackB para que el
		// sprite mas grande arranque desplazado hacia donde mira el jugador
		// en vez de quedar centrado y luego saltar de vuelta al terminar
		float forwardPush = animName switch
		{
			"Attack"  => OffsetAttackForwardPush,
			"AttackB" => OffsetAttackBForwardPush,
			_         => 0f
		};

		float x = baseOffset.X
				+ (_facingDir < 0 ? LeftFacingExtraOffsetX : 0f)
				+ (forwardPush * _facingDir);

		_sprite.Offset = new Vector2(x, baseOffset.Y);
	}
}
