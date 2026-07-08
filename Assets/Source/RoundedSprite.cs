using System.Collections.Generic;
using UnityEngine;

public static class RoundedSprite
{
    [System.Flags]
    public enum Corner
    {
        None = 0,
        BottomLeft = 1,
        BottomRight = 2,
        TopLeft = 4,
        TopRight = 8,
        All = BottomLeft | BottomRight | TopLeft | TopRight
    }

    private static readonly Dictionary<int, Sprite> _cache = new Dictionary<int, Sprite>();

    public static Sprite Get(int radius) => Get(radius, Corner.All);

    public static Sprite Get(int radius, Corner corners)
    {
        int key = (radius << 4) | (int)corners;
        if (_cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        int size = radius * 2 + 2;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool left = x < radius;
                bool right = x >= size - radius;
                bool bottom = y < radius;
                bool top = y >= size - radius;

                Corner corner =
                    left && bottom ? Corner.BottomLeft :
                    right && bottom ? Corner.BottomRight :
                    left && top ? Corner.TopLeft :
                    right && top ? Corner.TopRight : Corner.None;

                byte a = 255;
                if (corner != Corner.None && (corners & corner) != 0)
                {
                    float dx = left ? radius - x : x - (size - radius - 1);
                    float dy = bottom ? radius - y : y - (size - radius - 1);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= radius) a = 255;
                    else if (dist <= radius + 1f) a = (byte)(255f * (1f - (dist - radius)));
                    else a = 0;
                }
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        Vector4 border = new Vector4(radius, radius, radius, radius);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        _cache[key] = sprite;
        return sprite;
    }
}
