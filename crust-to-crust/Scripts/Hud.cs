using Godot;

public partial class Hud : CanvasLayer
{
    private Label _scoreLabel;
    private Label _depthLabel;
    private Label _heatLabel;
    private Control _gameOverPanel;
    private Button _restartButton;

    public override void _Ready()
    {
        _scoreLabel = GetNode<Label>("Control/ScoreLabel");
        _depthLabel = GetNode<Label>("Control/DepthLabel");
        _heatLabel = GetNode<Label>("Control/HeatLabel");
        _gameOverPanel = GetNode<Control>("Control/GameOverPanel");
        _restartButton = GetNode<Button>("Control/GameOverPanel/RestartButton");

        _restartButton.Pressed += OnRestartPressed;
    }

    public void UpdateScore(int score)
    {
        _scoreLabel.Text = $"Монети: {score}";
    }

    public void UpdateDepth(float depth)
    {
        _depthLabel.Text = $"Глибина: {Mathf.FloorToInt(depth)} м";
    }

    public void SetHeatWarning(bool visible, float timeLeft = 0f)
    {
        _heatLabel.Visible = visible;
        if (visible)
        {
            _heatLabel.Text = $"ЗОНА ЯДРА! До згорання: {timeLeft:F1}с";
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
