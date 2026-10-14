using Godot;

/// <summary>Local hazard cues drawn in screen units, independent of camera and art scale.</summary>
public partial class HazardWarning : Node2D
{
    private Vector2 _source, _target, _left, _right;
    private Vector2 _worldSource, _worldTarget, _worldLeft, _worldRight;
    private Color _color;
    private float _progress;
    private bool _dash, _spread, _collapse;
    private readonly Color _ink=new(0.015f,0.025f,0.03f,0.94f);
    public override void _Ready() { TopLevel=true; ZIndex=20; Material=GameAssets.Unshaded; Hide(); }
    public void SetCue(bool active,Vector2 source,Vector2 target,float progress,Color color,bool dash=false,bool collapse=false)
    {
        Visible=active; if(!active) return;
        _worldSource=source; _worldTarget=target;
        _progress=Mathf.Clamp(progress,0,1); _color=color; _dash=dash; _spread=false; _collapse=collapse;
        Project();
    }
    public void SetSpread(Vector2 left,Vector2 right)
    {
        _worldLeft=left; _worldRight=right; _spread=true; Project();
    }
    public override void _Process(double delta) { if(Visible) Project(); }
    private void Project()
    {
        var canvas=GetViewport().GetCanvasTransform(); GlobalTransform=canvas.AffineInverse();
        _source=canvas*_worldSource; _target=canvas*_worldTarget;
        _left=canvas*_worldLeft; _right=canvas*_worldRight; QueueRedraw();
    }
    private void Trace(Vector2 target,Rect2 view)
    {
        if(!view.HasPoint(_source) || !view.HasPoint(target)) return;
        DrawDashedLine(_source,target,_ink,5,12,true);
        Color line=_color; line.A=0.55f; DrawDashedLine(_source,target,line,2,12,true);
        DrawCircle(target,11,_ink);
        DrawArc(target,11,0,Mathf.Tau,24,_color,1.5f,true);
        DrawLine(target+Vector2.Left*4,target+Vector2.Right*4,_color,1,true);
    }
    public override void _Draw()
    {
        Rect2 view=GetViewport().GetVisibleRect();
        Vector2 badge=new(Mathf.Clamp(_source.X,view.Position.X+24,view.End.X-24),
            Mathf.Clamp(_source.Y,view.Position.Y+158,view.End.Y-106));
        if(!_collapse) { Trace(_target,view); if(_spread) { Trace(_left,view); Trace(_right,view); } }
        DrawCircle(badge,17,_ink);
        DrawArc(badge,16,0,Mathf.Tau,32,new Color(_color,0.45f),1,true);
        DrawArc(badge,16,-Mathf.Pi/2,-Mathf.Pi/2+Mathf.Tau*_progress,32,_color,3,true);
        if(_collapse)
        {
            DrawPolyline(new[] {badge+new Vector2(0,-7),badge+new Vector2(7,6),badge+new Vector2(-7,6),badge+new Vector2(0,-7)},_color,1.5f,true);
            DrawLine(badge+Vector2.Up*2,badge+Vector2.Down*1,_color,2,true); DrawCircle(badge+Vector2.Down*4,1,_color);
        }
        else if(_dash)
        {
            float sign=_target.X>=_source.X ? 1 : -1;
            DrawPolyline(new[] {badge+new Vector2(-sign*4,-6),badge+new Vector2(sign*3,0),badge+new Vector2(-sign*4,6)},_color,2.5f,true);
        }
        else
            DrawPolyline(new[] {badge+Vector2.Up*6,badge+Vector2.Right*5,badge+Vector2.Down*6,badge+Vector2.Left*5,badge+Vector2.Up*6},_color,2,true);
        if(!view.HasPoint(_source))
        {
            Vector2 direction=(_source-badge).Normalized(), center=badge+direction*24;
            Vector2 side=direction.Orthogonal()*4;
            DrawColoredPolygon(new[] {center+direction*5,center-direction*3+side,center-direction*3-side},_color);
        }
    }
}
