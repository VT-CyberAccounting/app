using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CompareLedgerUI
{
    private const int MaxEntries = 5;
    private const int VisibleRows = 5;
    private const float EntryHeight = 48f;
    private const float EntrySpacing = MetaTokens.Spacing;
    private const float SwatchSize = 16f;
    private const float HeaderHeight = 20f;
    private const float SheetStatColWidth = 80f;
    private const int ListPadLeft = 12;
    private const int RowPadX = 10;
    private const float ScrollViewportHeight = VisibleRows * EntryHeight + (VisibleRows - 1) * EntrySpacing + 2 * EntrySpacing;

    public event Action OnChanged;

    public event Action<Entry> OnEvicted;

    public bool HasEntries => _cells.Entries.Count > 0 || _sheets.Entries.Count > 0;

    public int CellEntryCount => _cells.Entries.Count;

    public int SheetEntryCount => _sheets.Entries.Count;

    public bool HasUndo => _order.Count > 0;

    private readonly TMP_FontAsset _font;
    private readonly Color[] _palette;

    private static readonly Color RowBg = MetaTokens.Alpha(MetaTokens.White, 0.06f);
    private static readonly Color LabelColor = MetaTokens.TextPrimary;
    private static readonly Color ValueColor = MetaTokens.TextPrimary;

    public class Entry
    {
        public GameObject Row;
        public GameObject Marker;
        public Color Color;
        public bool IsSheet;

        public int RMin, RMax, CMin, CMax;
        public int ORMin, ORMax, OCMin, OCMax;

        public string ColumnTitle;
        public string RowTitle;
        public float Value;
        public int CellCount;
        public float Mean;
        public float Sum;
    }

    private class SectionUI
    {
        public Transform ListRoot;
        public CanvasGroup HeaderGroup;
        public GameObject SectionHeader;
        public GameObject ColumnHeaders;
        public GameObject Viewport;
        public readonly List<Entry> Entries = new List<Entry>();
    }

    private readonly SectionUI _cells = new SectionUI();
    private readonly SectionUI _sheets = new SectionUI();
    private readonly List<Entry> _order = new List<Entry>();

    public IReadOnlyList<Entry> SheetEntries => _sheets.Entries;

    public IReadOnlyList<Entry> CellEntries => _cells.Entries;

    public IReadOnlyList<Entry> UndoOrder => _order;

    public CompareLedgerUI(Transform contentRoot, TMP_FontAsset font, Color[] palette)
    {
        _font = font;
        _palette = palette;
        Build(contentRoot);
    }

    public Color ReserveColor(bool isSheet)
    {
        if (_palette == null || _palette.Length == 0) return Color.white;

        SectionUI s = isSheet ? _sheets : _cells;
        if (s.Entries.Count >= MaxEntries) EvictOldest(s);
        return FirstUnusedColor(s);
    }

    private void EvictOldest(SectionUI s)
    {
        if (s.Entries.Count == 0) return;

        int last = s.Entries.Count - 1;
        Entry oldest = s.Entries[last];
        s.Entries.RemoveAt(last);
        _order.Remove(oldest);
        RemoveEntry(oldest);

        for (int i = 0; i < s.Entries.Count; i++)
            s.Entries[i].Row.transform.SetSiblingIndex(i);

        UpdateHeaderVisibility(s);
        OnChanged?.Invoke();
        OnEvicted?.Invoke(oldest);
    }

    public bool Remove(Entry e)
    {
        if (e == null) return false;
        SectionUI s = e.IsSheet ? _sheets : _cells;
        if (!s.Entries.Remove(e)) return false;
        _order.Remove(e);
        RemoveEntry(e);

        for (int i = 0; i < s.Entries.Count; i++)
            s.Entries[i].Row.transform.SetSiblingIndex(i);

        UpdateHeaderVisibility(s);
        OnChanged?.Invoke();
        return true;
    }

    private Color FirstUnusedColor(SectionUI s)
    {
        for (int i = 0; i < _palette.Length; i++)
        {
            bool used = false;
            for (int j = 0; j < s.Entries.Count; j++)
                if (s.Entries[j].Color == _palette[i]) { used = true; break; }
            if (!used) return _palette[i];
        }
        return _palette[0];
    }

    private void Build(Transform contentRoot)
    {
        _cells.ListRoot = CreateSection(contentRoot, "Cells", new[]
        {
            ("Column", 0f, true),
            ("Row", 0f, true),
            ("Value", 64f, false),
        }, _cells);
        _sheets.ListRoot = CreateSection(contentRoot, "Sheets", new[]
        {
            ("Count", 0f, true),
            ("Mean", SheetStatColWidth, false),
            ("Sum", SheetStatColWidth, false),
        }, _sheets);

        Transform spacer = contentRoot.Find("Spacer");
        if (spacer != null) spacer.SetAsLastSibling();
        Transform resetBtn = contentRoot.Find("Reset_Btn") ?? contentRoot.Find("Reset_Border");
        if (resetBtn != null) resetBtn.SetAsLastSibling();
    }

    public void SetVisibleSections(bool showCells, bool showSheets)
    {
        SetSectionVisible(_cells, showCells);
        SetSectionVisible(_sheets, showSheets);
    }

    private static void SetSectionVisible(SectionUI s, bool visible)
    {
        if (s.SectionHeader != null) s.SectionHeader.SetActive(visible);
        if (s.ColumnHeaders != null) s.ColumnHeaders.SetActive(visible);
        if (s.Viewport != null) s.Viewport.SetActive(visible);
    }

    private Transform CreateSection(Transform parent, string title, (string label, float width, bool flexible)[] columns, SectionUI section)
    {
        GameObject headerRow = NewBareRow(parent, title + "SectionHeader", 24f);
        HorizontalLayoutGroup hlg = headerRow.AddComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        CreateText(headerRow.transform, "Title", title, MetaTokens.Body1, MetaTokens.TextPrimary, TextAlignmentOptions.MidlineLeft, 0f, true);

        section.SectionHeader = headerRow;
        section.HeaderGroup = CreateColumnHeaders(parent, title, columns);
        section.HeaderGroup.alpha = 0f;
        section.ColumnHeaders = section.HeaderGroup.gameObject;

        GameObject viewport = new GameObject(title + "Viewport");
        viewport.transform.SetParent(parent, false);
        viewport.AddComponent<RectTransform>();
        LayoutElement vle = viewport.AddComponent<LayoutElement>();
        vle.minHeight = ScrollViewportHeight;
        vle.preferredHeight = ScrollViewportHeight;
        vle.flexibleHeight = 0f;
        viewport.AddComponent<RectMask2D>();

        ScrollRect scroll = viewport.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = EntryHeight;

        GameObject list = new GameObject(title + "List");
        list.transform.SetParent(viewport.transform, false);
        RectTransform listRT = list.AddComponent<RectTransform>();
        listRT.anchorMin = new Vector2(0f, 1f);
        listRT.anchorMax = new Vector2(1f, 1f);
        listRT.pivot = new Vector2(0.5f, 1f);
        listRT.offsetMin = Vector2.zero;
        listRT.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = list.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = EntrySpacing;
        vlg.padding = new RectOffset(ListPadLeft, ListPadLeft, (int)MetaTokens.Spacing, (int)MetaTokens.Spacing);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter csf = list.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = listRT;

        section.Viewport = viewport;
        return list.transform;
    }

    private CanvasGroup CreateColumnHeaders(Transform parent, string section, (string label, float width, bool flexible)[] columns)
    {
        GameObject row = new GameObject(section + "ColumnHeaders");
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>();
        CanvasGroup group = row.AddComponent<CanvasGroup>();

        LayoutElement le = row.AddComponent<LayoutElement>();
        le.minHeight = HeaderHeight;
        le.preferredHeight = HeaderHeight;
        le.flexibleHeight = 0f;

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = MetaTokens.Spacing;
        hlg.padding = new RectOffset(ListPadLeft + RowPadX, ListPadLeft + RowPadX, 0, 0);
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        GameObject spacer = new GameObject("SwatchSpacer");
        spacer.transform.SetParent(row.transform, false);
        spacer.AddComponent<RectTransform>();
        LayoutElement sle = spacer.AddComponent<LayoutElement>();
        sle.minWidth = SwatchSize;
        sle.preferredWidth = SwatchSize;
        sle.flexibleWidth = 0f;

        for (int i = 0; i < columns.Length; i++)
            CreateText(row.transform, "Header_" + columns[i].label, columns[i].label, MetaTokens.Body2, MetaTokens.TextPrimary,
                TextAlignmentOptions.Center, columns[i].width, columns[i].flexible);

        return group;
    }

    private void UpdateHeaderVisibility(SectionUI s)
    {
        if (s.HeaderGroup != null) s.HeaderGroup.alpha = s.Entries.Count > 0 ? 1f : 0f;
    }

    public Entry AddCellEntry(string columnTitle, string rowTitle, float value, Color color, GameObject marker)
    {
        Entry e = BuildCellRow(_cells.ListRoot, columnTitle, rowTitle, value, color);
        e.Color = color;
        e.Marker = marker;
        e.IsSheet = false;
        e.ColumnTitle = columnTitle;
        e.RowTitle = rowTitle;
        e.Value = value;
        AddToSection(_cells, e);
        return e;
    }

    public Entry AddSheetEntry(int cellCount, float average, float sum, Color color, GameObject marker,
        int rMin, int rMax, int cMin, int cMax, int oRMin, int oRMax, int oCMin, int oCMax)
    {
        Entry e = BuildSheetRow(_sheets.ListRoot, cellCount, average, sum, color);
        e.Color = color;
        e.Marker = marker;
        e.IsSheet = true;
        e.CellCount = cellCount;
        e.Mean = average;
        e.Sum = sum;
        e.RMin = rMin; e.RMax = rMax; e.CMin = cMin; e.CMax = cMax;
        e.ORMin = oRMin; e.ORMax = oRMax; e.OCMin = oCMin; e.OCMax = oCMax;
        AddToSection(_sheets, e);
        return e;
    }

    public void RemoveSheetEntry(Entry e)
    {
        if (!_sheets.Entries.Remove(e)) return;
        _order.Remove(e);
        RemoveEntry(e);

        for (int i = 0; i < _sheets.Entries.Count; i++)
            _sheets.Entries[i].Row.transform.SetSiblingIndex(i);

        UpdateHeaderVisibility(_sheets);
        OnChanged?.Invoke();
    }

    private void AddToSection(SectionUI s, Entry e)
    {
        s.Entries.Insert(0, e);
        _order.Add(e);

        for (int i = 0; i < s.Entries.Count; i++)
            s.Entries[i].Row.transform.SetSiblingIndex(i);

        UpdateHeaderVisibility(s);
        OnChanged?.Invoke();
    }

    private Entry BuildCellRow(Transform listRoot, string columnTitle, string rowTitle, float value, Color color)
    {
        GameObject row = NewEntryRow(listRoot);
        CreateSwatch(row.transform, color);
        CreateText(row.transform, "Column", columnTitle, MetaTokens.Body2, LabelColor, TextAlignmentOptions.Center, 0f, true);
        CreateText(row.transform, "Row", rowTitle, MetaTokens.Body2, LabelColor, TextAlignmentOptions.Center, 0f, true);
        CreateText(row.transform, "Value", Tooltip.FormatCompactValue(value, false), MetaTokens.Body2, ValueColor, TextAlignmentOptions.Center, 64f, false);
        return new Entry { Row = row };
    }

    private Entry BuildSheetRow(Transform listRoot, int cellCount, float average, float sum, Color color)
    {
        GameObject row = NewEntryRow(listRoot);
        CreateSwatch(row.transform, color);
        CreateText(row.transform, "Count", cellCount.ToString(), MetaTokens.Body2, LabelColor, TextAlignmentOptions.Center, 0f, true);
        CreateText(row.transform, "Average", Tooltip.FormatCompactValue(average, false), MetaTokens.Body2, ValueColor, TextAlignmentOptions.Center, SheetStatColWidth, false);
        CreateText(row.transform, "Sum", Tooltip.FormatCompactValue(sum, false), MetaTokens.Body2, ValueColor, TextAlignmentOptions.Center, SheetStatColWidth, false);
        return new Entry { Row = row };
    }

    private void CreateSwatch(Transform parent, Color color)
    {
        GameObject root = new GameObject("Swatch");
        root.transform.SetParent(parent, false);
        root.AddComponent<RectTransform>();

        LayoutElement le = root.AddComponent<LayoutElement>();
        le.minWidth = SwatchSize;
        le.preferredWidth = SwatchSize;
        le.flexibleWidth = 0f;

        GameObject dot = new GameObject("Dot");
        dot.transform.SetParent(root.transform, false);
        RectTransform rt = dot.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(SwatchSize, SwatchSize);

        Image img = dot.AddComponent<Image>();
        img.sprite = RoundedSprite.Get((int)MetaTokens.RadiusCircle);
        img.type = Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
    }

    private GameObject NewEntryRow(Transform listRoot)
    {
        GameObject row = new GameObject("Entry");
        row.transform.SetParent(listRoot, false);
        row.AddComponent<RectTransform>();

        LayoutElement le = row.AddComponent<LayoutElement>();
        le.minHeight = EntryHeight;
        le.preferredHeight = EntryHeight;
        le.flexibleHeight = 0f;

        Image bg = row.AddComponent<Image>();
        bg.sprite = RoundedSprite.Get(12);
        bg.type = Image.Type.Sliced;
        bg.color = RowBg;

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = MetaTokens.Spacing;
        hlg.padding = new RectOffset(RowPadX, RowPadX, 0, 0);
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        return row;
    }

    public void Clear()
    {
        ClearSection(_cells);
        ClearSection(_sheets);
        _order.Clear();
        OnChanged?.Invoke();
    }

    private void ClearSection(SectionUI s)
    {
        for (int i = 0; i < s.Entries.Count; i++)
            RemoveEntry(s.Entries[i]);
        s.Entries.Clear();
        UpdateHeaderVisibility(s);
    }

    private void RemoveEntry(Entry e)
    {
        if (e.Row != null) UnityEngine.Object.Destroy(e.Row);
        if (e.Marker != null) UnityEngine.Object.Destroy(e.Marker);
    }

    private GameObject NewBareRow(Transform parent, string name, float height)
    {
        GameObject row = new GameObject(name);
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>();
        LayoutElement le = row.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        le.flexibleHeight = 0f;
        return row;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string text, float size,
        Color color, TextAlignmentOptions align, float preferredWidth, bool flexible)
    {
        GameObject o = new GameObject(name);
        o.transform.SetParent(parent, false);
        o.AddComponent<RectTransform>();

        LayoutElement le = o.AddComponent<LayoutElement>();
        if (flexible)
        {
            le.flexibleWidth = 1f;
            le.minWidth = 0f;
            le.preferredWidth = 0f;
        }
        else if (preferredWidth > 0f)
        {
            le.preferredWidth = preferredWidth;
        }

        TextMeshProUGUI t = o.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        if (_font != null) t.font = _font;
        return t;
    }
}
