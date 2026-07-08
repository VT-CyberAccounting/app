using UnityEngine;
using UnityEngine.UI;

public static class UiScrollbar
{
    private const float Width = 6f;
    private const float Inset = 12f;

    private static readonly Color TrackColor = MetaTokens.Alpha(MetaTokens.White, 0.05f);
    private static readonly Color HandleColor = MetaTokens.NeutralC0;

    public static Scrollbar Attach(ScrollRect scroll, float edgeInset = 0f, float topInset = 0f, float bottomInset = 0f)
    {
        if (scroll == null) return null;

        RectTransform viewport = scroll.viewport;
        RectTransform content = scroll.content;
        if (viewport == null) return null;

        int radius = (int)(Width * 0.5f);

        GameObject barObj = new GameObject("VScrollbar");
        barObj.transform.SetParent(viewport, false);
        RectTransform barRT = barObj.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(1f, 0f);
        barRT.anchorMax = new Vector2(1f, 1f);
        barRT.pivot = new Vector2(1f, 1f);
        barRT.sizeDelta = new Vector2(Width, 0f);
        barRT.anchoredPosition = new Vector2(-edgeInset, 0f);
        barRT.offsetMax = new Vector2(barRT.offsetMax.x, -topInset);
        barRT.offsetMin = new Vector2(barRT.offsetMin.x, bottomInset);

        Image track = barObj.AddComponent<Image>();
        track.sprite = RoundedSprite.Get(radius);
        track.type = Image.Type.Sliced;
        track.color = TrackColor;

        GameObject slidingArea = new GameObject("SlidingArea");
        slidingArea.transform.SetParent(barObj.transform, false);
        RectTransform slidingRT = slidingArea.AddComponent<RectTransform>();
        slidingRT.anchorMin = Vector2.zero;
        slidingRT.anchorMax = Vector2.one;
        slidingRT.offsetMin = Vector2.zero;
        slidingRT.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(slidingArea.transform, false);
        RectTransform handleRT = handle.AddComponent<RectTransform>();
        handleRT.sizeDelta = Vector2.zero;

        Image handleImg = handle.AddComponent<Image>();
        handleImg.sprite = RoundedSprite.Get(radius);
        handleImg.type = Image.Type.Sliced;
        handleImg.color = HandleColor;

        Scrollbar bar = barObj.AddComponent<Scrollbar>();
        bar.transition = Selectable.Transition.None;
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handleRT;
        bar.targetGraphic = handleImg;

        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        if (content != null
            && Mathf.Approximately(content.anchorMin.x, 0f)
            && Mathf.Approximately(content.anchorMax.x, 1f))
        {
            content.offsetMax = new Vector2(-(Inset + edgeInset), content.offsetMax.y);
        }

        return bar;
    }
}
