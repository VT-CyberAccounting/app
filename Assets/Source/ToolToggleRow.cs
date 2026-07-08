using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class ToolToggleRow
{
    private static readonly Color ActiveBg = MetaTokens.Alpha(MetaTokens.Blue, 0.18f);
    private static readonly Color ActiveText = MetaTokens.BlueLight;
    private static readonly Color InactiveBg = MetaTokens.Alpha(MetaTokens.White, 0.05f);
    private static readonly Color InactiveText = MetaTokens.NeutralC0;
    private static readonly Color PressBg = MetaTokens.Alpha(MetaTokens.Blue, 0.30f);

    private class Toggle
    {
        public Image Bg;
        public TextMeshProUGUI Text;
        public PointerHighlight Highlight;
    }

    private readonly Transform _row;
    private readonly TMP_FontAsset _font;
    private readonly List<Toggle> _toggles = new List<Toggle>();

    public ToolToggleRow(GameObject content, string rowName, TMP_FontAsset bodyFont)
    {
        _font = bodyFont;

        GameObject row = new GameObject(rowName);
        row.transform.SetParent(content.transform, false);
        row.AddComponent<RectTransform>();

        LayoutElement le = row.AddComponent<LayoutElement>();
        le.minHeight = MetaTokens.ButtonHeight;
        le.preferredHeight = MetaTokens.ButtonHeight;
        le.flexibleHeight = 0f;

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = MetaTokens.Spacing;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        Transform description = content.transform.Find("Description");
        int insertAt = description != null ? description.GetSiblingIndex() + 1 : 0;
        row.transform.SetSiblingIndex(insertAt);

        _row = row.transform;
    }

    public void AddButton(string name, string label, string hint, UnityAction onClick)
    {
        GameObject o = new GameObject(name);
        o.transform.SetParent(_row, false);
        o.AddComponent<RectTransform>();

        Image bg = o.AddComponent<Image>();
        bg.sprite = RoundedSprite.Get((int)MetaTokens.RadiusButton);
        bg.type = Image.Type.Sliced;
        bg.color = InactiveBg;

        Button btn = o.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = bg;
        btn.onClick.AddListener(onClick);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(o.transform, false);
        RectTransform trt = textObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(6f, 0f);
        trt.offsetMax = new Vector2(-6f, 0f);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = MetaTokens.Body1;
        text.color = InactiveText;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        if (_font != null) text.font = _font;

        PointerHighlight hl = o.AddComponent<PointerHighlight>();
        hl.target = bg;
        hl.SetColors(InactiveBg, PressBg);

        HintTrigger.Attach(o, label, hint);

        _toggles.Add(new Toggle { Bg = bg, Text = text, Highlight = hl });
    }

    public void SetActive(int activeIndex)
    {
        for (int i = 0; i < _toggles.Count; i++)
            Apply(_toggles[i], i == activeIndex);
    }

    private static void Apply(Toggle t, bool active)
    {
        if (t == null) return;
        Color bg = active ? ActiveBg : InactiveBg;
        if (t.Highlight != null) t.Highlight.SetRest(bg);
        else if (t.Bg != null) t.Bg.color = bg;
        if (t.Text != null) t.Text.color = active ? ActiveText : InactiveText;
    }
}
