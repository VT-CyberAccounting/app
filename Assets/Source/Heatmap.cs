using UnityEngine;

public static class Heatmap
{
    private static readonly Color[] Stops = {
        new Color(0.0f, 0.0f, 1.0f),
        new Color(0.0f, 1.0f, 1.0f),
        new Color(0.0f, 1.0f, 0.0f),
        new Color(1.0f, 1.0f, 0.0f),
        new Color(1.0f, 0.0f, 0.0f)
    };

    public static Color Sample(float t)
    {
        t = Mathf.Clamp01(t);
        float scaled = t * (Stops.Length - 1);
        int lower = Mathf.FloorToInt(scaled);
        int upper = Mathf.Min(lower + 1, Stops.Length - 1);
        float frac = scaled - lower;
        return Color.Lerp(Stops[lower], Stops[upper], frac);
    }
}
