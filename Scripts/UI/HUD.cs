using Godot;

public partial class HUD : Control
{
	private ProgressBar _healthBar;
	private ProgressBar _postureBar;
	private ProgressBar _enemyPostureBar;
	private Label       _parryLabel;
	private Label       _stunLabel;

	private float _parryLabelTimer = 0f;
	private float _stunLabelTimer  = 0f;
	private const float LabelDisplayTime = 0.6f;

	private PlayerController _player;
	private EnemyAI          _enemy;

	public override void _Ready()
	{
		_healthBar       = GetNode<ProgressBar>("PlayerHealth/health_bar");
		_postureBar      = GetNode<ProgressBar>("PlayerPosture/posture_bar");
		_enemyPostureBar = GetNodeOrNull<ProgressBar>("EnemyPosture/enemy_posture_bar");
		_parryLabel      = GetNodeOrNull<Label>("ParryIndicator");
		_stunLabel       = GetNodeOrNull<Label>("StunIndicator");

		if (_parryLabel != null) _parryLabel.Visible = false;
		if (_stunLabel  != null) _stunLabel.Visible  = false;

		// Valores iniciales
		_healthBar.MaxValue  = 100f;
		_healthBar.Value     = 100f;
		_postureBar.MaxValue = 100f;
		_postureBar.Value    = 0f;

		if (_enemyPostureBar != null)
		{
			_enemyPostureBar.MaxValue = 100f;
			_enemyPostureBar.Value    = 0f;
		}

		// Colores
		var redFill = new StyleBoxFlat();
		redFill.BgColor = new Color(0.85f, 0.1f, 0.1f);
		_healthBar.AddThemeStyleboxOverride("fill", redFill);

		var redBg = new StyleBoxFlat();
		redBg.BgColor = new Color(0.2f, 0.0f, 0.0f);
		_healthBar.AddThemeStyleboxOverride("background", redBg);

		var postureFill = new StyleBoxFlat();
		postureFill.BgColor = new Color(0.9f, 0.7f, 0.1f);
		_postureBar.AddThemeStyleboxOverride("fill", postureFill);

		var postureBg = new StyleBoxFlat();
		postureBg.BgColor = new Color(0.2f, 0.15f, 0.0f);
		_postureBar.AddThemeStyleboxOverride("background", postureBg);

		CallDeferred(MethodName.ConnectToGameNodes);
	}

	private void ConnectToGameNodes()
	{
		var level = GetTree().CurrentScene;
		_player = level?.FindChild("Player", true, false) as PlayerController;
		_enemy  = level?.FindChild("Enemy",  true, false) as EnemyAI;

		if (_player != null)
		{
			_player.Health.HealthChanged   += OnPlayerHealthChanged;
			_player.Posture.PostureChanged += OnPlayerPostureChanged;
			_healthBar.MaxValue = _player.Health.MaxHealth;
			_healthBar.Value    = _player.Health.CurrentHealth;
		}

		if (_enemy != null)
		{
			_enemy.Posture.PostureChanged += OnEnemyPostureChanged;
			_enemy.Posture.PostureBroken  += OnEnemyPostureBroken;
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (_parryLabelTimer > 0f)
		{
			_parryLabelTimer -= dt;
			if (_parryLabelTimer <= 0f && _parryLabel != null)
				_parryLabel.Visible = false;
		}

		if (_stunLabelTimer > 0f)
		{
			_stunLabelTimer -= dt;
			if (_stunLabelTimer <= 0f && _stunLabel != null)
				_stunLabel.Visible = false;
		}

		if (_player != null && _player.ParryWindowActive && _parryLabel != null && !_parryLabel.Visible)
		{
			_parryLabel.Visible = true;
			_parryLabelTimer    = LabelDisplayTime;
		}
	}

	private void OnPlayerHealthChanged(float current, float max)
	{
		_healthBar.MaxValue = max;
		_healthBar.Value    = current;
	}

	private void OnPlayerPostureChanged(float current, float max)
	{
		_postureBar.MaxValue = max;
		_postureBar.Value    = current;
	}

	private void OnEnemyPostureChanged(float current, float max)
	{
		if (_enemyPostureBar == null) return;
		_enemyPostureBar.MaxValue = max;
		_enemyPostureBar.Value    = current;
	}

	private void OnEnemyPostureBroken()
	{
		if (_stunLabel == null) return;
		_stunLabel.Text    = "POSTURE BROKEN!";
		_stunLabel.Visible = true;
		_stunLabelTimer    = 1.4f;
	}
}
