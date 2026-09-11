using Godot;

// Barra de vida visual. Se adjunta como Sprite2D hijo del Player/Enemy,
// escucha las señales de HealthSystem y cambia el frame del PNG (7 frames, 6 segmentos).
public partial class HealthBar : Sprite2D
{
	// Textura fuente actual: el PNG con las 7 frames en tira horizontal (frame0=lleno, frame6=vacío).
	[Export] public Texture2D HeartStrip { get; set; }

	// Tiras alternativas para cada tier de vida (ej: [0]=chica, [1]=grande tras 2 corazones de ogro).
	// Si no usás tiers, dejalo vacío y solo se usa HeartStrip.
	[Export] public Texture2D[] TierStrips { get; set; }

	// Cantidad de frames en cada tira (todas deben tener el mismo FrameCount y dimensiones por frame).
	[Export] public int FrameCount { get; set; } = 7;

	// Referencia al HealthSystem del que escucha los cambios.
	[Export] public HealthSystem Health { get; set; }

	private AtlasTexture _atlas;
	private int _frameWidth;
	private int _frameHeight;

	public override void _Ready()
	{
		if (HeartStrip == null)
		{
			GD.PushError("HealthBar: falta asignar HeartStrip en el Inspector.");
			return;
		}

		_atlas = new AtlasTexture();
		_atlas.Atlas = HeartStrip;
		Texture = _atlas;

		RecalculateFrameSize();

		if (Health != null)
		{
			Health.HealthChanged += OnHealthChanged;
			// Pintar el estado inicial.
			OnHealthChanged(Health.CurrentHealth, Health.MaxHealth);
		}
	}

	// Calcula qué frame mostrar según el porcentaje de vida actual.
	private void OnHealthChanged(float current, float max)
	{
		float ratio = max > 0f ? current / max : 0f;
		ratio = Mathf.Clamp(ratio, 0f, 1f);

		// ratio=1 -> frame 0 (lleno) | ratio=0 -> frame FrameCount-1 (vacío)
		int frameIndex = (int)Mathf.Round((1f - ratio) * (FrameCount - 1));
		frameIndex = Mathf.Clamp(frameIndex, 0, FrameCount - 1);

		SetFrame(frameIndex);
	}

	private void SetFrame(int index)
	{
		_atlas.Region = new Rect2(index * _frameWidth, 0, _frameWidth, _frameHeight);
	}

	private void RecalculateFrameSize()
	{
		_frameWidth = HeartStrip.GetWidth() / FrameCount;
		_frameHeight = HeartStrip.GetHeight();
	}

	// Cambia a otra tira (tier) de vida, ej. al pasar de barra chica a grande.
	public void SetTier(int tierIndex)
	{
		if (TierStrips == null || tierIndex < 0 || tierIndex >= TierStrips.Length)
		{
			GD.PushError($"HealthBar: tierIndex {tierIndex} inválido.");
			return;
		}

		HeartStrip = TierStrips[tierIndex];
		_atlas.Atlas = HeartStrip;
		RecalculateFrameSize();

		if (Health != null)
			OnHealthChanged(Health.CurrentHealth, Health.MaxHealth);
	}

	public override void _ExitTree()
	{
		if (Health != null)
			Health.HealthChanged -= OnHealthChanged;
	}
}