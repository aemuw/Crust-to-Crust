using Godot;
using System;
using System.Collections.Generic;

public readonly record struct ProjectilePath(Vector2 Position,Vector2 Velocity);

/// <summary>Reachable-position search in time, including scrolling solids and every live shot.</summary>
public static class HazardEncounterRules
{
    public static bool HasEscape(float playerX,float horizontalSpeed,float radius,float scrollSpeed,
        IReadOnlyList<ProjectilePath> shots,IReadOnlyList<Rect2> solids,
        Func<float,float,bool> additionalBlocked=null)
    {
        const int count=53; const float spacing=10f, step=0.04f;
        bool[] reachable=new bool[count], next=new bool[count];
        for(int i=0;i<count;i++) reachable[i]=Mathf.Abs(-260+i*spacing-playerX)<=spacing;
        for(int tick=1;tick<=50;tick++)
        {
            float time=tick*step; bool any=false;
            for(int i=0;i<count;i++)
            {
                float x=-260+i*spacing;
                bool blocked=Mathf.Abs(x)>268-radius || additionalBlocked?.Invoke(x,time)==true;
                var point=new Vector2(x,-200);
                foreach(var solid in solids)
                    if(new Rect2(solid.Position-new Vector2(0,scrollSpeed*time),solid.Size).Grow(radius+8).HasPoint(point)) { blocked=true; break; }
                if(!blocked)
                    foreach(var shot in shots)
                    {
                        Vector2 velocity=shot.Velocity-new Vector2(0,scrollSpeed);
                        Vector2 from=shot.Position+velocity*(time-step), to=shot.Position+velocity*time;
                        if(HazardProjectile.SweepHits(from,to,point,radius+12)) { blocked=true; break; }
                    }
                next[i]=false;
                if(!blocked)
                    for(int j=Math.Max(0,i-2);j<=Math.Min(count-1,i+2);j++)
                        if(reachable[j] && Math.Abs(i-j)*spacing<=horizontalSpeed*step)
                        { next[i]=true; any=true; break; }
            }
            if(!any) return false;
            (reachable,next)=(next,reachable);
        }
        return true;
    }
}
