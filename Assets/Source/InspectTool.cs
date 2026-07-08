using UnityEngine;
using Oculus.Interaction;

public class InspectTool : SheetTool
{
    public ToolPanelUI toolPanelUI;
    public DataSource dataSource;
    public Tooltip tooltip;

    [UnityEngine.Serialization.FormerlySerializedAs("rectangleColor")]
    [UnityEngine.Serialization.FormerlySerializedAs("regionPinColor")]
    [UnityEngine.Serialization.FormerlySerializedAs("regionSelectColor")]
    [UnityEngine.Serialization.FormerlySerializedAs("regionHoverColor")]
    public Color sheetHoverColor = MetaTokens.Blue;
    [UnityEngine.Serialization.FormerlySerializedAs("rectangleWidth")]
    [UnityEngine.Serialization.FormerlySerializedAs("regionPinWidth")]
    [UnityEngine.Serialization.FormerlySerializedAs("regionSelectXOrZ")]
    [UnityEngine.Serialization.FormerlySerializedAs("regionHoverXOrZ")]
    public float sheetHoverXOrZ = 0.025f;

    private enum InspectMode { CellSheet, Columns, Rows }

    private SheetTargetSelector _selector;
    private SheetPin _sheetPin;
    private bool _active;
    private InspectMode _mode = InspectMode.CellSheet;

    private InspectMode _lastBandMode = InspectMode.CellSheet;
    private int _lastBandR0 = -1, _lastBandR1 = -1, _lastBandC0 = -1, _lastBandC1 = -1;

    private SheetPreview _preview;
    private ToolToggleRow _toggles;

    private void Start()
    {
        ResolveSheetRefs(false);
        if (toolPanelUI == null) toolPanelUI = FindAnyObjectByType<ToolPanelUI>();
        if (tooltip == null) tooltip = FindAnyObjectByType<Tooltip>();
        if (dataSource == null && sheetGenerator != null) dataSource = sheetGenerator.dataSource;
        if (dataSource == null) dataSource = DatasetManager.ActiveSource ?? FileReader.Instance;

        BuildModeButtons();

        if (sheetGenerator != null)
        {
            RayInteractable ray = sheetGenerator.GetComponentInChildren<RayInteractable>(true);
            _selector = new SheetTargetSelector(sheetGenerator, ray);
            _selector.OnPreview += OnPreview;
            _selector.OnCleared += OnCleared;

            GameObject pinObj = new GameObject("InspectSheetPin");
            pinObj.transform.SetParent(sheetGenerator.transform, false);
            _sheetPin = pinObj.AddComponent<SheetPin>();
            _sheetPin.Init(sheetGenerator, sheetHoverColor, sheetHoverXOrZ);
            _sheetPin.SetSortingOrder(SheetPin.HoverSortingOrder);

            _preview = new SheetPreview(sheetGenerator, sheetManager, tooltip, _sheetPin, sheetHoverColor);
        }

        if (toolController != null)
        {
            toolController.OnToolChanged += OnToolChanged;
            toolController.OnToolReset += OnToolReset;
        }

        ApplyModeVisuals();
        SetActive(toolController != null && toolController.SelectedTool == ToolType.Inspect);
    }

    private void OnDestroy()
    {
        if (toolController != null)
        {
            toolController.OnToolChanged -= OnToolChanged;
            toolController.OnToolReset -= OnToolReset;
        }
        if (_selector != null)
        {
            _selector.OnPreview -= OnPreview;
            _selector.OnCleared -= OnCleared;
            _selector.Disable();
        }
    }

    private void OnToolChanged(ToolType selected) => SetActive(selected == ToolType.Inspect);

    private void OnToolReset(ToolType tool)
    {
        if (tool != ToolType.Inspect) return;
        ResetMode();
    }

    private void ResetMode()
    {
        _mode = InspectMode.CellSheet;
        ApplyModeVisuals();
        OnCleared();
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
            OnCleared();
        }
    }

    private void OnPreview(SheetTarget target)
    {
        if (!_active || tooltip == null) return;

        if (_mode == InspectMode.Columns || _mode == InspectMode.Rows)
        {
            ShowBand(target.worldPoint);
            return;
        }

        if (target.kind == SheetTarget.Kind.Cell) _preview.ShowCell(target, dataSource);
        else if (target.kind == SheetTarget.Kind.Sheet && !_preview.ShowSheet(target, dataSource)) OnCleared();
    }

    private void OnCleared()
    {
        if (_preview != null) _preview.Clear();
        ClearBandDedupe();
    }

    private void ClearBandDedupe()
    {
        _lastBandR0 = -1;
        _lastBandR1 = -1;
        _lastBandC0 = -1;
        _lastBandC1 = -1;
    }

    private void ShowBand(Vector3 world)
    {
        if (sheetGenerator == null) { OnCleared(); return; }

        (int vr, int vc) = sheetGenerator.GetNearestVisibleCell(world);
        if (vr < 0 || vc < 0) { OnCleared(); return; }

        int rMin, rMax, cMin, cMax;
        SheetManager.Sheet sheet = null;
        if (sheetManager != null && sheetManager.IsBaked)
        {
            sheet = sheetManager.SheetAt(vr, vc);
            if (sheet == null) { OnCleared(); return; }
            rMin = sheet.rowMin; rMax = sheet.rowMax; cMin = sheet.colMin; cMax = sheet.colMax;
        }
        else
        {
            rMin = 0; rMax = sheetGenerator.RowCount - 1;
            cMin = 0; cMax = sheetGenerator.VisibleColCount - 1;
        }

        int bandR0, bandR1, bandC0, bandC1;
        if (_mode == InspectMode.Columns)
        {
            bandR0 = rMin; bandR1 = rMax; bandC0 = vc; bandC1 = vc;
        }
        else
        {
            bandR0 = vr; bandR1 = vr; bandC0 = cMin; bandC1 = cMax;
        }

        _preview.ClearSheetDedupe();

        if (_mode == _lastBandMode && bandR0 == _lastBandR0 && bandR1 == _lastBandR1 &&
            bandC0 == _lastBandC0 && bandC1 == _lastBandC1 && tooltip.IsVisible)
        {
            tooltip.UpdatePosition(world);
            return;
        }

        _lastBandMode = _mode;
        _lastBandR0 = bandR0;
        _lastBandR1 = bandR1;
        _lastBandC0 = bandC0;
        _lastBandC1 = bandC1;

        SheetTarget bandTarget = SheetTarget.Sheet(bandR0, bandR1, bandC0, bandC1, world);
        SheetStatsResult s = SheetReadout.ShowHeader(tooltip, sheetGenerator, dataSource, bandTarget,
            _mode == InspectMode.Columns);
        if (!s.valid)
        {
            OnCleared();
            return;
        }

        if (_sheetPin != null)
        {
            _sheetPin.SetColor(sheetHoverColor);
            _sheetPin.Show(bandTarget, sheetManager);
        }
    }

    private void SetMode(InspectMode mode)
    {
        _mode = _mode == mode ? InspectMode.CellSheet : mode;
        ApplyModeVisuals();
        OnCleared();
    }

    public string CurrentModeName =>
        _mode == InspectMode.Columns ? "columns" :
        _mode == InspectMode.Rows ? "rows" : "cell";

    public bool SetInspectMode(string mode)
    {
        switch (mode)
        {
            case "cell": case "cellsheet": case "none": _mode = InspectMode.CellSheet; break;
            case "column": case "columns": _mode = InspectMode.Columns; break;
            case "row": case "rows": _mode = InspectMode.Rows; break;
            default: return false;
        }
        ApplyModeVisuals();
        OnCleared();
        return true;
    }

    private void ApplyModeVisuals()
    {
        if (_toggles != null)
            _toggles.SetActive(_mode == InspectMode.Columns ? 0 : _mode == InspectMode.Rows ? 1 : -1);
    }

    private void BuildModeButtons()
    {
        if (toolPanelUI == null) return;
        GameObject content = toolPanelUI.GetToolContent(ToolType.Inspect);
        if (content == null) return;

        _toggles = new ToolToggleRow(content, "ModeButtons", toolPanelUI.bodyFont);
        _toggles.AddButton("InspectColumns", "Inspect Columns",
            "This button opens the column section.", () => SetMode(InspectMode.Columns));
        _toggles.AddButton("InspectRows", "Inspect Rows",
            "This button opens the row section.", () => SetMode(InspectMode.Rows));
    }
}
