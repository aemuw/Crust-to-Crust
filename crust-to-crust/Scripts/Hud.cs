using Godot;

public partial class Hud : CanvasLayer
{
    private Label _scoreLabel;
    private Label _depthLabel;
    private Label _heatLabel;
    private Control _gameOverPanel;
    private Button _restartButton;
    private Control _startMenuPanel;
    private Button _startButton;

    [Signal]
    public delegate void StartRequestedEventHandler();

    public override void _Ready()
    {
        _scoreLabel = GetNode<Label>("Control/ScoreLabel");
        _depthLabel = GetNode<Label>("Control/DepthLabel");
        _heatLabel = GetNode<Label>("Control/HeatLabel");
        _gameOverPanel = GetNode<Control>("Control/GameOverPanel");
        _restartButton = GetNode<Button>("Control/GameOverPanel/RestartButton");
        _startMenuPanel = GetNodeOrNull<Control>("Control/StartMenuPanel");
        _startButton = GetNodeOrNull<Button>("Control/StartMenuPanel/StartButton");

        _restartButton.Pressed += OnRestartPressed;
        if (_startButton != null)
        {
            _startButton.Pressed += OnStartPressed;
        }
    }

    private void OnStartPressed()
    {
        if (_startMenuPanel != null)
        {
            _startMenuPanel.Visible = false;
        }
        EmitSignal(SignalName.StartRequested);
    }

    public void UpdateScore(int score)
    {
        _scoreLabel.Text = $"COINS: {score}";
    }

    public void UpdateDepth(float depth)
    {
        _depthLabel.Text = $"DEPTH: {Mathf.FloorToInt(depth)} M";
    }

    public void SetHeatWarning(bool visible)
    {
        _heatLabel.Visible = visible;
        if (visible)
        {
            _heatLabel.Text = "CORE ZONE! FIND WATER!";
        }
    }

    public void ShowGameOver()
    {
        _gameOverPanel.Visible = true;
    }

    private void OnRestartPressed()
    {
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }
}
