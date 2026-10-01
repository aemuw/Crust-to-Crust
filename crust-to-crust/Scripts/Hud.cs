using Godot;

public partial class Hud : CanvasLayer
{
    private Label _scoreLabel, _depthLabel, _heatLabel;
    private Control _gameOverPanel, _startMenuPanel, _pauseOverlay;
    private Button _pauseButton, _resumeButton;
    private bool _started, _finished;
    private static bool _restartImmediately;
    [Signal] public delegate void StartRequestedEventHandler();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        var root = GetNode<Control>("Control");
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        _scoreLabel = GetNode<Label>("Control/ScoreLabel");
        _depthLabel = GetNode<Label>("Control/DepthLabel");
        _heatLabel = GetNode<Label>("Control/HeatLabel");
        _gameOverPanel = GetNode<Control>("Control/GameOverPanel");
        _startMenuPanel = GetNode<Control>("Control/StartMenuPanel");
        GetNode<Button>("Control/GameOverPanel/RestartButton").Pressed += Restart;
        GetNode<Button>("Control/StartMenuPanel/StartButton").Pressed += () =>
        {
            _started = true;
            _startMenuPanel.Hide();
            _pauseButton.Show();
            EmitSignal(SignalName.StartRequested);
        };
        var theme = new Theme();
        theme.DefaultFontSize = 22;
        theme.SetStylebox("normal", "Button", Card(new Color("203b48"), new Color("497283"), 12));
        theme.SetStylebox("hover", "Button", Card(new Color("315b68"), new Color("8ddcdb"), 12));
        theme.SetStylebox("pressed", "Button", Card(new Color("152c36"), new Color("8ddcdb"), 12));
        theme.SetStylebox("focus", "Button", Card(new Color(0,0,0,0), new Color("a4efe3"), 12));
        theme.SetStylebox("panel", "Panel", Card(new Color("12232f"), new Color("4e7783"), 20));
        root.Theme = theme;
        _pauseButton = new Button { Text = "Ⅱ  PAUSE", Visible = false, AnchorLeft = 1, AnchorRight = 1,
            OffsetLeft = -166, OffsetRight = -24, OffsetTop = 20, OffsetBottom = 66 };
        root.AddChild(_pauseButton);
        _pauseButton.Pressed += TogglePause;
        _pauseOverlay = new Control { Visible = false };
        root.AddChild(_pauseOverlay);
        _pauseOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0.025f, 0.045f, 0.07f, 0.84f) };
        _pauseOverlay.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var center = new CenterContainer();
        _pauseOverlay.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(430, 0) };
        center.AddChild(panel);
        panel.AddThemeStyleboxOverride("panel", Card(new Color("12232f"), new Color("4e7783"), 22));
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 32);
        panel.AddChild(margin);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 16);
        margin.AddChild(box);
        var eyebrow = new Label { Text = "C R U S T   T O   C R U S T", HorizontalAlignment = HorizontalAlignment.Center };
        eyebrow.AddThemeColorOverride("font_color", new Color("8ddcdb"));
        eyebrow.AddThemeFontSizeOverride("font_size", 16);
        box.AddChild(eyebrow);
        var title = new Label { Text = "Take a breath", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 36);
        box.AddChild(title);
        box.AddChild(new Label { Text = "Your journey can wait a little.", HorizontalAlignment = HorizontalAlignment.Center });
        _resumeButton = AddButton(box, "Continue journey", TogglePause);
        AddButton(box, "Start again", Restart);
        AddButton(box, "Main menu", () => Reload(false));
        var sound = new CheckButton { Text = "Sound", ButtonPressed = true };
        box.AddChild(sound);
        sound.Toggled += enabled => AudioServer.SetBusMute(0, !enabled);
        sound.ButtonPressed = !AudioServer.IsBusMute(0);
        box.AddChild(new Label { Text = "A / D or ← / → to move  •  Esc to resume", HorizontalAlignment = HorizontalAlignment.Center });
        GetNode<Button>("Control/StartMenuPanel/StartButton").GrabFocus();
        if (_restartImmediately)
        {
            _restartImmediately = false;
            Callable.From(() => GetNode<Button>("Control/StartMenuPanel/StartButton").EmitSignal(Button.SignalName.Pressed)).CallDeferred();
        }
    }

    private static StyleBoxFlat Card(Color fill, Color edge, int radius)
    {
        return new StyleBoxFlat { BgColor = fill, BorderColor = edge,
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 12, ContentMarginBottom = 12,
            ShadowColor = new Color(0, 0, 0, 0.3f), ShadowSize = 12 };
    }
    private static Button AddButton(VBoxContainer parent, string text, System.Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 48) };
        parent.AddChild(button);
        button.Pressed += action;
        return button;
    }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape && _started && !_finished)
        {
            TogglePause();
            GetViewport().SetInputAsHandled();
        }
    }
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut && _started && !_finished && !GetTree().Paused) TogglePause();
    }
    private void TogglePause()
    {
        if (!_started || _finished) return;
        GetTree().Paused = !GetTree().Paused;
        _pauseOverlay.Visible = GetTree().Paused;
        _pauseButton.Visible = !GetTree().Paused;
        if (GetTree().Paused) _resumeButton.GrabFocus();
        else GetViewport().GuiReleaseFocus();
    }
    public void UpdateScore(int score) => _scoreLabel.Text = $"COINS: {score}";
    public void UpdateDepth(float depth) => _depthLabel.Text = $"DEPTH: {Mathf.FloorToInt(depth)} M";
    public void SetHeatWarning(bool visible) => _heatLabel.Visible = visible;
    public void ShowGameOver()
    {
        _finished = true;
        _pauseOverlay.Hide();
        _pauseButton.Hide();
        _heatLabel.Hide();
        _gameOverPanel.Show();
        GetNode<Button>("Control/GameOverPanel/RestartButton").GrabFocus();
    }
    private void Restart() => Reload(true);
    private void Reload(bool startImmediately)
    {
        _restartImmediately = startImmediately;
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }
}
