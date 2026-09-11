using Godot;

/// <summary>
/// Parte parcial de EnemyAI: animación y orientación del sprite.
/// </summary>
public partial class EnemyAI
{
	// Reproduce la animación correspondiente al estado actual
	private void UpdateAnimation()
	{
		if (_sprite == null) return;

		string anim = _state switch
		{
			State.Attack                => "Attack",
			State.Patrol or State.Chase => "Running",
			State.Dead                  => "Death",
			_                           => "Idle"
		};

		if (_sprite.Animation != anim)
			_sprite.Play(anim);
	}

	// Voltea el sprite según la dirección de movimiento.
	// Ya no hay DetectionZone/AttackZone que voltear: la detección ahora es
	// por distancia real (ver EnemyDetection.cs), no depende de la orientación
	// del sprite ni de zonas con posición local fija.
	private void UpdateSpriteFacing()
	{
		if (_sprite == null) return;
		_sprite.FlipH  = _facingDir < 0;
		_sprite.Offset = new Vector2(_facingDir < 0 ? -15f : -1f, 0f);
	}
}
