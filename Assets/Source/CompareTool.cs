using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;

public class CompareTool : SheetTool
{
    [UnityEngine.Serialization.FormerlySerializedAs("toolPanel")]
    public ToolPanelUI toolPanelUI;
    public DataSource dataSource;
    public Tooltip tooltip;
    public InspectTool inspectTool;

    public Color[] palette =
    {
        new Color(1.00f, 1.00f, 1.00f),
        new Color(0.90f, 0.22f, 0.22f),
        new Color(0.95f, 0.55f, 0.15f),
        new Color(0.95f, 0.85f, 0.22f),
        new Color(0.32f, 0.80f, 0.38f),
        new Color(0.25f, 0.55f, 0.95f),
        new Color(0.29f, 0.00f, 0.51f),
        new Color(0.56f, 0.00f, 1.00f),
        new Color(0.00f, 0.00f, 0.00f),
    };

    [UnityEngine.Serialization.FormerlySerializedAs("rectangleWidth")]
    [UnityEngine.Serialization.FormerlySerializedAs("regionPinWidth")]
    [UnityEngine.Serialization.FormerlySerializedAs("sheetPinWidth")]
    public float sheetPinXOrZ = 0.025f;

    private SheetTargetSelector _selector;
    private SheetPin _sheetPin;
    private CompareLedgerUI _ledger;
    private bool _active;

    private SheetPreview _preview;

    private Color _sheetHoverColor;

    private enum CompareMode { None, Cells, Sheets }
    private CompareMode _mode = CompareMode.None;

    private ToolToggleRow _toggles;

    private int _ledgerRowCount = -1;
    private int _ledgerColCount = -1;

    private void Start()
    {
        ResolveSheetRefs(true);
        if (toolPanelUI == null) toolPanelUI = FindAnyObjectByType<ToolPanelUI>();
        if (tooltip == null) tooltip = FindAnyObjectByType<Tooltip>();
        if (inspectTool == null) inspectTool = FindAnyObjectByType<InspectTool>();
        if (dataSource == null && sheetGenerator != null) dataSource = sheetGenerator.dataSource;
        if (dataSource == null) dataSource = DatasetManager.ActiveSource ?? FileReader.Instance;

        if (sheetGenerator != null)
        {
            RayInteractable ray = sheetGenerator.GetComponentInChildren<RayInteractable>(true);
            _selector = new SheetTargetSelector(sheetGenerator, ray);
            _selector.OnPreview += OnPreview;
            _selector.OnCommit += OnCommit;
            _selector.OnCleared += OnCleared;

            _sheetHoverColor = inspectTool != null ? inspectTool.sheetHoverColor : MetaTokens.Blue;
            float previewWidth = inspectTool != null ? inspectTool.sheetHoverXOrZ : sheetPinXOrZ;
            GameObject pinObj = new GameObject("CompareSheetPin");
            pinObj.transform.SetParent(sheetGenerator.transform, false);
            _sheetPin = pinObj.AddComponent<SheetPin>();
            _sheetPin.Init(sheetGenerator, _sheetHoverColor, previewWidth);
            _sheetPin.SetSortingOrder(SheetPin.HoverSortingOrder);

            _preview = new SheetPreview(sheetGenerator, sheetManager, tooltip, _sheetPin, _sheetHoverColor);

            sheetGenerator.OnSheetCollapseStarted += OnSheetCollapsed;
            sheetGenerator.OnSheetLayoutChanged += OnSheetLayoutChanged;
        }

        if (sheetManager != null)
        {
            sheetManager.OnSliced += OnSliced;
            sheetManager.OnSlicesReset += OnSlicesReset;
            sheetManager.OnSliceUndone += OnSliceUndone;
        }

        if (toolPanelUI != null)
        {
            GameObject content = toolPanelUI.GetToolContent(ToolType.Compare);
            if (content != null)
            {
                _ledger = new CompareLedgerUI(content.transform, toolPanelUI.bodyFont, palette);
                _ledger.OnChanged += OnLedgerChanged;
                _ledger.OnEvicted += OnLedgerEvicted;
            }
        }

        BuildModeButtons();
        ApplyMode();

        if (toolController != null)
        {
            toolController.OnToolChanged += OnToolChanged;
            toolController.OnToolReset += OnToolReset;
        }
        SetActive(toolController != null && toolController.SelectedTool == ToolType.Compare);
    }

    private void OnDestroy()
    {
        if (toolController != null)
        {
            toolController.OnToolChanged -= OnToolChanged;
            toolController.OnToolReset -= OnToolReset;
        }
        if (sheetGenerator != null)
        {
            sheetGenerator.OnSheetCollapseStarted -= OnSheetCollapsed;
            sheetGenerator.OnSheetLayoutChanged -= OnSheetLayoutChanged;
        }
        if (sheetManager != null)
        {
            sheetManager.OnSliced -= OnSliced;
            sheetManager.OnSlicesReset -= OnSlicesReset;
            sheetManager.OnSliceUndone -= OnSliceUndone;
        }
        if (_selector != null)
        {
            _selector.OnPreview -= OnPreview;
            _selector.OnCommit -= OnCommit;
            _selector.OnCleared -= OnCleared;
            _selector.Disable();
        }
        if (_ledger != null)
        {
            _ledger.OnChanged -= OnLedgerChanged;
            _ledger.OnEvicted -= OnLedgerEvicted;
        }
        if (toolController != null) toolController.SetLock(this, false);
    }

    private void OnLedgerChanged()
    {
        if (_ledger != null && _ledger.HasEntries && sheetGenerator != null)
        {
            _ledgerRowCount = sheetGenerator.RowCount;
            _ledgerColCount = sheetGenerator.VisibleColCount;
        }
        if (toolController != null) toolController.SetLock(this, _ledger != null && _ledger.HasEntries);
    }

    private void OnSheetLayoutChanged()
    {
        if (_ledger == null || !_ledger.HasEntries || sheetGenerator == null) return;
        if (sheetGenerator.RowCount == _ledgerRowCount &&
            sheetGenerator.VisibleColCount == _ledgerColCount) return;
        _ledger.Clear();
        SheetNotices.EditsDropped(this,
            "The dataset changed shape, so its edits and compare pins were cleared.");
    }

    private void OnSheetCollapsed()
    {
        if (_ledger != null) _ledger.Clear();
    }

    private void OnToolChanged(ToolType selected) => SetActive(selected == ToolType.Compare);

    private void OnToolReset(ToolType tool)
    {
        if (tool != ToolType.Compare) return;
        if (_ledger != null) _ledger.Clear();
        _mode = CompareMode.None;
        ApplyMode();
    }

    private void OnLedgerEvicted(CompareLedgerUI.Entry entry)
    {
        if (toolController != null) toolController.Journal.PrunePins(HasPinFor);
    }

    public bool HasPinFor(EditJournal.Record r)
    {
        if (_ledger == null || r == null) return false;
        if (r.pinIsSheet)
        {
            var entries = _ledger.SheetEntries;
            for (int i = 0; i < entries.Count; i++)
                if (MatchesOrigin(entries[i], r)) return true;
            return false;
        }
        var cells = _ledger.CellEntries;
        for (int i = 0; i < cells.Count; i++)
            if (MatchesCell(cells[i], r)) return true;
        return false;
    }

    public bool UndoPin(EditJournal.Record r)
    {
        if (_ledger == null || r == null) return false;
        bool removed = false;
        if (r.pinIsSheet)
        {
            var entries = new List<CompareLedgerUI.Entry>(_ledger.SheetEntries);
            for (int i = 0; i < entries.Count; i++)
                if (MatchesOrigin(entries[i], r) && _ledger.Remove(entries[i])) removed = true;
        }
        else
        {
            var cells = _ledger.CellEntries;
            for (int i = 0; i < cells.Count; i++)
                if (MatchesCell(cells[i], r)) { removed = _ledger.Remove(cells[i]); break; }
        }
        return removed;
    }

    private static bool MatchesOrigin(CompareLedgerUI.Entry e, EditJournal.Record r) =>
        e.ORMin == r.pinORowMin && e.ORMax == r.pinORowMax &&
        e.OCMin == r.pinOColMin && e.OCMax == r.pinOColMax;

    private static bool MatchesCell(CompareLedgerUI.Entry e, EditJournal.Record r) =>
        e.RMin == r.pinRow && e.CMin == r.pinCol &&
        e.ColumnTitle == r.pinColumnTitle && e.RowTitle == r.pinRowTitle;

    private void OnSliceUndone(int rMin, int rMax, int cMin, int cMax, SliceAxis axis, int boundary)
    {
        if (_ledger == null) return;

        int aRMin, aRMax, aCMin, aCMax, bRMin, bRMax, bCMin, bCMax;
        if (axis == SliceAxis.Column)
        {
            aRMin = rMin; aRMax = rMax; aCMin = cMin; aCMax = boundary;
            bRMin = rMin; bRMax = rMax; bCMin = boundary + 1; bCMax = cMax;
        }
        else
        {
            aRMin = rMin; aRMax = boundary; aCMin = cMin; aCMax = cMax;
            bRMin = boundary + 1; bRMax = rMax; bCMin = cMin; bCMax = cMax;
        }

        var entries = new List<CompareLedgerUI.Entry>(_ledger.SheetEntries);
        var processed = new HashSet<(int, int, int, int)>();
        for (int i = 0; i < entries.Count; i++)
        {
            CompareLedgerUI.Entry ea = entries[i];
            if (ea.RMin != aRMin || ea.RMax != aRMax || ea.CMin != aCMin || ea.CMax != aCMax) continue;
            var origin = (ea.ORMin, ea.ORMax, ea.OCMin, ea.OCMax);
            if (!processed.Add(origin)) continue;

            CompareLedgerUI.Entry eb = null;
            for (int j = 0; j < entries.Count; j++)
            {
                CompareLedgerUI.Entry c = entries[j];
                if (c.RMin == bRMin && c.RMax == bRMax && c.CMin == bCMin && c.CMax == bCMax
                    && c.ORMin == ea.ORMin && c.ORMax == ea.ORMax && c.OCMin == ea.OCMin && c.OCMax == ea.OCMax)
                {
                    eb = c;
                    break;
                }
            }
            if (eb == null) continue;

            _ledger.RemoveSheetEntry(ea);
            _ledger.RemoveSheetEntry(eb);
            AddSheetComparison(rMin, rMax, cMin, cMax, ea.ORMin, ea.ORMax, ea.OCMin, ea.OCMax);
        }
    }

    private void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active;
        if (_selector == null) return;

        if (active) _selector.Enable();
        else
        {
            _selector.Disable();
            if (tooltip != null) tooltip.Hide();
            if (_sheetPin != null) _sheetPin.Hide();
        }
    }

    private void OnPreview(SheetTarget target)
    {
        if (!_active || tooltip == null) return;

        if (target.kind == SheetTarget.Kind.Cell && _mode == CompareMode.Cells) _preview.ShowCell(target, dataSource);
        else if (target.kind == SheetTarget.Kind.Sheet && _mode == CompareMode.Sheets) _preview.ShowSheet(target, dataSource);
        else _preview.Clear();
    }

    private void OnCleared()
    {
        if (_preview != null) _preview.Clear();
    }

    private void OnCommit(SheetTarget target)
    {
        if (!_active || _ledger == null) return;
        if (_mode == CompareMode.None) return;

        if (target.kind == SheetTarget.Kind.Cell)
        {
            if (_mode != CompareMode.Cells) return;
            if (dataSource == null || !dataSource.IsLoaded) return;
            if (target.dataRow < 0 || target.dataRow >= dataSource.RowCount) return;
            if (target.dataCol < 0 || target.dataCol >= dataSource.ColumnCount) return;

            int visRow = sheetGenerator != null ? sheetGenerator.DataRowToVisible(target.dataRow) : -1;
            int visCol = sheetGenerator != null ? sheetGenerator.DataColToVisible(target.dataCol) : -1;
            if (visRow < 0 || visCol < 0) return;

            float value = dataSource.GetValue(target.dataRow, target.dataCol);
            string columnTitle = dataSource.ColumnTitles[target.dataCol];
            string rowTitle = dataSource.RowTitles[target.dataRow];

            Color color = _ledger.ReserveColor(false);
            GameObject marker = AddSheetMarker(visRow, visRow, visCol, visCol, color, PaletteIndex(color));
            CompareLedgerUI.Entry entry = _ledger.AddCellEntry(columnTitle, rowTitle, value, color, marker);
            entry.RMin = visRow; entry.RMax = visRow; entry.CMin = visCol; entry.CMax = visCol;
            if (toolController != null) toolController.Journal.PushCellPin(visRow, visCol, columnTitle, rowTitle);
        }
        else if (target.kind == SheetTarget.Kind.Sheet)
        {
            if (_mode != CompareMode.Sheets) return;
            int rMin = target.visRowMin, rMax = target.visRowMax;
            int cMin = target.visColMin, cMax = target.visColMax;
            if (AddSheetComparison(rMin, rMax, cMin, cMax, rMin, rMax, cMin, cMax) && toolController != null)
                toolController.Journal.PushSheetPin(rMin, rMax, cMin, cMax);
        }
    }

    private bool AddSheetComparison(int rMin, int rMax, int cMin, int cMax,
        int oRMin, int oRMax, int oCMin, int oCMax, Color? forcedColor = null)
    {
        SheetStatsResult s = SheetStats.Compute(sheetGenerator, dataSource,
            SheetTarget.Sheet(rMin, rMax, cMin, cMax, Vector3.zero));
        if (!s.valid) return false;

        Color color = forcedColor ?? _ledger.ReserveColor(true);
        GameObject marker = AddSheetMarker(rMin, rMax, cMin, cMax, color, 10 + PaletteIndex(color));
        _ledger.AddSheetEntry(s.count, s.average, s.sum, color, marker,
            rMin, rMax, cMin, cMax, oRMin, oRMax, oCMin, oCMax);
        return true;
    }

    private void OnSliced(SliceAxis axis, int boundary)
    {
        if (_ledger == null) return;

        var entries = new List<CompareLedgerUI.Entry>(_ledger.SheetEntries);
        for (int i = 0; i < entries.Count; i++)
        {
            CompareLedgerUI.Entry e = entries[i];
            bool crosses = axis == SliceAxis.Column
                ? e.CMin <= boundary && boundary < e.CMax
                : e.RMin <= boundary && boundary < e.RMax;
            if (!crosses) continue;

            _ledger.RemoveSheetEntry(e);
            if (axis == SliceAxis.Column)
            {
                AddSheetComparison(e.RMin, e.RMax, e.CMin, boundary, e.ORMin, e.ORMax, e.OCMin, e.OCMax);
                AddSheetComparison(e.RMin, e.RMax, boundary + 1, e.CMax, e.ORMin, e.ORMax, e.OCMin, e.OCMax);
            }
            else
            {
                AddSheetComparison(e.RMin, boundary, e.CMin, e.CMax, e.ORMin, e.ORMax, e.OCMin, e.OCMax);
                AddSheetComparison(boundary + 1, e.RMax, e.CMin, e.CMax, e.ORMin, e.ORMax, e.OCMin, e.OCMax);
            }
        }
    }

    private void OnSlicesReset()
    {
        if (_ledger == null) return;

        var entries = new List<CompareLedgerUI.Entry>(_ledger.SheetEntries);
        var processed = new HashSet<(int, int, int, int)>();

        for (int i = 0; i < entries.Count; i++)
        {
            CompareLedgerUI.Entry e = entries[i];
            var origin = (e.ORMin, e.ORMax, e.OCMin, e.OCMax);
            if (!processed.Add(origin)) continue;

            var group = new List<CompareLedgerUI.Entry>();
            bool split = false;
            for (int j = 0; j < entries.Count; j++)
            {
                CompareLedgerUI.Entry g = entries[j];
                if ((g.ORMin, g.ORMax, g.OCMin, g.OCMax) != origin) continue;
                group.Add(g);
                if (g.RMin != g.ORMin || g.RMax != g.ORMax || g.CMin != g.OCMin || g.CMax != g.OCMax)
                    split = true;
            }
            if (group.Count == 1 && !split) continue;

            for (int j = 0; j < group.Count; j++)
                _ledger.RemoveSheetEntry(group[j]);
            AddSheetComparison(e.ORMin, e.ORMax, e.OCMin, e.OCMax, e.ORMin, e.ORMax, e.OCMin, e.OCMax);
        }
    }

    private GameObject AddSheetMarker(int rMin, int rMax, int cMin, int cMax, Color color, int sortingOrder)
    {
        Transform parent = sheetGenerator != null ? sheetGenerator.transform : transform;
        GameObject markerObj = new GameObject("CompareSheetMarker");
        markerObj.transform.SetParent(parent, false);

        SheetPin sheetPin = markerObj.AddComponent<SheetPin>();
        sheetPin.Init(sheetGenerator, color, sheetPinXOrZ);
        sheetPin.SetSortingOrder(sortingOrder);

        CompareMarker cm = markerObj.AddComponent<CompareMarker>();
        cm.Init(sheetGenerator, sheetManager, sheetPin, rMin, rMax, cMin, cMax);

        return markerObj;
    }

    private int PaletteIndex(Color color)
    {
        if (palette == null) return 0;
        for (int i = 0; i < palette.Length; i++)
            if (palette[i] == color) return i;
        return 0;
    }

    [System.Serializable]
    public class PinState
    {
        public bool isSheet;
        public Color color;
        public string columnTitle;
        public string rowTitle;
        public float value;
        public int rMin, rMax, cMin, cMax;
        public int oRMin, oRMax, oCMin, oCMax;
    }

    public class PinSnapshot
    {
        public int rowCount;
        public int colCount;
        public List<PinState> pins;
    }

    public PinSnapshot CapturePins()
    {
        if (_ledger == null || !_ledger.HasUndo || sheetGenerator == null) return null;

        var order = _ledger.UndoOrder;
        var snap = new PinSnapshot
        {
            rowCount = sheetGenerator.RowCount,
            colCount = sheetGenerator.VisibleColCount,
            pins = new List<PinState>(order.Count)
        };
        for (int i = 0; i < order.Count; i++)
        {
            CompareLedgerUI.Entry e = order[i];
            snap.pins.Add(new PinState
            {
                isSheet = e.IsSheet,
                color = e.Color,
                columnTitle = e.ColumnTitle,
                rowTitle = e.RowTitle,
                value = e.Value,
                rMin = e.RMin, rMax = e.RMax, cMin = e.CMin, cMax = e.CMax,
                oRMin = e.ORMin, oRMax = e.ORMax, oCMin = e.OCMin, oCMax = e.OCMax
            });
        }
        return snap;
    }

    public void RestorePins(PinSnapshot snap)
    {
        if (_ledger == null || snap == null || snap.pins == null || sheetGenerator == null) return;
        if (sheetGenerator.RowCount != snap.rowCount || sheetGenerator.VisibleColCount != snap.colCount)
        {
            SheetNotices.EditsDropped(this,
                "This dataset's data changed shape, so its compare pins were cleared.");
            return;
        }

        for (int i = 0; i < snap.pins.Count; i++)
        {
            PinState p = snap.pins[i];
            if (p.isSheet)
            {
                AddSheetComparison(p.rMin, p.rMax, p.cMin, p.cMax,
                    p.oRMin, p.oRMax, p.oCMin, p.oCMax, p.color);
            }
            else
            {
                int dataRow = sheetGenerator.VisibleRowToData(p.rMin);
                int dataCol = sheetGenerator.VisibleColToData(p.cMin);
                if (dataRow < 0 || dataCol < 0 || dataSource == null) continue;
                if (dataSource.RowTitles[dataRow] != p.rowTitle ||
                    dataSource.ColumnTitles[dataCol] != p.columnTitle) continue;
                float value = dataSource.GetValue(dataRow, dataCol);

                GameObject marker = AddSheetMarker(p.rMin, p.rMax, p.cMin, p.cMax, p.color, PaletteIndex(p.color));
                CompareLedgerUI.Entry e = _ledger.AddCellEntry(p.columnTitle, p.rowTitle, value, p.color, marker);
                e.RMin = p.rMin; e.RMax = p.rMax; e.CMin = p.cMin; e.CMax = p.cMax;
            }
        }
    }

    public bool HasEntries => _ledger != null && _ledger.HasEntries;

    public int CellEntryCount => _ledger != null ? _ledger.CellEntryCount : 0;

    public int SheetEntryCount => _ledger != null ? _ledger.SheetEntryCount : 0;

    public IReadOnlyList<CompareLedgerUI.Entry> CellEntries =>
        _ledger != null ? _ledger.CellEntries : System.Array.Empty<CompareLedgerUI.Entry>();

    public IReadOnlyList<CompareLedgerUI.Entry> SheetLedgerEntries =>
        _ledger != null ? _ledger.SheetEntries : System.Array.Empty<CompareLedgerUI.Entry>();

    public string CurrentModeName =>
        _mode == CompareMode.Cells ? "cells" :
        _mode == CompareMode.Sheets ? "sheets" : "none";

    public bool SetCompareMode(string mode)
    {
        switch (mode)
        {
            case "none": _mode = CompareMode.None; break;
            case "cell": case "cells": _mode = CompareMode.Cells; break;
            case "sheet": case "sheets": _mode = CompareMode.Sheets; break;
            default: return false;
        }
        ApplyMode();
        return true;
    }

    private void SetMode(CompareMode mode)
    {
        _mode = _mode == mode ? CompareMode.None : mode;
        ApplyMode();
    }

    private void ApplyMode()
    {
        ApplyModeVisuals();
        ApplyModeSections();
        if (_preview != null) _preview.Clear();
    }

    private void ApplyModeSections()
    {
        if (_ledger != null)
            _ledger.SetVisibleSections(_mode == CompareMode.Cells, _mode == CompareMode.Sheets);
    }

    private void ApplyModeVisuals()
    {
        if (_toggles != null)
            _toggles.SetActive(_mode == CompareMode.Cells ? 0 : _mode == CompareMode.Sheets ? 1 : -1);
    }

    private void BuildModeButtons()
    {
        if (toolPanelUI == null) return;
        GameObject content = toolPanelUI.GetToolContent(ToolType.Compare);
        if (content == null) return;

        _toggles = new ToolToggleRow(content, "ModeButtons", toolPanelUI.bodyFont);
        _toggles.AddButton("CompareCells", "Compare Cells",
            "This button opens the cell section.", () => SetMode(CompareMode.Cells));
        _toggles.AddButton("CompareSheets", "Compare Sheets",
            "This button opens the sheet section.", () => SetMode(CompareMode.Sheets));
    }
}
