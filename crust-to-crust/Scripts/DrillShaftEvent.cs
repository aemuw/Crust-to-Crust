using Godot;

public enum DrillShaftPhase { Dormant, Warning, Passages, Recovery }
public partial class DrillShaftEvent : Node2D
{
    private LevelGenerator _level;
    private readonly DrillGate[] _gates=new DrillGate[3];
    private HazardWarning _leftCue,_rightCue;
    private AudioStreamPlayer2D _motor,_engage;
    private float _recovery;
    public DrillShaftPhase Phase { get; private set; }
    public bool Active=>Phase!=DrillShaftPhase.Dormant;
    public float Elapsed { get; private set; }
    public int ArmedGates { get; private set; }
    public int ActiveColliders { get { int n=0;foreach(var gate in _gates) if(gate?.CollisionActive==true)n++;return n; } }
    public override void _Ready()
    {
        Material=GameAssets.Unshaded; ZIndex=2;
        for(int i=0;i<3;i++) { _gates[i]=new DrillGate { Name="Gate"+i }; AddChild(_gates[i]); }
        _leftCue=new HazardWarning { Name="LeftWarning" }; AddChild(_leftCue);
        _rightCue=new HazardWarning { Name="RightWarning" }; AddChild(_rightCue);
        var stream=GD.Load<AudioStreamWav>("res://Audio/SFX/drill_motor.wav");
        stream.LoopMode=AudioStreamWav.LoopModeEnum.Forward; stream.LoopBegin=0; stream.LoopEnd=Mathf.RoundToInt((float)stream.GetLength()*stream.MixRate);
        _motor=new AudioStreamPlayer2D { Name="ShaftMotor",Stream=stream,Bus=AudioManager.SfxBus,VolumeDb=-17,MaxDistance=2000,Attenuation=.3f }; AddChild(_motor);
        _engage=new AudioStreamPlayer2D { Name="ShaftEngage",Stream=GD.Load<AudioStream>("res://Audio/SFX/drill_engage.wav"),Bus=AudioManager.SfxBus,VolumeDb=-9,MaxDistance=2400,Attenuation=.3f }; AddChild(_engage);
        Deactivate();
    }
    public void Activate(LevelGenerator level)
    {
        _level=level; Elapsed=_recovery=0; ArmedGates=0; Phase=DrillShaftPhase.Warning; Modulate=Colors.White; Show(); ProcessMode=ProcessModeEnum.Inherit;
        for(int i=0;i<3;i++) _gates[i].Activate(level,i,level.Runner.FixedY+level.FallSpeed*(3.35f+i*1.15f));
        _motor.Position=new Vector2(0,level.Runner.FixedY+130); _motor.Play(); Warn();
    }
    private void Warn()
    {
        if(ArmedGates>=3) return;
        _level.GetRockBounds(_gates[ArmedGates].Position.Y,out float left,out _);
        _engage.Position=new Vector2(left,_gates[ArmedGates].Position.Y);
        Vector2 listener=GetViewport().GetCanvasTransform().AffineInverse()*GetViewport().GetVisibleRect().GetCenter();
        _engage.MaxDistance=Mathf.Max(1600,ToGlobal(_engage.Position).DistanceTo(listener)*1.7f); _engage.Play();
    }
    public void Deactivate()
    {
        Phase=DrillShaftPhase.Dormant; Hide(); _motor?.Stop(); _engage?.Stop(); _leftCue?.Hide(); _rightCue?.Hide();
        foreach(var gate in _gates) gate?.Deactivate(); ProcessMode=ProcessModeEnum.Disabled;
    }
    private void Recover()
    {
        Phase=DrillShaftPhase.Recovery; _recovery=0; _leftCue.Hide(); _rightCue.Hide(); _motor.Stop(); _engage.Stop();
        foreach(var gate in _gates) gate.Disarm();
    }
    public override void _Process(double delta)
    {
        float remaining=Mathf.Max(0,(float)delta);
        while(remaining>0 && Active)
        {
            float dt=Mathf.Min(remaining,Mathf.Min(.01f,12/Mathf.Max(1,_level.FallSpeed)));remaining-=dt;Elapsed+=dt;
            if(!_level.IsGameActive) { Deactivate();return; }
            if(Phase!=DrillShaftPhase.Recovery && !_level.CanContinueCollapseEvent(_gates[Mathf.Min(ArmedGates,2)].Position.Y)) Recover();
            if(Phase==DrillShaftPhase.Recovery)
            {
                _recovery+=dt; Modulate=new Color(1,1,1,Mathf.Clamp(1-_recovery/1.2f,0,1));
                if(_recovery>=1.6f) { Deactivate();return; }
            }
            else
            {
                if(ArmedGates<3 && Elapsed>=1.35f+ArmedGates*1.15f) { _gates[ArmedGates++].Arm();Phase=DrillShaftPhase.Passages;Warn(); }
                if(ArmedGates==3 && _gates[2].Position.Y<_level.Runner.FixedY-_level.FallSpeed*.5f) Recover();
            }
            foreach(var gate in _gates) gate.Step(dt);
        }
        bool warning=Active && Phase!=DrillShaftPhase.Recovery && ArmedGates<3;
        if(warning)
        {
            float y=_gates[ArmedGates].Position.Y; _level.GetRockBounds(y,out float left,out float right);
            float start=ArmedGates==0 ? 0 : 1.35f+(ArmedGates-1)*1.15f;
            float progress=(Elapsed-start)/(ArmedGates==0 ? 1.35f : 1.15f);
            Vector2 a=ToGlobal(new Vector2(left,y)),b=ToGlobal(new Vector2(right,y));
            _leftCue.SetCue(true,a,a,progress,new Color("c5ad70"),collapse:true); _rightCue.SetCue(true,b,b,progress,new Color("c5ad70"),collapse:true);
        }
        else { _leftCue.Hide();_rightCue.Hide(); }
        QueueRedraw();
    }
    public override void _Draw()
    {
        if(!Active) return;
        // Cables follow the actual rock profile between the bolted frames.
        for(int side=0;side<2;side++)
        {
            Vector2[] cable=new Vector2[25];
            for(int i=0;i<cable.Length;i++)
            {
                float t=(float)i/(cable.Length-1),y=Mathf.Lerp(_gates[0].Position.Y-100,_gates[2].Position.Y+100,t);
                _level.GetRockBounds(y,out float left,out float right);
                cable[i]=new Vector2((side==0 ? left+18 : right-18)+Mathf.Sin(t*7)*5,y);
            }
            for(int i=0;i<cable.Length-1;i++)
            {
                Vector2 segment=cable[i+1]-cable[i];
                DrawSetTransform(cable[i],segment.Angle()-Mathf.Pi/2);
                DrawTextureRect(CaveArt.Cable,new Rect2(-2,0,4,segment.Length()+1),false,new Color(.5f,.5f,.5f));
            }
            DrawSetTransform(Vector2.Zero);
        }
    }
}
