using Godot;

/// <summary>
/// Hurtbox.cs
/// Attach to an Area2D that can receive damage.
/// It delegates to HealthSystem and PostureSystem on the parent.
/// It is always Monitorable (enemies/player can hit it).
/// </summary>
public partial class Hurtbox : Area2D
{
	// ─── Signals ───────────────────────────────────────────────────────────
	/// Emitted when a hit is received. Useful for VFX/sound.
	[Signal] public delegate void HitReceivedEventHandler(float damage, Vector2 hitPosition);

	// ─── Exported Properties ───────────────────────────────────────────────
	/// Must match the parent entity: "player" or "enemy".
	[Export] public string OwnerTag { get; set; } = "player";

	// ─── Cached References ─────────────────────────────────────────────────
	private HealthSystem  _health;
	private PostureSystem _posture;

	// ─── Invincibility Frame State ─────────────────────────────────────────
	private bool  _isInvincible    = false;
	private float _invincibleTimer = 0f;

	// ─── Godot Lifecycle ───────────────────────────────────────────────────
	public override void _Ready()
	{
		// Hurtbox is always monitorable but does NOT monitor itself.
		Monitoring  = false;
		Monitorable = true;

		// Walk up to find HealthSystem and PostureSystem siblings/parent.
		_health  = GetParent().GetNodeOrNull<HealthSystem>("HealthSystem");
		_posture = GetParent().GetNodeOrNull<PostureSystem>("PostureSystem");

		if (_health  == null) GD.PrintErr($"[Hurtbox] No HealthSystem found on {GetParent().Name}");
		if (_posture == null) GD.PrintErr($"[Hurtbox] No PostureSystem found on {GetParent().Name}");
	}

	public override void _Process(double delta)
	{
		if (_isInvincible)
		{
			_invincibleTimer -= (float)delta;
			if (_invincibleTimer <= 0f)
			{
				_isInvincible = false;
			}
		}
	}

	// ─── Public API ────────────────────────────────────────────────────────

	/// <summary>
	/// Called by Hitbox when an attack lands.
	/// </summary>
	public void ReceiveHit(float damage, float postureDamage, Vector2 hitPosition)
	{
		if (_isInvincible) return;

		_health?.TakeDamage(damage);
		_posture?.AddPostureDamage(postureDamage);

		EmitSignal(SignalName.HitReceived, damage, hitPosition);
	}

	/// <summary>
	/// Grant i-frames for dodge / certain animations.
	/// </summary>
	public void SetInvincible(float duration)
	{
		_isInvincible    = true;
		_invincibleTimer = duration;
	}

	public bool IsInvincible => _isInvincible;
}
