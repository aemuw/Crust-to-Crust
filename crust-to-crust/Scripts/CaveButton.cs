using Godot;

// A restrained drawn accent: no generated button bitmap, layout movement or glow.
public partial class CaveButton : Button
{
    public bool Primary;
    private float _emphasis;
    public override void _Ready() { ProcessMode = ProcessModeEnum.Always; }
    public override void _Process(double delta)
    {
        float target = !Disabled && (IsHovered() || HasFocus()) ? 1 : 0;
        float next = Mathf.MoveToward(_emphasis, target, (float)delta * 7);
        if (!Mathf.IsEqualApprox(next, _emphasis)) { _emphasis = next; QueueRedraw(); }
    }
    public override void _Draw()
    {
        if (Disabled) return;
        Color accent = Primary ? new Color("eee2c9") : new Color("79b1a9");
        accent.A = 0.15f + _emphasis * 0.6f;
        DrawLine(new Vector2(16, Size.Y-3), new Vector2(16 + (Size.X-32)*_emphasis, Size.Y-3), accent, 1, true);
    }
}
