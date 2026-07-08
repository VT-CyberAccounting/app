using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FilterContent
{
    public enum Axis { Columns, Rows }

    private static readonly Color ActiveRowBg = MetaTokens.Alpha(MetaTokens.White, 0.06f);
    private static readonly Color InactiveRowBg = MetaTokens.Alpha(MetaTokens.White, 0.03f);
    private static readonly Color ActiveTrack = MetaTokens.Blue;
    private static readonly Color InactiveTrack = MetaTokens.Neutral5A;
    private static readonly Color ActiveTextColor = MetaTokens.TextPrimary;
    private static readonly Color InactiveText = MetaTokens.NeutralC0;
    private static readonly Color ActiveKnob = MetaTokens.White;
    private static readonly Color InactiveKnob = MetaTokens.NeutralD9;

    private static readonly Color SegActiveBg = MetaTokens.Alpha(MetaTokens.Blue, 0.30f);
    private static readonly Color SegActiveText = MetaTokens.TextPrimary;
    private static readonly Color SegInactiveText = MetaTokens.NeutralC0;
    private static readonly Color PressBg = MetaTokens.Alpha(MetaTokens.Blue, 0.30f);

    private readonly SheetController _sheetController;
    private readonly TMP_FontAsset _bodyFont;

    private DataSource _source;
    private Axis _axis = Axis.Columns;
    private bool _open;
    private bool _locked;

    private SegmentVisual _columnsSeg;
    private SegmentVisual _rowsSeg;
    private SegmentVisual _sortAsc;
    private SegmentVisual _sortDesc;
    private const float ActionHeight = 24f;
    private PointerHighlight _selectAllHl;
    private PointerHighlight _deselectAllHl;
    private Transform _listContent;
    private RectTransform _root;
    private LayoutElement _viewportLE;
    private GameObject _actionsRoot;
    private GameObject _viewportRoot;
    private GameObject _sectionPrompt;
    private readonly List<ToggleState> _toggles = new List<ToggleState>();

    private HintTrigger _selectAllHint;
    private HintTrigger _deselectAllHint;
    private HintTrigger _sortAscHint;
    private HintTrigger _sortDescHint;

    private bool _cardsHintLatched;

    private class SegmentVisual
    {
        public Image Inner;
        public TextMeshProUGUI Text;
        public PointerHighlight Highlight;
        public Color InactiveBg;
    }

    private class ToggleState
    {
        public int Index;
        public bool IsActive;
        public RectTransform Root;
        public Image RowBackground;
        public TextMeshProUGUI Label;
        public Image TrackImage;
        public RectTransform Knob;
        public Image KnobImage;
        public PointerHighlight Highlight;
    }

    public FilterContent(RectTransform parent, SheetController sheetController, TMP_FontAsset bodyFont)
    {
        _sheetController = sheetController;
        _bodyFont = bodyFont;
        Build(parent);

        if (_sheetController != null)
        {
            _sheetController.OnColumnToggled += OnColumnToggledExternal;
            _sheetController.OnRowToggled += OnRowToggledExternal;
        }
    }

    public void Dispose()
    {
        Unsubscribe();
        if (_sheetController != null)
        {
            _sheetController.OnColumnToggled -= OnColumnToggledExternal;
            _sheetController.OnRowToggled -= OnRowToggledExternal;
        }
    }

    public void Bind(DataSource source)
    {
        Unsubscribe();
        _source = source;
        _open = false;
        ApplySegmentVisuals();
        ApplySections();
        if (_source != null)
        {
            _source.OnDataLoaded += OnDataLoaded;
            _source.OnOrderChanged += OnOrderChanged;
        }
        if (_source != null && _source.IsLoaded) OnDataLoaded();
        else Rebuild();
    }

    private void Unsubscribe()
    {
        if (_source == null) return;
        _source.OnDataLoaded -= OnDataLoaded;
        _source.OnOrderChanged -= OnOrderChanged;
    }

    private void OnDataLoaded()
    {
        _cardsHintLatched = false;
        Rebuild();
    }

    private void OnOrderChanged()
    {
        ApplyOrder();
        RefreshSortVisual();
    }

    public void ShowAxis(Axis axis)
    {
        _axis = axis;
        _open = true;
        ApplySegmentVisuals();
        ApplySections();
        Rebuild();
    }

    public void CloseSections()
    {
        if (!_open) return;
        _open = false;
        ApplySegmentVisuals();
        ApplySections();
    }

    public int ActiveAxisIndex => !_open ? -1 : (_axis == Axis.Rows ? 1 : 0);

    public void SetLocked(bool locked)
    {
        _locked = locked;
        for (int i = 0; i < _toggles.Count; i++)
        {
            if (_toggles[i].Highlight != null) _toggles[i].Highlight.SetSuppressed(locked);
            ApplyToggleVisuals(_toggles[i]);
        }
        if (_selectAllHl != null) _selectAllHl.SetSuppressed(locked);
        if (_deselectAllHl != null) _deselectAllHl.SetSuppressed(locked);
        if (_sortAsc != null && _sortAsc.Highlight != null) _sortAsc.Highlight.SetSuppressed(locked);
        if (_sortDesc != null && _sortDesc.Highlight != null) _sortDesc.Highlight.SetSuppressed(locked);
    }

    private void SetAxis(Axis axis)
    {
        if (_open && _axis == axis) { CloseSections(); return; }
        ShowAxis(axis);
    }

    private void ApplySections()
    {
        if (_actionsRoot != null) _actionsRoot.SetActive(_open);
        if (_viewportRoot != null) _viewportRoot.SetActive(_open);
        if (_sectionPrompt != null) _sectionPrompt.SetActive(!_open);
    }

    private bool IsColumns => _axis == Axis.Columns;

    private IReadOnlyList<string> Titles =>
        _source == null ? System.Array.Empty<string>() : (IsColumns ? _source.ColumnTitles : _source.RowTitles);

    private bool IsVisible(int index) =>
        IsColumns ? _sheetController.IsColumnVisible(index) : _sheetController.IsRowVisible(index);

    private IReadOnlyList<int> Order =>
        _source == null ? null : (IsColumns ? _source.ColumnOrder : _source.RowOrder);

    private DataSource.SortMode CurrentSort =>
        _source == null ? DataSource.SortMode.Original
                        : (IsColumns ? _source.ColumnSortMode : _source.RowSortMode);

    private void Rebuild()
    {
        PopulateToggles();
        UpdateViewportHeight();
        ApplyOrder();
        RefreshSortVisual();
        RefreshActionHints();
        SetLocked(_locked);
    }

    // ---- construction --------------------------------------------------

    private void Build(RectTransform parent)
    {
        _root = parent;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.Destroy(parent.GetChild(i).gameObject);

        VerticalLayoutGroup vlg = parent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = MetaTokens.Spacing;
        vlg.padding = new RectOffset((int)MetaTokens.Spacing, (int)MetaTokens.Spacing,
            (int)MetaTokens.Spacing, (int)MetaTokens.Spacing);

        BuildSegmentRow(parent);
        BuildActionGrid(parent);
        BuildList(parent);
        BuildSectionPrompt(parent);
        ApplySections();
    }

    private void BuildSectionPrompt(RectTransform parent)
    {
        GameObject obj = new GameObject("SectionPrompt", typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f;

        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.text = HintText.NoSectionSelected;
        text.fontSize = MetaTokens.Body1;
        text.color = MetaTokens.Neutral8E;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        if (_bodyFont != null) text.font = _bodyFont;

        _sectionPrompt = obj;
    }

    private void BuildSegmentRow(RectTransform parent)
    {
        RectTransform row = Row(parent, "AxisToggle", MetaTokens.ButtonHeight);
        _columnsSeg = BuildSegment(row, "Columns", Axis.Columns);
        _rowsSeg = BuildSegment(row, "Rows", Axis.Rows);
        ApplySegmentVisuals();
    }

    private SegmentVisual BuildSegment(RectTransform row, string label, Axis axis)
    {
        UIButton.Handle h = UIButton.Create(row, $"Seg_{label}", label, flexibleWidth: true);
        if (_bodyFont != null && h.Text != null) h.Text.font = _bodyFont;
        h.Button.onClick.AddListener(() => SetAxis(axis));
        HintTrigger.Attach(h.Root, label, $"This button opens the {(axis == Axis.Columns ? "column" : "row")} section.");
        return new SegmentVisual { Inner = h.Inner, Text = h.Text, Highlight = h.Highlight, InactiveBg = UIButton.InnerIdle };
    }

    private void BuildActionGrid(RectTransform parent)
    {
        RectTransform grid = ActionGrid(parent, "Actions");
        _actionsRoot = grid.gameObject;

        RectTransform bulk = ActionSubColumn(grid, "Bulk");
        _selectAllHl = BuildActionButton(bulk, "Select All", out _selectAllHint, () => SetAll(true));
        _deselectAllHl = BuildActionButton(bulk, "Deselect All", out _deselectAllHint, () => SetAll(false));

        RectTransform sort = ActionSubColumn(grid, "Sort");
        _sortAsc = BuildSortButton(sort, "Sort Ascending", DataSource.SortMode.Ascending, out _sortAscHint);
        _sortDesc = BuildSortButton(sort, "Sort Descending", DataSource.SortMode.Descending, out _sortDescHint);

        RefreshActionHints();
    }

    private static RectTransform ActionGrid(RectTransform parent, string name)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        HorizontalLayoutGroup hlg = obj.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = false;
        hlg.spacing = MetaTokens.Spacing;
        hlg.padding = new RectOffset((int)MetaTokens.Spacing, (int)MetaTokens.Spacing, 0, 0);

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.flexibleHeight = 0f;
        return rt;
    }

    private static RectTransform ActionSubColumn(RectTransform parent, string name)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        VerticalLayoutGroup vlg = obj.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = MetaTokens.Spacing;

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        return rt;
    }

    private PointerHighlight BuildActionButton(RectTransform col, string label, out HintTrigger hint, System.Action onClick)
    {
        UIButton.Handle h = UIButton.Create(col, label.Replace(" ", ""), label, height: ActionHeight, fontSize: MetaTokens.Body1);
        if (_bodyFont != null && h.Text != null) h.Text.font = _bodyFont;
        h.Button.onClick.AddListener(() => { if (!_locked) onClick(); });
        hint = HintTrigger.Attach(h.Root, label, "").SetLockOverride(() => _locked, "Locked", HintText.WindowLocked);
        return h.Highlight;
    }

    private void RefreshActionHints()
    {
        string noun = IsColumns ? "columns" : "rows";
        string single = IsColumns ? "column" : "row";
        if (_selectAllHint != null)
            _selectAllHint.body = $"This button unfilters all {noun}.";
        if (_deselectAllHint != null)
            _deselectAllHint.body = $"This button filters all {noun}.";

        SortExtremes(out string smallest, out string largest);
        string small = string.IsNullOrEmpty(smallest) ? $"smallest {single}" : smallest;
        string large = string.IsNullOrEmpty(largest) ? $"largest {single}" : largest;
        if (_sortAscHint != null)
            _sortAscHint.body = $"This button sorts {noun} from {small} to {large}.";
        if (_sortDescHint != null)
            _sortDescHint.body = $"This button sorts {noun} from {large} to {small}.";
    }

    private void SortExtremes(out string smallest, out string largest)
    {
        smallest = null;
        largest = null;
        IReadOnlyList<string> titles = Titles;
        if (titles.Count == 0) return;

        bool numeric = _source != null && (IsColumns ? _source.ColumnTitlesAreNumeric : _source.RowTitlesAreNumeric);

        int hi = 0, lo = 0;
        for (int i = 1; i < titles.Count; i++)
        {
            if (IsLarger(titles[i], titles[hi], numeric)) hi = i;
            if (IsLarger(titles[lo], titles[i], numeric)) lo = i;
        }
        largest = titles[hi];
        smallest = titles[lo];
    }

    private static bool IsLarger(string a, string b, bool numeric)
    {
        if (numeric)
            return ParseNumber(a) > ParseNumber(b);
        return string.Compare(a, b, System.StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static double ParseNumber(string value)
    {
        return double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double result) ? result : 0d;
    }

    private SegmentVisual BuildSortButton(RectTransform col, string label, DataSource.SortMode mode, out HintTrigger hint)
    {
        UIButton.Handle h = UIButton.Create(col, label.Replace(" ", ""), label, height: ActionHeight, fontSize: MetaTokens.Body1);
        if (_bodyFont != null && h.Text != null) h.Text.font = _bodyFont;
        h.Button.onClick.AddListener(() => { if (!_locked) Sort(mode); });
        hint = HintTrigger.Attach(h.Root, label, "").SetLockOverride(() => _locked, "Locked", HintText.WindowLocked);
        return new SegmentVisual { Inner = h.Inner, Text = h.Text, Highlight = h.Highlight, InactiveBg = UIButton.InnerIdle };
    }

    private void BuildList(RectTransform parent)
    {
        GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        _viewportRoot = viewportObj;
        RectTransform viewport = viewportObj.GetComponent<RectTransform>();
        viewport.SetParent(parent, false);
        viewportObj.GetComponent<Image>().color = MetaTokens.White;
        viewportObj.GetComponent<Mask>().showMaskGraphic = false;

        _viewportLE = viewportObj.AddComponent<LayoutElement>();
        _viewportLE.flexibleHeight = 0f;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        RectTransform content = contentObj.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0f, content.offsetMin.y);
        content.offsetMax = new Vector2(0f, content.offsetMax.y);

        VerticalLayoutGroup contentVlg = contentObj.AddComponent<VerticalLayoutGroup>();
        contentVlg.childControlWidth = true;
        contentVlg.childControlHeight = true;
        contentVlg.childForceExpandWidth = true;
        contentVlg.childForceExpandHeight = false;
        contentVlg.spacing = MetaTokens.Spacing;
        contentVlg.padding = new RectOffset(
            (int)MetaTokens.Spacing, 0,
            0, 0);

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = viewportObj.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        UiScrollbar.Attach(scroll, edgeInset: MetaTokens.Spacing, topInset: 0f, bottomInset: 0f);

        _listContent = content;
    }

    private static RectTransform Row(RectTransform parent, string name, float height)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        HorizontalLayoutGroup hlg = obj.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.spacing = MetaTokens.Spacing;
        hlg.padding = new RectOffset((int)MetaTokens.Spacing, (int)MetaTokens.Spacing, 0, 0);

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        le.flexibleHeight = 0f;
        return rt;
    }

    // ---- toggles -------------------------------------------------------

    private void PopulateToggles()
    {
        if (_listContent == null) return;

        for (int i = _listContent.childCount - 1; i >= 0; i--)
            Object.Destroy(_listContent.GetChild(i).gameObject);
        _toggles.Clear();

        IReadOnlyList<string> titles = Titles;
        for (int i = 0; i < titles.Count; i++)
            _toggles.Add(CreateToggleRow(titles[i], i, IsVisible(i)));
    }

    private void UpdateViewportHeight()
    {
        if (_viewportLE == null) return;

        int rows = _toggles.Count;
        float content = rows <= 0
            ? 0f
            : rows * MetaTokens.RowHeight + (rows - 1) * MetaTokens.Spacing;

        float available = _root == null
            ? content
            : _root.rect.height
              - MetaTokens.Spacing
              - MetaTokens.Spacing
              - MetaTokens.ButtonHeight
              - (2f * ActionHeight + MetaTokens.Spacing)
              - 2f * MetaTokens.Spacing;

        if (available <= 1f) available = content;
        _viewportLE.preferredHeight = Mathf.Min(content, available);
    }

    private ToggleState CreateToggleRow(string label, int index, bool startActive)
    {
        Sprite rounded = RoundedSprite.Get(12);

        GameObject rowObj = new GameObject($"Toggle_{index}", typeof(RectTransform));
        rowObj.transform.SetParent(_listContent, false);
        RectTransform rowRect = rowObj.GetComponent<RectTransform>();

        LayoutElement le = rowObj.AddComponent<LayoutElement>();
        le.minHeight = MetaTokens.RowHeight;
        le.preferredHeight = MetaTokens.RowHeight;
        le.flexibleHeight = 0f;

        Image rowBg = rowObj.AddComponent<Image>();
        rowBg.sprite = rounded;
        rowBg.type = Image.Type.Sliced;
        rowBg.color = startActive ? ActiveRowBg : InactiveRowBg;

        Button btn = rowObj.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;

        PointerHighlight highlight = AttachHighlight(rowObj, rowBg, startActive ? ActiveRowBg : InactiveRowBg);

        GameObject labelObj = new GameObject("Label", typeof(RectTransform));
        labelObj.transform.SetParent(rowObj.transform, false);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(16f, 0f);
        labelRect.offsetMax = new Vector2(-60f, 0f);

        TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = MetaTokens.Body2;
        labelText.color = startActive ? ActiveTextColor : InactiveText;
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        labelText.enableWordWrapping = false;
        if (_bodyFont != null) labelText.font = _bodyFont;

        GameObject trackObj = new GameObject("Track", typeof(RectTransform));
        trackObj.transform.SetParent(rowObj.transform, false);
        RectTransform trackRect = trackObj.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(1f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.pivot = new Vector2(1f, 0.5f);
        trackRect.anchoredPosition = new Vector2(-14f, 0f);
        trackRect.sizeDelta = new Vector2(40f, 22f);

        Image trackImg = trackObj.AddComponent<Image>();
        trackImg.sprite = RoundedSprite.Get(11);
        trackImg.type = Image.Type.Sliced;
        trackImg.color = startActive ? ActiveTrack : InactiveTrack;

        GameObject knobObj = new GameObject("Knob", typeof(RectTransform));
        knobObj.transform.SetParent(trackObj.transform, false);
        RectTransform knobRect = knobObj.GetComponent<RectTransform>();
        knobRect.sizeDelta = new Vector2(16f, 16f);
        knobRect.anchorMin = new Vector2(0f, 0.5f);
        knobRect.anchorMax = new Vector2(0f, 0.5f);
        knobRect.pivot = new Vector2(0f, 0.5f);
        knobRect.anchoredPosition = startActive ? new Vector2(21f, 0f) : new Vector2(3f, 0f);

        Image knobImg = knobObj.AddComponent<Image>();
        knobImg.sprite = RoundedSprite.Get(8);
        knobImg.type = Image.Type.Sliced;
        knobImg.color = startActive ? ActiveKnob : InactiveKnob;

        Shadow knobShadow = knobObj.AddComponent<Shadow>();
        knobShadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
        knobShadow.effectDistance = new Vector2(0f, -1.5f);

        ToggleState state = new ToggleState
        {
            Index = index,
            IsActive = startActive,
            Root = rowRect,
            RowBackground = rowBg,
            Label = labelText,
            TrackImage = trackImg,
            Knob = knobRect,
            KnobImage = knobImg,
            Highlight = highlight
        };

        HintTrigger.AttachShared(rowObj,
            IsColumns ? "Toggle Column" : "Toggle Row",
            $"This button filters {label}.",
            () => _cardsHintLatched).SetLockOverride(() => _locked, "Locked", HintText.WindowLocked);

        btn.onClick.AddListener(() =>
        {
            if (_locked || _sheetController == null) return;
            bool newState = !state.IsActive;
            if (!newState) _cardsHintLatched = true;
            if (IsColumns) _sheetController.SetColumnVisible(index, newState);
            else _sheetController.SetRowVisible(index, newState);
        });

        return state;
    }

    private void OnColumnToggledExternal(int index, bool visible)
    {
        if (IsColumns) ApplyExternal(index, visible);
    }

    private void OnRowToggledExternal(int index, bool visible)
    {
        if (!IsColumns) ApplyExternal(index, visible);
    }

    private void ApplyExternal(int index, bool visible)
    {
        if (index < 0 || index >= _toggles.Count) return;
        ToggleState state = _toggles[index];
        state.IsActive = visible;
        ApplyToggleVisuals(state);
    }

    private void ApplyToggleVisuals(ToggleState state)
    {
        if (state.IsActive)
        {
            if (state.Highlight != null) state.Highlight.SetRest(ActiveRowBg);
            else if (state.RowBackground != null) state.RowBackground.color = ActiveRowBg;
            if (state.Label != null) state.Label.color = ActiveTextColor;
            if (state.TrackImage != null) state.TrackImage.color = _locked ? MetaTokens.Neutral74 : ActiveTrack;
            if (state.KnobImage != null) state.KnobImage.color = ActiveKnob;
            if (state.Knob != null) state.Knob.anchoredPosition = new Vector2(21f, 0f);
        }
        else
        {
            if (state.Highlight != null) state.Highlight.SetRest(InactiveRowBg);
            else if (state.RowBackground != null) state.RowBackground.color = InactiveRowBg;
            if (state.Label != null) state.Label.color = InactiveText;
            if (state.TrackImage != null) state.TrackImage.color = InactiveTrack;
            if (state.KnobImage != null) state.KnobImage.color = InactiveKnob;
            if (state.Knob != null) state.Knob.anchoredPosition = new Vector2(3f, 0f);
        }
    }

    private void ApplyOrder()
    {
        IReadOnlyList<int> order = Order;
        if (order == null) return;
        for (int pos = 0; pos < order.Count; pos++)
        {
            int idx = order[pos];
            if (idx < 0 || idx >= _toggles.Count) continue;
            RectTransform root = _toggles[idx].Root;
            if (root != null && root.GetSiblingIndex() != pos) root.SetSiblingIndex(pos);
        }
    }

    // ---- actions -------------------------------------------------------

    private void SetAll(bool visible)
    {
        if (_locked || _sheetController == null) return;
        if (IsColumns) _sheetController.SetAllColumnsVisible(visible);
        else _sheetController.SetAllRowsVisible(visible);
    }

    private void Sort(DataSource.SortMode mode)
    {
        if (_locked || _sheetController == null || _source == null) return;
        DataSource.SortMode next = CurrentSort == mode ? DataSource.SortMode.Original : mode;
        if (IsColumns) _sheetController.SortColumns(next);
        else _sheetController.SortRows(next);
    }

    // ---- visuals -------------------------------------------------------

    private void ApplySegmentVisuals()
    {
        ApplySegment(_columnsSeg, _open && _axis == Axis.Columns);
        ApplySegment(_rowsSeg, _open && _axis == Axis.Rows);
    }

    private void ApplySegment(SegmentVisual seg, bool active)
    {
        if (seg == null) return;
        Color rest = active ? SegActiveBg : seg.InactiveBg;
        if (seg.Highlight != null) seg.Highlight.SetRest(rest);
        else if (seg.Inner != null) seg.Inner.color = rest;
        if (seg.Text != null) seg.Text.color = active ? SegActiveText : SegInactiveText;
    }

    private void RefreshSortVisual()
    {
        DataSource.SortMode mode = CurrentSort;
        ApplySegment(_sortAsc, mode == DataSource.SortMode.Ascending);
        ApplySegment(_sortDesc, mode == DataSource.SortMode.Descending);
    }

    private static PointerHighlight AttachHighlight(GameObject go, Graphic target, Color rest)
    {
        PointerHighlight hl = go.GetComponent<PointerHighlight>();
        if (hl == null) hl = go.AddComponent<PointerHighlight>();
        hl.target = target;
        hl.SetColors(rest, PressBg);
        return hl;
    }
}
