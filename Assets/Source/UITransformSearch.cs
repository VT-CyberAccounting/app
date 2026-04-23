using UnityEngine;

public static class UITransformSearch
{
    public static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindDeep(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }
}
