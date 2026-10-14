using Godot;

/// <summary>
/// Створює шини Music/SFX, застосовує збережену гучність і грає фонову музику.
/// Додається в сцену з LevelGenerator._Ready().
/// </summary>
public partial class AudioManager : Node
{
	public const string MusicBus = "Music";
	public const string SfxBus = "SFX";
	private const string MusicPath = "res://Audio/Ambience/cave_ambience.wav";

	private AudioStreamPlayer _music;
    private AudioStreamPlayer _menuScore, _gameScore;
    private Tween _scoreFade;
    private bool _gameplayMusic;
    private AudioStreamPlayer _coreAccent;
    private AudioStreamPlayer _coreRumble;
    private float _coreTarget, _coreLevel;
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
        _menuScore = CreateScore("MenuScore", "res://Audio/Music/menu_planets_lullaby.wav", -2f);
        _gameScore = CreateScore("GameScore", "res://Audio/Music/game_obsidian_rush.wav", -60f);
        _menuScore.Play();
        _coreAccent = new AudioStreamPlayer { Name = "CoreAccent",Stream = GD.Load<AudioStream>("res://Audio/SFX/core_crossing.wav"),Bus = SfxBus,VolumeDb = -10f,ProcessMode = ProcessModeEnum.Pausable };
        AddChild(_coreAccent);
        _coreRumble = CreateScore("CoreRumble","res://Audio/Ambience/core_rumble.wav",-60f);
        _coreRumble.Bus = SfxBus;
        _coreRumble.ProcessMode = ProcessModeEnum.Pausable;
	}
    private AudioStreamPlayer CreateScore(string name, string path, float volume)
    {
        var stream = GD.Load<AudioStreamWav>(path);
        stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        stream.LoopBegin = 0;
        stream.LoopEnd = (int)Mathf.Round(stream.GetLength() * stream.MixRate);
        var player = new AudioStreamPlayer { Name = name, Stream = stream, Bus = MusicBus, VolumeDb = volume };
        AddChild(player);
        return player;
    }
    public void StartGameplayMusic()
    {
        if (_gameplayMusic || _gameScore == null) return;
        _gameplayMusic = true;
        _scoreFade?.Kill();
        _gameScore.Play();
        _scoreFade = CreateTween().SetParallel();
        _scoreFade.TweenProperty(_menuScore, "volume_db", -60f, 1.25);
        _scoreFade.TweenProperty(_gameScore, "volume_db", -2f, 1.25);
        _scoreFade.Chain().TweenCallback(Callable.From(() => _menuScore.Stop()));
    }
    public void SetZone(CaveZone zone) => _zone = zone;
    public void SetCorePresence(float proximity,bool active)
    {
        _coreTarget = active && float.IsFinite(proximity) ? Mathf.Pow(Mathf.Clamp(proximity,0,1),2)*0.4f : 0f;
        if (!active)
        {
            _coreLevel=0; _coreRumble?.Stop(); _coreAccent?.Stop();
        }
    }
    public override void _ExitTree()
    {
        // Release playback before a menu reload replaces the whole world.
        if(_music!=null) { _music.Stop(); _music.Stream=null; }
        if(_coreAccent!=null) { _coreAccent.Stop(); _coreAccent.Stream=null; }
        if(_coreRumble!=null) { _coreRumble.Stop(); _coreRumble.Stream=null; }
        _scoreFade?.Kill();
        foreach (var score in new[] { _menuScore, _gameScore })
            if (score != null) { score.Stop(); score.Stream = null; }
    }
    public void PlayCoreAccent() => _coreAccent?.Play();
    public override void _Process(double delta)
    {
        if (GetTree().Paused || _music == null) return;
        float target = _zone == CaveZone.Core ? 0.78f : _zone == CaveZone.Magma ? 0.88f : _zone == CaveZone.Crystal ? 1.08f : 1f;
        _zonePitch = Mathf.Lerp(_zonePitch,target,1f-Mathf.Exp(-(float)delta*1.2f));
        _music.PitchScale = _zonePitch;
        _coreLevel = Mathf.MoveToward(_coreLevel,_coreTarget,(float)delta*0.35f);
        if (_coreRumble!=null)
        {
            if(_coreLevel>0.0001f)
            {
                if(!_coreRumble.Playing) _coreRumble.Play();
                _coreRumble.VolumeDb = Mathf.LinearToDb(_coreLevel);
            }
            else if(_coreRumble.Playing) _coreRumble.Stop();
        }
    }
}
