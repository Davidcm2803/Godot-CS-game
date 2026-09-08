using Godot;

/// <summary>
/// Projectile.cs
/// Viaja en línea recta, hace daño al primer Hurtbox enemigo que toca,
/// y se destruye al colisionar con cualquier cosa excepto el propio jugador.
/// Se destruye también al vencer su tiempo de vida.
///
/// La escena debe ser: Area2D (este script) + CollisionShape2D + AnimatedSprite2D
/// El jugador debe estar en el grupo "player" para que el proyectil lo ignore.
/// </summary>
public partial class Projectile : Area2D
{
	[Export] public float Speed      { get; set; } = 400f;
	[Export] public float Damage     { get; set; } = 15f;
	[Export] public float PostureDmg { get; set; } = 10f;
	[Export] public float Lifetime   { get; set; } = 3f;

	// Dirección de viaje: 1 = derecha, -1 = izquierda
	// Se setea desde PlayerController justo después de instanciar
	public float Direction { get; set; } = 1f;

	private float            _timer = 0f;
	private bool             _dead  = false;
	private AnimatedSprite2D _sprite;

	public override void _Ready()
	{
		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		GD.Print("[Projectile] sprite encontrado: ", _sprite != null);
		GD.Print("[Projectile] animacion: ", _sprite?.Animation);

		// Voltea el sprite si va hacia la izquierda
		if (_sprite != null)
			_sprite.FlipH = Direction < 0;

		// Detecta Hurtbox enemigos (Area2D)
		AreaEntered += OnAreaEntered;

		// Detecta paredes y suelo (StaticBody2D, TileMapLayer, etc.)
		BodyEntered += OnBodyEntered;

		// Reproduce la animación de vuelo
		_sprite?.Play("Fly");

		GD.Print("[Projectile] Instanciado en: ", GlobalPosition);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_dead) return;
		GD.Print("[Projectile] pos: ", GlobalPosition);
		float dt = (float)delta;

		// Mueve el proyectil en línea recta
		Position += new Vector2(Direction * Speed * dt, 0f);

		// Tiempo de vida
		_timer += dt;
		if (_timer >= Lifetime)
			Destroy();
	}

	// Tocó un Area2D — chequea si es un Hurtbox enemigo
	private void OnAreaEntered(Area2D area)
	{
		if (_dead) return;

		if (area is Hurtbox hurtbox && hurtbox.OwnerTag == "enemy")
		{
			hurtbox.ReceiveHit(Damage, PostureDmg, GlobalPosition);
			Destroy();
		}
	}

	// Tocó un cuerpo físico (pared, suelo, plataforma)
	private void OnBodyEntered(Node2D body)
	{
		if (_dead) return;

		// Ignora al propio jugador para no destruirse al instanciarse
		if (body.IsInGroup("player")) return;

		Destroy();
	}

	/// <summary>
	/// Destruye el proyectil. Si tiene animación "Hit" la reproduce antes de borrarse.
	/// </summary>
	private void Destroy()
	{
		if (_dead) return;
		_dead = true;

		// Desactiva colisión inmediatamente para no golpear dos veces
		SetDeferred(Area2D.PropertyName.Monitoring,  false);
		SetDeferred(Area2D.PropertyName.Monitorable, false);

		// Si hay animación de impacto la reproduce y luego borra el nodo
		if (_sprite != null && _sprite.SpriteFrames != null &&
			_sprite.SpriteFrames.HasAnimation("Hit"))
		{
			_sprite.Play("Hit");
			_sprite.AnimationFinished += () => QueueFree();
		}
		else
		{
			QueueFree();
		}
	}
}
