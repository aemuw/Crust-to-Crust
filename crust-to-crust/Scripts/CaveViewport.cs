using Godot;

/// <summary>Visibility in the actual camera, including zoom, rotation and tall windows.</summary>
public static class CaveViewport
{
    public static bool Intersects(Node2D node,float radius)
    {
        Transform2D canvas=node.GetViewport().GetCanvasTransform();
        float pixels=radius*Mathf.Max(canvas.X.Length(),canvas.Y.Length());
        return node.GetViewport().GetVisibleRect().Grow(pixels+24).HasPoint(canvas*node.GlobalPosition);
    }
    public static bool PassedTop(Node2D node,float radius)
    {
        Transform2D canvas=node.GetViewport().GetCanvasTransform();
        float pixels=radius*Mathf.Max(canvas.X.Length(),canvas.Y.Length());
        return (canvas*node.GlobalPosition).Y+pixels<node.GetViewport().GetVisibleRect().Position.Y-64;
    }
    public static float WorldTop(Node2D node)
    {
        Transform2D inverse=node.GetViewport().GetCanvasTransform().AffineInverse();
        Rect2 rect=node.GetViewport().GetVisibleRect();
        return Mathf.Min(Mathf.Min((inverse*rect.Position).Y,(inverse*rect.End).Y),
            Mathf.Min((inverse*new Vector2(rect.End.X,rect.Position.Y)).Y,(inverse*new Vector2(rect.Position.X,rect.End.Y)).Y));
    }
}
