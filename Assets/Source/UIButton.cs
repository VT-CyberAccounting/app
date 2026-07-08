using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class UIButton
{
    public static readonly Color BaseBg = MetaTokens.SheetAlt;
    public static readonly Color InnerIdle = MetaTokens.Alpha(MetaTokens.White, 0.05f);
    public static readonly Color Selected = MetaTokens.Alpha(MetaTokens.Blue, 0.30f);
    public static readonly Color TextIdle = MetaTokens.NeutralC0;
    public static readonly Color TextActive = MetaTokens.TextPrimary;

    public class Handle
    {
        public GameObject Root;
        public Button Button;
        public Image BaseImage;
        public Image Inner;
        public TextMeshProUGUI Text;
        public PointerHighlight Highlight;
    }

    public static Handle Create(Transform parent, string name, string label,
        float width = 0f, bool flexibleWidth = false, float height = 0f,
        float fontSize = 0f, TextAlignmentOptions alignment = TextAlignmentOptions.Center,
        float padLeft = 10f, float padRight = 10f)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.AddComponent<RectTransform>();

        LayoutElement le = root.AddComponent<LayoutElement>();
        le.minHeight = height > 0f ? height : MetaTokens.ButtonHeight;
        le.preferredHeight = le.minHeight;
        if (width > 0f) le.preferredWidth = width;
        le.flexibleWidth = flexibleWidth ? 1f : 0f;

        Handle h = new Handle { Root = root };
        Build(h, label, fontSize, alignment, padLeft, padRight);
        return h;
    }

    public static Handle Style(GameObject root, float fontSize = 0f,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center,
        float padLeft = 10f, float padRight = 10f)
    {
        Handle h = new Handle { Root = root };
        Build(h, null, fontSize, alignment, padLeft, padRight);
        return h;
    }

    public static void SetSelected(Handle h, bool selected)
    {
        if (h == null) return;
        if (h.Highlight != null) h.Highlight.SetRest(selected ? Selected : InnerIdle);
        else if (h.Inner != null) h.Inner.color = selected ? Selected : InnerIdle;
        if (h.Text != null) h.Text.color = selected ? TextActive : TextIdle;
    }

    private static void Build(Handle h, string label, float fontSize,
        TextAlignmentOptions alignment, float padLeft, float padRight)
    {
        GameObject root = h.Root;
        Sprite rounded = RoundedSprite.Get((int)MetaTokens.RadiusButton);

        Image baseImg = Ensure<Image>(root);
        baseImg.sprite = rounded;
        baseImg.type = Image.Type.Sliced;
        baseImg.color = BaseBg;
        h.BaseImage = baseImg;

        Button button = Ensure<Button>(root);
        button.transition = Selectable.Transition.None;
        h.Button = button;

        RectTransform innerRt = EnsureChild(root.transform, "Inner");
        innerRt.anchorMin = Vector2.zero;
        innerRt.anchorMax = Vector2.one;
        innerRt.offsetMin = Vector2.zero;
        innerRt.offsetMax = Vector2.zero;
        Image inner = Ensure<Image>(innerRt.gameObject);
        inner.sprite = rounded;
        inner.type = Image.Type.Sliced;
        inner.color = InnerIdle;
        inner.raycastTarget = false;
        h.Inner = inner;

        RectTransform textRt = EnsureChild(root.transform, "Text");
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(padLeft, 0f);
        textRt.offsetMax = new Vector2(-padRight, 0f);
        TextMeshProUGUI text = Ensure<TextMeshProUGUI>(textRt.gameObject);
        if (label != null) text.text = label;
        text.fontSize = fontSize > 0f ? fontSize : MetaTokens.Body2;
        text.alignment = alignment;
        text.color = TextIdle;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        h.Text = text;

        PointerHighlight hl = Ensure<PointerHighlight>(root);
        hl.target = inner;
        hl.SetColors(InnerIdle, Selected);
        hl.SetLiftTarget(root.transform);
        hl.SetTextColors(text, TextIdle, TextActive);
        h.Highlight = hl;
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static RectTransform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing as RectTransform ?? existing.gameObject.AddComponent<RectTransform>();
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.AddComponent<RectTransform>();
    }
}
