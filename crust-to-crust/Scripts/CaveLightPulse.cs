using Godot;
using System.Collections.Generic;

/// <summary>
/// Один вузол оновлює мерехтіння всіх кристалічних ліхтарів (~20 разів/с).
/// Пульсує лише TextureScale (±3.5%), тому не конфліктує з керуванням Energy/Enabled у LevelGenerator.
/// </summary>
public partial class CaveLightPulse : Node
{
    private readonly List<PointLight2D> _lights = new();
    private readonly List<float> _baseScale = new();
    private readonly List<float> _phase = new();
    private float _time, _accum;
    private const float Step = 0.05f;

    public void Register(PointLight2D light)
    {
        _lights.Add(light);
        _baseScale.Add(light.TextureScale);
        _phase.Add(GD.Randf()*Mathf.Tau);
    }

    public override void _Process(double delta)
    {
        _accum += (float)delta;
        if (_accum < Step) return;
        _time += _accum; _accum = 0f;
        for (int i = 0; i < _lights.Count; i++)
        {
            var light = _lights[i];
            if (!IsInstanceValid(light) || !light.Enabled) continue;
            float k = 1f + 0.035f*Mathf.Sin(_time*1.7f + _phase[i]) + 0.015f*Mathf.Sin(_time*5.3f + _phase[i]*2f);
            light.TextureScale = _baseScale[i]*k;
        }
    }
}
