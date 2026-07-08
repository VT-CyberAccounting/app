using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;

public class SliceTool : SheetTool
{
    [UnityEngine.Serialization.FormerlySerializedAs("toolPanel")]
    public ToolPanelUI toolPanelUI;

    public Color lineColor = new Color(0f, 0f, 0f, 1f);
    public float lineWidth = 0.025f;
    public float dashTiling = 5f;
    public float sliceGapCells = 0.5f;

    private SliceAxis _axis = SliceAxis.Column;
    private bool _axisActive;
    private RayInteractable _ray;
    private bool _hooked;
    private bool _active;
    private LineRenderer _previewLine;

    private ToolToggleRow _toggles;

    private static Material _dashedLineMaterial;

    private static Material DashedLineMaterial
    {
        get
        {
            if (_dashedLineMaterial == null)
            {
                Texture2D tex = new Texture2D(2, 1, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Point
                };
                tex.SetPixel(0, 0, Color.white);
                tex.SetPixel(1, 0, new Color(1f, 1f, 1f, 0f));
                tex.Apply();

                _dashedLineMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
            }
            return _dashedLineMaterial;
        }
    }

    private struct CutInfo
    {
        public bool valid;
        public int rMin, rMax, cMin, cMax;
        public Vector3 piecePos;
        public Quaternion pieceRot;
        public Vector3 center;
        public int boundary;
        public SheetManager.Sheet sheet;
    }

    private void Start()
    {
        ResolveSheetRefs(true);
        if (toolPanelUI == null) toolPanelUI = FindAnyObjectByType<ToolPanelUI>();

        if (sheetGenerator != null)
        {
            _ray = sheetGenerator.GetComponentInChildren<RayInteractable>(true);

            GameObject lineObj = new GameObject("SliceCutPreview");
            lineObj.transform.SetParent(sheetGenerator.transform, false);
            _previewLine = lineObj.AddComponent<LineRenderer>();
            _previewLine.useWorldSpace = false;
            _previewLine.positionCount = 2;
            _previewLine.widthMultiplier = lineWidth;
            _previewLine.numCapVertices = 0;
            _previewLine.alignment = LineAlignment.View;
            _previewLine.textureMode = LineTextureMode.Tile;
            _previewLine.textureScale = new Vector2(dashTiling, 1f);
            _previewLine.sharedMaterial = DashedLineMaterial;
            _previewLine.startColor = lineColor;
            _previewLine.endColor = lineColor;
            _previewLine.enabled = false;
        }

        BuildAxisButtons();

        if (toolController != null)
        {
            toolController.OnToolChanged += OnToolChanged;
            toolController.OnToolReset += OnToolReset;
        }

        ApplyAxisVisuals();
        SetActive(toolController != null && toolController.SelectedTool == ToolType.Slice);
    }

    private void OnDestroy()
    {
        if (toolController != null)
        {
            toolController.OnToolChanged -= OnToolChanged;
            toolController.OnToolReset -= OnToolReset;
        }
        Unhook();
    }

    private void OnToolChanged(ToolType selected) => SetActive(selected == ToolType.Slice);

    private void OnToolReset(ToolType tool)
    {
        if (tool != ToolType.Slice) return;
        if (sheetManager != null) sheetManager.ResetSlices();
        _axisActive = false;
        ApplyAxisVisuals();
        HidePreview();
    }

    private void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active;
        if (active) Hook();
        else { Unhook(); HidePreview(); }
    }

    private void Hook()
    {
        if (_hooked || _ray == null) return;
        _ray.WhenPointerEventRaised += OnPointerEvent;
        _hooked = true;
    }

    private void Unhook()
    {
        if (!_hooked) return;
        if (_ray != null) _ray.WhenPointerEventRaised -= OnPointerEvent;
        _hooked = false;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Hover:
            case PointerEventType.Move:
                UpdatePreview(evt.Pose.position);
                break;
            case PointerEventType.Select:
                Commit(evt.Pose.position);
                break;
            case PointerEventType.Unhover:
            case PointerEventType.Cancel:
                HidePreview();
                break;
        }
    }

    private CutInfo ComputeCut(Vector3 world)
    {
        CutInfo info = default;
        if (!_axisActive) return info;
        if (sheetGenerator == null) return info;

        (int vr, int vc) = sheetGenerator.GetNearestVisibleCell(world);
        if (vr < 0) return info;

        float colSp = sheetGenerator.CurrentColumnSpacing;
        float rowSp = sheetGenerator.CurrentRowSpacing;

        int rMin, rMax, cMin, cMax;
        Vector3 piecePos, center;
        Quaternion pieceRot;
        SheetManager.Sheet sheet = null;

        if (sheetManager != null && sheetManager.IsBaked)
        {
            sheet = sheetManager.SheetAt(vr, vc);
            if (sheet == null) return info;
            rMin = sheet.rowMin; rMax = sheet.rowMax; cMin = sheet.colMin; cMax = sheet.colMax;
            center = new Vector3((cMin + cMax) * 0.5f * colSp, 0f, (rMin + rMax) * 0.5f * rowSp);
            if (sheet.piece != null)
            {
                piecePos = sheet.piece.transform.localPosition;
                pieceRot = sheet.piece.transform.localRotation;
            }
            else
            {
                piecePos = center + sheet.offset;
                pieceRot = Quaternion.identity;
            }
        }
        else
        {
            rMin = 0; rMax = sheetGenerator.RowCount - 1;
            cMin = 0; cMax = sheetGenerator.VisibleColCount - 1;
            center = Vector3.zero;
            piecePos = Vector3.zero;
            pieceRot = Quaternion.identity;
        }

        Vector3 local = sheetGenerator.transform.InverseTransformPoint(world);
        Vector3 pieceLocal = Quaternion.Inverse(pieceRot) * (local - piecePos);

        int boundary;
        if (_axis == SliceAxis.Column)
        {
            if (cMax - cMin < 1) return info;
            float fc = (pieceLocal.x + center.x) / Mathf.Max(colSp, 1e-4f);
            boundary = Mathf.Clamp(Mathf.RoundToInt(fc - 0.5f), cMin, cMax - 1);
        }
        else
        {
            if (rMax - rMin < 1) return info;
            float fr = (pieceLocal.z + center.z) / Mathf.Max(rowSp, 1e-4f);
            boundary = Mathf.Clamp(Mathf.RoundToInt(fr - 0.5f), rMin, rMax - 1);
        }

        info.valid = true;
        info.rMin = rMin; info.rMax = rMax; info.cMin = cMin; info.cMax = cMax;
        info.piecePos = piecePos; info.pieceRot = pieceRot; info.center = center;
        info.boundary = boundary; info.sheet = sheet;
        return info;
    }

    private void UpdatePreview(Vector3 world)
    {
        CutInfo cut = ComputeCut(world);
        if (!cut.valid || _previewLine == null)
        {
            HidePreview();
            return;
        }

        float colSp = sheetGenerator.CurrentColumnSpacing;
        float rowSp = sheetGenerator.CurrentRowSpacing;
        float top = sheetGenerator.SheetTopY + 0.012f;

        float half = sheetGenerator.LockedCellSize * 0.5f;

        Vector3 a0, a1;
        if (_axis == SliceAxis.Column)
        {
            float x = (cut.boundary + 0.5f) * colSp;
            a0 = new Vector3(x, top, cut.rMin * rowSp - half);
            a1 = new Vector3(x, top, cut.rMax * rowSp + half);
        }
        else
        {
            float z = (cut.boundary + 0.5f) * rowSp;
            a0 = new Vector3(cut.cMin * colSp - half, top, z);
            a1 = new Vector3(cut.cMax * colSp + half, top, z);
        }

        Vector3 p0 = cut.piecePos + cut.pieceRot * (a0 - cut.center);
        Vector3 p1 = cut.piecePos + cut.pieceRot * (a1 - cut.center);

        _previewLine.SetPosition(0, p0);
        _previewLine.SetPosition(1, p1);
        _previewLine.enabled = true;
    }

    private void HidePreview()
    {
        if (_previewLine != null) _previewLine.enabled = false;
    }

    private void Commit(Vector3 world)
    {
        if (sheetManager == null) return;

        CutInfo cut = ComputeCut(world);
        if (!cut.valid) return;

        bool wasBaked = sheetManager.IsBaked;
        if (!wasBaked) sheetManager.EnsureBaked();

        cut = ComputeCut(world);
        if (!cut.valid || cut.sheet == null)
        {
            if (!wasBaked) sheetManager.Unbake();
            return;
        }

        if (sheetManager.Slice(cut.sheet, _axis, cut.boundary, sliceGapCells * sheetGenerator.LockedCellSize, out SheetManager.SliceRecord record))
        {
            if (toolController != null) toolController.Journal.PushSlice(record);
        }
        else if (!wasBaked)
            sheetManager.Unbake();

        UpdatePreview(world);
    }

    private void ToggleAxis(SliceAxis axis)
    {
        if (_axisActive && _axis == axis) _axisActive = false;
        else { _axis = axis; _axisActive = true; }
        ApplyAxisVisuals();
        HidePreview();
    }

    public string CurrentAxisName => !_axisActive ? "none" : (_axis == SliceAxis.Row ? "rows" : "columns");

    public bool SetAxis(string axis)
    {
        switch (axis)
        {
            case "column": case "columns": _axis = SliceAxis.Column; _axisActive = true; break;
            case "row": case "rows": _axis = SliceAxis.Row; _axisActive = true; break;
            case "none": _axisActive = false; break;
            default: return false;
        }
        ApplyAxisVisuals();
        HidePreview();
        return true;
    }

    private void ApplyAxisVisuals()
    {
        if (_toggles != null)
            _toggles.SetActive(!_axisActive ? -1 : (_axis == SliceAxis.Column ? 0 : 1));
    }

    private void BuildAxisButtons()
    {
        if (toolPanelUI == null) return;
        GameObject content = toolPanelUI.GetToolContent(ToolType.Slice);
        if (content == null) return;

        _toggles = new ToolToggleRow(content, "AxisButtons", toolPanelUI.bodyFont);
        _toggles.AddButton("SliceColumns", "Slice Columns",
            "This button opens the column section.", () => ToggleAxis(SliceAxis.Column));
        _toggles.AddButton("SliceRows", "Slice Rows",
            "This button opens the row section.", () => ToggleAxis(SliceAxis.Row));
    }
}
