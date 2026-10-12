using Godot;

/// <summary>Air streaks follow actual fall speed; pauses freeze both trail position and phase.</summary>
public partial class WindTrails : Node2D
{
    private const int Count = 30;
    private readonly Vector2[] _points = new Vector2[Count];
    private readonly float[] _weights = new float[Count];
    private float _speed, _sideways, _opacity;
    public override void _Ready()
    {
        ZIndex = 12;
        Material = new CanvasItemMaterial { LightMode = CanvasItemMaterial.LightModeEnum.Unshaded };
        for (int i = 0; i < Count; i++)
        {
            _points[i] = new Vector2((float)GD.RandRange(-230,230), (float)GD.RandRange(-500,500));
            _weights[i] = (float)GD.RandRange(0.5,1.0);
        }
    }
    public void SetMotion(float speed, float sideways, bool active)
    {
        _speed = active ? speed : 0f; _sideways = sideways;
        Visible = active;
    }
    public override void _Process(double delta)
    {
        float dt = (float)delta;
        float target = Mathf.Clamp((_speed - 260f) / 500f,0f,1f) * 0.34f;
        _opacity = Mathf.Lerp(_opacity,target,1f-Mathf.Exp(-dt*4f));
        for (int i = 0; i < Count; i++)
        {
            _points[i] += new Vector2(-_sideways*0.1f,-_speed*(0.8f+_weights[i]*0.7f))*dt;
            if (_points[i].Y < -520) _points[i] = new Vector2((float)GD.RandRange(-230,230),520);
        }
        QueueRedraw();
    }
    public override void _Draw()
    {
        for (int i = 0; i < Count; i++)
        {
            float edge = Mathf.Clamp((Mathf.Abs(_points[i].X)-55f)/180f,0.15f,1f);
            float length = Mathf.Lerp(12f,68f,Mathf.Clamp(_speed/760f,0f,1f))*_weights[i];
            DrawLine(_points[i],_points[i]+new Vector2(_sideways*0.018f,length),
                new Color(0.65f,0.83f,0.91f,_opacity*edge*_weights[i]),1f,true);
        }
    }
}
