using Godot;
using System;

public partial class AudioSmoke : Node
{
    private async System.Threading.Tasks.Task Wait() => await ToSignal(GetTree().CreateTimer(1.6), SceneTreeTimer.SignalName.Timeout);
    public override async void _Ready()
    {
        try
        {
            var level = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>();
            AddChild(level);
            var audio = level.GetNode<AudioManager>("AudioManager");
            await Wait();
            var ambience = audio.GetNode<AudioStreamPlayer>("Music");
            var menu = audio.GetNode<AudioStreamPlayer>("MenuScore");
            var game = audio.GetNode<AudioStreamPlayer>("GameScore");
            if (!ambience.Playing || !menu.Playing || game.Playing) throw new Exception("Menu must mix original ambience with lullaby only");
            float savedVolume = SaveData.MusicVolume;
            SaveData.MusicVolume = 0;
            AudioManager.ApplyVolumes();
            bool muted = AudioServer.IsBusMute(AudioServer.GetBusIndex(AudioManager.MusicBus));
            SaveData.MusicVolume = savedVolume;
            AudioManager.ApplyVolumes();
            if (!muted || menu.Bus != AudioManager.MusicBus || game.Bus != AudioManager.MusicBus)
                throw new Exception("Existing music setting must control both new scores");
            foreach (var player in new[] { menu, game })
                if (player.Stream is not AudioStreamWav wav || wav.LoopMode != AudioStreamWav.LoopModeEnum.Forward || wav.GetLength() < 45)
                    throw new Exception("Both scores must have complete looping audio");
            level.StartGame();
            await Wait();
            if (!game.Playing || menu.Playing || !ambience.Playing || game.VolumeDb < -10) throw new Exception("Starting a run must crossfade to gameplay and preserve ambience");
            audio.SetZone(CaveZone.Core);
            await Wait();
            if (game.PitchScale != 1) throw new Exception("Zone ambience must not detune the score");
            GetTree().Paused = true;
            await Wait();
            if (!game.Playing) throw new Exception("Pause must preserve music playback");
            GetTree().Paused = false;
            level.Free();
            await Wait();
            GD.Print("AUDIO SMOKE PASSED"); GetTree().Quit();
        }
        catch (Exception ex) { GetTree().Paused = false; GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
}
