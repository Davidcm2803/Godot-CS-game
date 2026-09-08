using Godot;

/// <summary>
/// Hitbox.cs
/// Attach to an Area2D that represents an attack hitbox.
/// When this area overlaps a Hurtbox it delivers damage + posture damage.
/// The hitbox is DISABLED by default. Enable it only during attack frames.
///
/// Usa CallDeferred para cambiar Monitoring/Monitorable porque Godot no permite
/// modificarlos directamente dentro de una señal de física (AreaEntered).
/// </summary>
public partial class Hitbox : Area2D
{
	// ─── Exported Properties ───────────────────────────────────────────────
	[Export] public float  Damage        { get; set; } = 20f;
	[Export] public float  PostureDamage { get; set; } = 18f;
	/// <summary>Owner tag para evitar self-hits ("player" o "enemy").</summary>
	[Export] public string OwnerTag      { get; set; } = "player";

	// ─── Godot Lifecycle ───────────────────────────────────────────────────
	public override void _Ready()
	{
		// Los hitboxes arrancan desactivados; el state machine los activa
		Monitoring  = false;
		Monitorable = false;

		AreaEntered += OnAreaEntered;
	}

	// ─── Signal Handlers ───────────────────────────────────────────────────
	private void OnAreaEntered(Area2D area)
	{
		if (!Monitoring) return;

		// Solo golpea Hurtboxes del bando contrario
		if (area is Hurtbox hurtbox && hurtbox.OwnerTag != OwnerTag)
		{
			hurtbox.ReceiveHit(Damage, PostureDamage, GlobalPosition);
		}
	}

	// ─── Public API ────────────────────────────────────────────────────────

	/// <summary>Activa el hitbox para una ventana de ataque.</summary>
	public void Activate()
	{
		// Activate puede llamarse desde _PhysicsProcess, que está fuera de señales
		// — se puede asignar directo. Pero usamos deferred igual por consistencia.
		Monitoring  = true;
		Monitorable = true;
	}

	/// <summary>
	/// Desactiva el hitbox. Usa CallDeferred porque puede llamarse desde dentro
	/// de una señal de física (AreaEntered → ReceiveHit → OnHitReceived → Deactivate),
	/// y Godot bloquea cambios a Monitorable durante el flush de queries.
	/// </summary>
	public void Deactivate()
	{
		// SetDeferred evita el error:
		// "Function blocked during in/out signal. Use set_deferred(monitorable)"
		SetDeferred(Area2D.PropertyName.Monitoring,  false);
		SetDeferred(Area2D.PropertyName.Monitorable, false);
	}
}
