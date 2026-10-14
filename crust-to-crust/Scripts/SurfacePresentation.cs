using Godot;

public partial class SurfacePresentation : Node2D
{
    private ShaderMaterial _sky;
    private float _clock;
    public override void _Ready()
    {
        Node2D surface=GetParent<Node2D>();
        foreach(Node child in surface.GetChildren())
        {
            if(child is CanvasItem art && child!=this) art.Material=GameAssets.Unshaded;
            if(child.Name=="GrassLeft" || child.Name=="GrassRight" || child.Name=="Moon" || child.Name=="MoonGlow" || child.Name=="MountainsFar" || child.Name=="LeftRim" || child.Name=="RightRim") ((CanvasItem)child).Hide();
        }
        _sky=new ShaderMaterial { Shader=GD.Load<Shader>("res://Shaders/SurfaceSky.gdshader") };
        _sky.SetShaderParameter("night_texture",GD.Load<Texture2D>("res://Textures/ArtRefresh/surface_night.png"));
        surface.GetNode<ColorRect>("Sky").Material=_sky;
        foreach(int side in new[] {-1,1})
        {
            var points=new[] {new Vector2(-5000,-450),new Vector2(-600,-450),new Vector2(-490,-456),new Vector2(-430,-469),new Vector2(-350,-450),new Vector2(-315,-330),new Vector2(-340,300),new Vector2(-5000,300)};
            if(side>0) for(int i=0;i<points.Length;i++) points[i]=new Vector2(-points[i].X,points[i].Y);
            AddChild(new Polygon2D { Polygon=points,Texture=GD.Load<Texture2D>("res://Textures/ArtRefresh/cave_basalt.png"),TextureRepeat=TextureRepeatEnum.Enabled,Color=new Color("48575e"),Material=GameAssets.Unshaded,ZIndex=1 });
        }
        var rim=new Sprite2D { Texture=GameAssets.Halo,Position=new Vector2(0,-450),Scale=new Vector2(660,45)/GameAssets.Halo.GetSize(),Modulate=new Color(0.35f,0.7f,0.8f,0.11f),Material=GameAssets.Unshaded };
        AddChild(rim);
    }
    public override void _Process(double delta)
    { _clock+=(float)delta; _sky.SetShaderParameter("clock",_clock); }
}
