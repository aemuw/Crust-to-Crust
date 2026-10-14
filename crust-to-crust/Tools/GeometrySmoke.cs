using Godot;
using System;
using System.Linq;

public partial class GeometrySmoke : Node
{
    private static void Check(bool ok,string text) { if (!ok) throw new Exception(text); GD.Print("PASS: "+text); }
    public override async void _Ready()
    {
        try
        {
            var level = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var sections = level.GetNode<Node2D>("TunnelSegments").GetChildren().OfType<Node2D>().OrderBy(s=>s.Position.Y).ToArray();
            for (int i=0;i<sections.Length-1;i++)
            {
                var a=(ShaderMaterial)sections[i].GetNode<Polygon2D>("DeepRock").Material;
                var b=(ShaderMaterial)sections[i+1].GetNode<Polygon2D>("DeepRock").Material;
                Check(a.GetShaderParameter("texture_origin").VariantType==Variant.Type.Vector2,"background has continuous logical coordinates");
                Check(Mathf.IsEqualApprox(a.GetShaderParameter("texture_origin").AsVector2().Y+720,b.GetShaderParameter("texture_origin").AsVector2().Y),"adjacent UV origins meet exactly");
            }
            foreach (Node node in level.GetChildren())
                if (node is Obstacle obstacle)
                {
                    obstacle.ConfigureVariant(1);
                    var shadow=obstacle.GetChildren().OfType<LightOccluder2D>().First();
                    Check(shadow.Occluder.Polygon.Length>4,"shard shadow follows textured silhouette rather than common square");
                    break;
                }
            level.BeginFalling(); level.CurrentDepth=10000; level.FallSpeed=900; level._Process(1);
            level.StopFalling(); level.RebaseEmergence();
            float? baseOrigin=null;
            foreach(var section in sections)
            {
                var material=(ShaderMaterial)section.GetNode<Polygon2D>("DeepRock").Material;
                float logicalBase=material.GetShaderParameter("texture_origin").AsVector2().Y-section.Position.Y;
                if(baseOrigin.HasValue) Check(Mathf.IsEqualApprox(baseOrigin.Value,logicalBase),"cinematic rebase keeps adjacent texture coordinates continuous");
                baseOrigin=logicalBase;
            }
            level.Free();
            await ToSignal(GetTree().CreateTimer(0.15),SceneTreeTimer.SignalName.Timeout);
            GD.Print("GEOMETRY SMOKE PASSED"); GetTree().Quit();
        }
        catch(Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
}
