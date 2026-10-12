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
	}
}
