using Godot;

/// <summary>
/// Створює шини Music/SFX, застосовує збережену гучність і грає фонову музику.
/// Додається в сцену з LevelGenerator._Ready().
/// </summary>
public partial class AudioManager : Node
{
	public const string MusicBus = "Music";
	public const string SfxBus = "SFX";
	private const string MusicPath = "res://Audio/cave_ambience.wav";

	private AudioStreamPlayer _music;
    private AudioStreamPlayer _coreAccent;
    private CaveZone _zone = CaveZone.Basalt;
    private float _zonePitch = 1f;

	public static void EnsureBuses()
	{
		SaveData.Load();
		foreach (string name in new[] { MusicBus, SfxBus })
		{
			if (AudioServer.GetBusIndex(name) >= 0) continue;
			AudioServer.AddBus();
			int index = AudioServer.BusCount - 1;
			AudioServer.SetBusName(index, name);
			AudioServer.SetBusSend(index, "Master");
		}
		ApplyVolumes();
	}

	public static void ApplyVolumes()
	{
		SetBusVolume(MusicBus, SaveData.MusicVolume);
		SetBusVolume(SfxBus, SaveData.SfxVolume);
	}

	private static void SetBusVolume(string bus, float linear)
	{
		int index = AudioServer.GetBusIndex(bus);
		if (index < 0) return;
		AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Max(linear, 0.0001f)));
		AudioServer.SetBusMute(index, linear <= 0.001f);
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		EnsureBuses();

		var stream = ResourceLoader.Exists(MusicPath) ? GD.Load<AudioStream>(MusicPath) : null;
		if (stream == null) return;

		// Робимо доріжку безшовним лупом (налаштування імпорту в редакторі не потрібні).
		double mixRate = stream.Get("mix_rate").AsDouble();
		stream.Set("loop_mode", 1); // Forward
		stream.Set("loop_begin", 0);
		stream.Set("loop_end", (int)(stream.GetLength() * mixRate));

		_music = new AudioStreamPlayer { Name = "Music", Stream = stream, Bus = MusicBus, VolumeDb = -4f };
		AddChild(_music);
		_music.Play();
        _coreAccent = new AudioStreamPlayer { Name = "CoreAccent",Stream = GD.Load<AudioStream>("res://Audio/coin.wav"),Bus = SfxBus,VolumeDb = -8f,PitchScale = 0.55f };
        AddChild(_coreAccent);
	}
    public void SetZone(CaveZone zone) => _zone = zone;
    public override void _ExitTree()
    {
        // Release playback before a menu reload replaces the whole world.
        if(_music!=null) { _music.Stop(); _music.Stream=null; }
        if(_coreAccent!=null) { _coreAccent.Stop(); _coreAccent.Stream=null; }
    }
    public void PlayCoreAccent() => _coreAccent?.Play();
    public override void _Process(double delta)
    {
        if (GetTree().Paused || _music == null) return;
        float target = _zone == CaveZone.Core ? 0.78f : _zone == CaveZone.Magma ? 0.88f : _zone == CaveZone.Crystal ? 1.08f : 1f;
        _zonePitch = Mathf.Lerp(_zonePitch,target,1f-Mathf.Exp(-(float)delta*1.2f));
        _music.PitchScale = _zonePitch;
    }
}
