using Godot;

// Sistema de salud. Se adjunta como nodo hijo del Player o Enemy.
// Controla HP actual y máximo, y emite señales cuando cambia la salud o el personaje muere.
public partial class HealthSystem : Node
{
	// Señal que se emite cada vez que cambia la salud, enviando el valor actual y el máximo.
	[Signal] public delegate void HealthChangedEventHandler(float current, float max);
	// Señal que se emite una sola vez cuando la salud llega a cero.
	[Signal] public delegate void DiedEventHandler();

	// HP máximo configurable desde el Inspector de Godot.
	[Export] public float MaxHealth { get; set; } = 100f;

	private float _currentHealth;
	private bool _isDead = false;

	// Propiedades de solo lectura para consultar el estado desde otros scripts.
	public float CurrentHealth => _currentHealth;
	public bool IsDead => _isDead;

	// Al iniciar, la salud actual se establece al máximo.
	public override void _Ready()
	{
		_currentHealth = MaxHealth;
	}

	// Aplica daño al personaje. Si la salud llega a 0 emite la señal de muerte.
	public void TakeDamage(float amount)
	{
		if (_isDead) return;
		_currentHealth = Mathf.Clamp(_currentHealth - amount, 0f, MaxHealth);
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
		if (_currentHealth <= 0f)
		{
			_isDead = true;
			EmitSignal(SignalName.Died);
		}
	}

	// Restaura salud sin superar el máximo. No funciona si el personaje está muerto.
	public void Heal(float amount)
	{
		if (_isDead) return;
		_currentHealth = Mathf.Clamp(_currentHealth + amount, 0f, MaxHealth);
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
	}

	// Restaura la salud al máximo y reactiva el personaje, usado para respawn.
	public void ResetHealth()
	{
		_isDead = false;
		_currentHealth = MaxHealth;
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
	}
}
