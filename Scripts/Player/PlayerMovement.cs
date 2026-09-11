using Godot;

/// <summary>
/// Parte parcial de PlayerController: movimiento base en el suelo y en el aire
/// (Idle, Running, Jumping, Falling), la transicion desde estado "grounded"
/// (salto y chequeo de parry) y la lectura de input horizontal.
/// </summary>
public partial class PlayerController
{
	// MOVIMIENTO valores base de caminata y salto
	[ExportGroup("Movement")]
	[Export] public float MoveSpeed { get; set; } = 180f;
	[Export] public float JumpForce { get; set; } = -400f;

	// FLAGS Y DIRECCION
	private int _facingDir = 1;

	// ESTADOS

	/// <summary>Jugador quieto reproduce "NormalIdle" en loop</summary>
	private void StateIdle(float dt)
	{
		ApplyGravity(dt);

		if (TryTransitionFromGrounded()) return;

		if (_state != State.Idle) return;

		Velocity = new Vector2(0, Velocity.Y);

		if (_sprite.Animation != "NormalIdle")
			_sprite.Play("NormalIdle");
	}

	/// <summary>Jugador moviendose horizontalmente</summary>
	private void StateRunning(float dt)
	{
		ApplyGravity(dt);
		float dir = GetMoveInput();
		Velocity  = new Vector2(dir * MoveSpeed, Velocity.Y);
		if (dir == 0) ChangeState(State.Idle);
		else TryTransitionFromGrounded();
	}

	/// <summary>Jugador en el aire subiendo pasa a Falling cuando Y empieza a bajar</summary>
	private void StateJumping(float dt)
	{
		ApplyGravity(dt);
		Velocity = new Vector2(GetMoveInput() * MoveSpeed, Velocity.Y);
		if (Velocity.Y > 0) ChangeState(State.Falling);
	}

	/// <summary>Jugador cayendo vuelve a Idle al tocar el suelo</summary>
	private void StateFalling(float dt)
	{
		ApplyGravity(dt);
		Velocity = new Vector2(GetMoveInput() * MoveSpeed, Velocity.Y);
		if (IsOnFloor()) ChangeState(State.Idle);
	}

	// TRANSICIONES E INPUTS

	/// <summary>
	/// Lee inputs de suelo parry jump movimiento
	/// los ataques y el dash se leen globalmente en _PhysicsProcess para no perder clicks
	/// devuelve true si hubo transicion
	/// </summary>
	private bool TryTransitionFromGrounded()
	{
		float dir = GetMoveInput();

		// parry solo en suelo y con cooldown disponible tecla Q accion "parry_"
		if (Input.IsActionJustPressed("parry_") && _parryCdTimer <= 0 && IsOnFloor())
		{ BeginParry(); return true; }

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

	/// <summary>
	/// Lee input horizontal orienta el sprite FlipH y devuelve el eje -1 0 1
	/// el offset ya no se toca aca lo maneja ApplySpriteOffset cada frame
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
				_sprite.FlipH = axis < 0;
		}

		return axis;
	}
}
