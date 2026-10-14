using Godot;

/// <summary>Trace once at loading time in sprite space, never at hazard spawn time.</summary>
public static class HazardSilhouettes
{
    private static readonly Vector2[][] Cached = new Vector2[6][];
    public static float CanvasSize(int variant) => variant is 1 or 4 ? 103f : variant==2 ? 145f : variant==3 ? 103f : variant==5 ? 70f : 87f;
    public static Vector2[] Outline(int variant)
    {
        if (Cached[variant]!=null) return Cached[variant];
        Texture2D texture=GameAssets.HazardTextures[variant];
        using var image=texture.GetImage();
        using var bitmap=new Bitmap();
        bitmap.CreateFromImageAlpha(image,0.35f);
        var polygons=bitmap.OpaqueToPolygons(new Rect2I(0,0,image.GetWidth(),image.GetHeight()),3f);
        Vector2[] largest=System.Array.Empty<Vector2>();
        float largestArea=0;
        foreach (Vector2[] polygon in polygons)
        {
            float area=0;
            for(int i=0;i<polygon.Length;i++) area+=polygon[i].Cross(polygon[(i+1)%polygon.Length]);
            if(Mathf.Abs(area)>largestArea) { largestArea=Mathf.Abs(area); largest=polygon; }
        }
        float scale=CanvasSize(variant)/texture.GetWidth();
        Vector2 center=texture.GetSize()*0.5f;
        for(int i=0;i<largest.Length;i++) largest[i]=(largest[i]-center)*scale;
        return Cached[variant]=largest;
    }
}
