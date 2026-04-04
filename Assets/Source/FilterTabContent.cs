using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FilterTabContent : MonoBehaviour
{
    #region Types

    public struct ToggleItem
    {
        public string Label;
        public string Key;
        public bool StartActive;
    }

    #endregion

    #region Private Fields

    private ScrollRect _scrollRect;
    private RectTransform _contentContainer;
    private Dictionary<string, ToggleState> _toggleStates = new Dictionary<string, ToggleState>();
    private Action<string, bool> _onToggleChanged;
    private Action _onBulkBegin;
    private Action _onBulkEnd;
    private float _alpha = 0.92f;
    private Sprite _roundedSprite;

    private class ToggleState
    {
        public bool IsActive;
        public Image RowBackground;
        public TextMeshProUGUI Label;
        public Image ToggleTrack;
        public RectTransform Knob;
        public Image KnobImage;
    }

    #endregion

    #region Colors

    private Color ActiveRowBg => new Color(0f, 0.82f, 0.9f, 0.1f * _alpha);
    private Color ActiveRowBorder => new Color(0f, 0.82f, 0.9f, 0.3f * _alpha);
    private Color ActiveTrack => new Color(0f, 0.82f, 0.9f, 1f);
    private static readonly Color ActiveText = new Color(0.91f, 0.92f, 0.94f, 1f);
    private static readonly Color ActiveKnob = Color.white;

    private Color InactiveRowBg => new Color(1f, 1f, 1f, 0.03f * _alpha);
    private Color InactiveRowBorder => new Color(1f, 1f, 1f, 0.06f * _alpha);
    private Color InactiveTrack => new Color(0.16f, 0.18f, 0.23f, 1f * _alpha);
    private static readonly Color InactiveText = new Color(0.31f, 0.33f, 0.41f, 1f);
    private static readonly Color InactiveKnob = new Color(0.31f, 0.33f, 0.41f, 1f);

    private Color BulkBtnBg => new Color(0f, 0f, 0f, 0.01f * _alpha);
    private Color BulkBtnBorder => new Color(1f, 1f, 1f, 0.12f * _alpha);
    private static readonly Color BulkBtnText = new Color(0.61f, 0.63f, 0.71f, 1f);

    #endregion

    #region Initialization

    public void Initialize(List<ToggleItem> items, Action<string, bool> onToggleChanged, float alpha, Sprite roundedSprite, Action onBulkBegin = null, Action onBulkEnd = null)
    {
        _onToggleChanged = onToggleChanged;
        _onBulkBegin = onBulkBegin;
        _onBulkEnd = onBulkEnd;
        _alpha = alpha;
        _roundedSprite = roundedSprite;
        BuildLayout(items);
    }

    private void BuildLayout(List<ToggleItem> items)
    {
        GameObject scrollObj = CreateScrollView();
        _scrollRect = scrollObj.GetComponent<ScrollRect>();
        _contentContainer = _scrollRect.content;

        CreateBulkButtons();

        foreach (ToggleItem item in items)
            CreateToggleRow(item);
    }

    #endregion

    #region Scroll View

    private GameObject CreateScrollView()
    {
        GameObject scrollObj = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollObj.transform.SetParent(transform, false);

        RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = Vector2.zero;

        Image scrollBg = scrollObj.GetComponent<Image>();
        scrollBg.color = Color.clear;

        ScrollRect scroll = scrollObj.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObj.transform.SetParent(scrollObj.transform, false);

        RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        Image viewportImage = viewportObj.GetComponent<Image>();
        viewportImage.color = Color.white;

        Mask viewportMask = viewportObj.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        scroll.viewport = viewportRect;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObj.transform.SetParent(viewportObj.transform, false);

        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(12f, 0f);
        contentRect.offsetMax = new Vector2(-12f, 0f);

        VerticalLayoutGroup layout = contentObj.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(0, 0, 8, 12);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        ContentSizeFitter fitter = contentObj.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;

        return scrollObj;
    }

    #endregion

    #region Bulk Buttons

    private void CreateBulkButtons()
    {
        GameObject row = new GameObject("BulkButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(_contentContainer, false);

        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.minHeight = 36f;
        rowLayout.preferredHeight = 36f;
        rowLayout.flexibleHeight = 0f;

        HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.padding = new RectOffset(0, 0, 2, 2);
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        CreateBulkButton(row.transform, "Select all", OnSelectAll);
        CreateBulkButton(row.transform, "Deselect all", OnDeselectAll);
    }

    private void CreateBulkButton(Transform parent, string label, Action onClick)
    {
        GameObject btnObj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnObj.transform.SetParent(parent, false);

        LayoutElement le = btnObj.GetComponent<LayoutElement>();
        le.preferredWidth = 110f;

        Image bg = btnObj.GetComponent<Image>();
        bg.sprite = _roundedSprite;
        bg.type = Image.Type.Sliced;
        bg.color = BulkBtnBorder;

        GameObject innerObj = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        innerObj.transform.SetParent(btnObj.transform, false);

        RectTransform innerRect = innerObj.GetComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(1f, 1f);
        innerRect.offsetMax = new Vector2(-1f, -1f);

        Image innerBg = innerObj.GetComponent<Image>();
        innerBg.sprite = _roundedSprite;
        innerBg.type = Image.Type.Sliced;
        innerBg.color = BulkBtnBg;

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 12f;
        text.color = BulkBtnText;
        text.alignment = TextAlignmentOptions.Center;

        Button btn = btnObj.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => onClick());
    }

    #endregion

    #region Toggle Row

    private void CreateToggleRow(ToggleItem item)
    {
        GameObject rowObj = new GameObject($"Toggle_{item.Key}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        rowObj.transform.SetParent(_contentContainer, false);

        LayoutElement le = rowObj.GetComponent<LayoutElement>();
        le.minHeight = 44f;
        le.preferredHeight = 44f;
        le.flexibleHeight = 0f;

        Image rowBg = rowObj.GetComponent<Image>();
        rowBg.sprite = _roundedSprite;
        rowBg.type = Image.Type.Sliced;

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(rowObj.transform, false);

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(16f, 0f);
        labelRect.offsetMax = new Vector2(-60f, 0f);

        TextMeshProUGUI labelText = labelObj.GetComponent<TextMeshProUGUI>();
        labelText.text = item.Label;
        labelText.fontSize = 14f;
        labelText.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject trackObj = new GameObject("Track", typeof(RectTransform), typeof(Image));
        trackObj.transform.SetParent(rowObj.transform, false);

        RectTransform trackRect = trackObj.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(1f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.pivot = new Vector2(1f, 0.5f);
        trackRect.anchoredPosition = new Vector2(-14f, 0f);
        trackRect.sizeDelta = new Vector2(40f, 22f);

        Image trackImage = trackObj.GetComponent<Image>();
        trackImage.sprite = _roundedSprite;
        trackImage.type = Image.Type.Sliced;

        GameObject knobObj = new GameObject("Knob", typeof(RectTransform), typeof(Image));
        knobObj.transform.SetParent(trackObj.transform, false);

        RectTransform knobRect = knobObj.GetComponent<RectTransform>();
        knobRect.sizeDelta = new Vector2(16f, 16f);
        knobRect.anchorMin = new Vector2(0f, 0.5f);
        knobRect.anchorMax = new Vector2(0f, 0.5f);
        knobRect.pivot = new Vector2(0f, 0.5f);

        Image knobImage = knobObj.GetComponent<Image>();
        knobImage.sprite = _roundedSprite;
        knobImage.type = Image.Type.Sliced;

        ToggleState state = new ToggleState
        {
            IsActive = item.StartActive,
            RowBackground = rowBg,
            Label = labelText,
            ToggleTrack = trackImage,
            Knob = knobRect,
            KnobImage = knobImage
        };

        _toggleStates[item.Key] = state;
        ApplyVisualState(state);

        string key = item.Key;
        Button btn = rowObj.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => OnToggleClicked(key));
    }

    #endregion

    #region Toggle Logic

    private void OnToggleClicked(string key)
    {
        if (!_toggleStates.TryGetValue(key, out ToggleState state)) return;

        state.IsActive = !state.IsActive;
        ApplyVisualState(state);
        _onToggleChanged?.Invoke(key, state.IsActive);
    }

    private void ApplyVisualState(ToggleState state)
    {
        if (state.IsActive)
        {
            state.RowBackground.color = ActiveRowBg;
            state.Label.color = ActiveText;
            state.ToggleTrack.color = ActiveTrack;
            state.KnobImage.color = ActiveKnob;
            state.Knob.anchoredPosition = new Vector2(21f, 0f);
        }
        else
        {
            state.RowBackground.color = InactiveRowBg;
            state.Label.color = InactiveText;
            state.ToggleTrack.color = InactiveTrack;
            state.KnobImage.color = InactiveKnob;
            state.Knob.anchoredPosition = new Vector2(3f, 0f);
        }
    }

    #endregion

    #region Bulk Actions

    private void OnSelectAll()
    {
        _onBulkBegin?.Invoke();

        foreach (var kvp in _toggleStates)
        {
            if (!kvp.Value.IsActive)
            {
                kvp.Value.IsActive = true;
                ApplyVisualState(kvp.Value);
                _onToggleChanged?.Invoke(kvp.Key, true);
            }
        }

        _onBulkEnd?.Invoke();
    }

    private void OnDeselectAll()
    {
        _onBulkBegin?.Invoke();

        foreach (var kvp in _toggleStates)
        {
            if (kvp.Value.IsActive)
            {
                kvp.Value.IsActive = false;
                ApplyVisualState(kvp.Value);
                _onToggleChanged?.Invoke(kvp.Key, false);
            }
        }

        _onBulkEnd?.Invoke();
    }

    #endregion

    #region External State Update

    public void SetToggleState(string key, bool active)
    {
        if (!_toggleStates.TryGetValue(key, out ToggleState state)) return;
        if (state.IsActive == active) return;

        state.IsActive = active;
        ApplyVisualState(state);
    }

    public void UpdateAlpha(float alpha)
    {
        _alpha = alpha;
        foreach (var kvp in _toggleStates)
            ApplyVisualState(kvp.Value);
    }

    #endregion
}