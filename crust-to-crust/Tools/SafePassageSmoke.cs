using Godot;
using System;

public partial class SafePassageSmoke : Node
{
    private void Check(bool ok,string message) { if (!ok) throw new Exception(message); }
    public override void _Ready()
    {
        try
        {
            var player = GD.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Node2D>();
            var shape = (RectangleShape2D)player.GetNode<CollisionShape2D>("Hitbox/hitbox").Shape;
            Check(shape.Size.Length()*0.5f<=SafePassage.PlayerRadius,"player radius contains hitbox at all rotations");
            player.Free();
            var section = GD.Load<PackedScene>("res://Scenes/Environment/TunnelSegment.tscn").Instantiate<Node2D>();
            AddChild(section);
            float envelope = SafePassage.Amplitude+SafePassage.HalfWidth+SafePassage.PlayerRadius+SafePassage.Clearance;
            foreach (Node node in section.FindChildren("*","CollisionPolygon2D",true,false))
            {
                var polygon = (CollisionPolygon2D)node;
                int side = Math.Sign(polygon.ToGlobal(polygon.Polygon[0]).X);
                foreach (Vector2 point in polygon.Polygon)
                    Check(side*polygon.ToGlobal(point).X>envelope,"static cave collision stays outside protected envelope");
            }
            int samples = 0;
            foreach (int variant in new[] {0,1,3,4,5})
            {
                float radius = SafePassage.ColliderRadius(variant)*SafePassage.MaxSpawnScale*SafePassage.AppearOvershoot;
                for (int depth = 0; depth < 40000; depth += 73)
                {
                    float lane = SafePassage.Center(depth,-200);
                    foreach (int side in new[] {-1,1})
                    {
                        Check(SafePassage.TryBounds(side,lane,radius,-265,270,out float min,out float max),"bounds remain valid throughout the cave");
                        foreach (float desired in new[] {-1000f,0f,1000f})
                        {
                            float x = SafePassage.Clamp(desired,min,max);
                            Check(Mathf.Abs(x-lane)-radius >= SafePassage.HalfWidth+SafePassage.PlayerRadius+SafePassage.Clearance-0.001f,"clamped hazard keeps corridor clear");
                            samples++;
                        }
                    }
                }
            }
            GD.Print($"SAFE PASSAGE SMOKE PASSED: {samples} positions, static collision envelope and player hitbox.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
