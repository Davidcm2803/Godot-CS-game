using Godot;

/// <summary>
/// PostureSystem.cs
/// Sekiro-style posture (poise) system.
/// Posture builds when the owner blocks / is hit.
/// When posture breaks the owner is stunned and takes bonus damage.
/// Posture regenerates when the owner is not being hit.
/// </summary>
public partial class PostureSystem : Node
{
	// ─── Signals ───────────────────────────────────────────────────────────
	[Signal] public delegate void PostureChangedEventHandler(float current, float max);
	[Signal] public delegate void PostureBrokenEventHandler();   // stagger / stun
	[Signal] public delegate void PostureRestoredEventHandler(); // back to normal

	// ─── Exported Properties ───────────────────────────────────────────────
	[Export] public float MaxPosture           { get; set; } = 100f;
	[Export] public float PostureRegenRate     { get; set; } = 15f;   // per second
	[Export] public float RegenDelay           { get; set; } = 2.5f;  // seconds after last hit
	[Export] public float StunDuration         { get; set; } = 1.8f;  // seconds stunned when broken
	[Export] public float BrokenDamageMultiplier { get; set; } = 2.5f; // bonus deathblow damage

	// ─── Private State ─────────────────────────────────────────────────────
	private float _currentPosture   = 0f;
	private bool  _isBroken         = false;
	private float _regenTimer       = 0f;   // countdown to start regen
	private float _stunTimer        = 0f;   // countdown while stunned

	// ─── Accessors ─────────────────────────────────────────────────────────
	public float CurrentPosture  => _currentPosture;
	public bool  IsBroken        => _isBroken;
	public bool  IsStunned       => _stunTimer > 0f;

	// ─── Godot Lifecycle ───────────────────────────────────────────────────
	public override void _Process(double delta)
	{
		float dt = (float)delta;

		// Handle stun countdown
		if (_stunTimer > 0f)
		{
			_stunTimer -= dt;
			if (_stunTimer <= 0f)
			{
				_isBroken   = false;
				_stunTimer  = 0f;
				_currentPosture = 0f;
				EmitSignal(SignalName.PostureRestored);
				EmitSignal(SignalName.PostureChanged, _currentPosture, MaxPosture);
			}
			return; // no regen while stunned
		}

		// Regen delay countdown
		if (_regenTimer > 0f)
		{
			_regenTimer -= dt;
			return;
		}

		// Regenerate posture toward 0
		if (_currentPosture > 0f)
		{
			_currentPosture = Mathf.MoveToward(_currentPosture, 0f, PostureRegenRate * dt);
			EmitSignal(SignalName.PostureChanged, _currentPosture, MaxPosture);
		}
	}

	// ─── Public API ────────────────────────────────────────────────────────

	/// <summary>
	/// Add posture damage (from being hit or blocking an attack).
	/// </summary>
	public void AddPostureDamage(float amount)
	{
		if (_isBroken) return;

		_regenTimer   = RegenDelay;
		_currentPosture = Mathf.Clamp(_currentPosture + amount, 0f, MaxPosture);
		EmitSignal(SignalName.PostureChanged, _currentPosture, MaxPosture);

		if (_currentPosture >= MaxPosture)
		{
			BreakPosture();
		}
	}

	/// <summary>
	/// A successful parry reduces enemy posture by a large amount (reward precision).
	/// </summary>
	public void AddParryPostureDamage(float amount)
	{
		// Parry damage bypasses the broken check — stacking parries should
		// still contribute (though it won't fire the broken signal twice).
		_regenTimer     = RegenDelay;
		_currentPosture = Mathf.Clamp(_currentPosture + amount, 0f, MaxPosture);
		EmitSignal(SignalName.PostureChanged, _currentPosture, MaxPosture);

		if (!_isBroken && _currentPosture >= MaxPosture)
		{
			BreakPosture();
		}
	}

	/// <summary>
	/// Force-break posture immediately (e.g. parry deathblow).
	/// </summary>
	private void BreakPosture()
	{
		_isBroken  = true;
		_stunTimer = StunDuration;
		EmitSignal(SignalName.PostureBroken);
	}

	/// <summary>
	/// Reset posture to 0 (full regen, e.g. between encounters).
	/// </summary>
	public void ResetPosture()
	{
		_isBroken       = false;
		_stunTimer      = 0f;
		_regenTimer     = 0f;
		_currentPosture = 0f;
		EmitSignal(SignalName.PostureChanged, _currentPosture, MaxPosture);
	}
}
