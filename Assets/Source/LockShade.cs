using UnityEngine;
using UnityEngine.UI;

public class LockShade : MonoBehaviour
{
    public static LockShade Attach(Transform parent, float cornerRadius,
        RoundedSprite.Corner corners = RoundedSprite.Corner.All,
        float insetLeft = 0f, float insetBottom = 0f, float insetRight = 0f, float insetTop = 0f)
    {
        GameObject go = new GameObject("LockShade");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(insetLeft, insetBottom);
        rt.offsetMax = new Vector2(-insetRight, -insetTop);

        Image dim = go.AddComponent<Image>();
        if (cornerRadius > 0f && corners != RoundedSprite.Corner.None)
        {
            dim.sprite = RoundedSprite.Get((int)cornerRadius, corners);
            dim.type = Image.Type.Sliced;
        }
        dim.color = MetaTokens.LockDim;
        dim.raycastTarget = false;

        LockShade shade = go.AddComponent<LockShade>();
        go.transform.SetAsLastSibling();
        go.SetActive(false);
        return shade;
    }

    public void SetLocked(bool locked)
    {
        if (gameObject.activeSelf == locked) return;
        if (locked) transform.SetAsLastSibling();
        gameObject.SetActive(locked);
    }
}
