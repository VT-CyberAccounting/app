using UnityEngine;

public static class CameraRig
{
    private static Transform _cached;

    public static Transform MainTransform
    {
        get
        {
            if (_cached == null)
            {
                Camera cam = Camera.main;
                if (cam != null) _cached = cam.transform;
            }
            return _cached;
        }
    }
}
