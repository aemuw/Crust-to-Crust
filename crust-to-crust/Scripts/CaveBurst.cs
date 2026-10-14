using Godot;

/// <summary>Short-lived cinematic dust; visual randomness cannot alter encounter seeds.</summary>
public partial class CaveBurst : Node2D
{
    private readonly Vector2[] _points=new Vector2[28],_velocities=new Vector2[28];
    private readonly float[] _sizes=new float[28];
    public Color Tint=new("b0a38c");
    public float Direction=-1;
    private float _time;
    public override void _Ready()
    {
        ZIndex=15; Material=GameAssets.Unshaded;
        for(int i=0;i<_points.Length;i++)
        {
            _velocities[i]=new Vector2((float)GD.RandRange(-110,110),Direction*(float)GD.RandRange(45,170));
            _sizes[i]=(float)GD.RandRange(1.2,4.5);
        }
    }
    public override void _Process(double delta)
    {
        float dt=(float)delta; _time+=dt;
        for(int i=0;i<_points.Length;i++) { _points[i]+=_velocities[i]*dt; _velocities[i]=_velocities[i]*Mathf.Exp(-dt*2)+new Vector2(0,65*dt); }
        QueueRedraw(); if(_time>1.2f) QueueFree();
    }
    public override void _Draw()
    {
        Color color=Tint; color.A=Mathf.Clamp(1-_time/1.2f,0,1)*0.65f;
        for(int i=0;i<_points.Length;i++) DrawCircle(_points[i],_sizes[i]*(1+_time*0.7f),color);
    }
}
