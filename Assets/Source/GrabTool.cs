using System.Collections.Generic;
using UnityEngine;

public class GrabTool : SheetTool
{
    public struct MoveRecord
    {
        public int rMin, rMax, cMin, cMax;
        public Vector3 prePos;
        public Quaternion preRot;
    }

    public float yGrabBounds = 1f;

    private const float PollInterval = 0.2f;

    private bool _active;
    private float _nextPollTime;
    private int _enableQueuedFrame = -1;

    private void Start()
    {
        ResolveSheetRefs(true);

        if (sheetManager != null) sheetManager.SetGrabBounds(yGrabBounds);

        if (toolController != null)
        {
            toolController.OnToolChanged += OnToolChanged;
            toolController.OnToolReset += OnToolReset;
        }
        if (sheetManager != null)
        {
            sheetManager.OnSheetsChanged += ApplyGrabEnabled;
            sheetManager.OnSheetMoveCommitted += OnSheetMoveCommitted;
        }

        SetActive(toolController != null && toolController.SelectedTool == ToolType.Grab);
    }

    private void OnDestroy()
    {
        if (toolController != null)
        {
            toolController.OnToolChanged -= OnToolChanged;
            toolController.OnToolReset -= OnToolReset;
        }
        if (sheetManager != null)
        {
            sheetManager.OnSheetsChanged -= ApplyGrabEnabled;
            sheetManager.OnSheetMoveCommitted -= OnSheetMoveCommitted;
        }
    }

    private void OnToolChanged(ToolType selected) => SetActive(selected == ToolType.Grab);

    private void OnToolReset(ToolType tool)
    {
        if (tool != ToolType.Grab) return;
        if (sheetManager != null) sheetManager.ResetGrabs();
    }

    private void OnSheetMoveCommitted(SheetManager.Sheet sheet, Vector3 prePos, Quaternion preRot)
    {
        if (sheet == null) return;
        if (toolController != null)
            toolController.Journal.PushMove(new MoveRecord
            {
                rMin = sheet.rowMin, rMax = sheet.rowMax, cMin = sheet.colMin, cMax = sheet.colMax,
                prePos = prePos, preRot = preRot
            });
    }

    private void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active;

        if (active)
        {
            if (sheetManager != null)
            {
                sheetManager.SetGrabBounds(yGrabBounds);
                sheetManager.EnsureBaked();
            }
            ApplyGrabEnabled();
        }
        else
        {
            if (sheetManager != null && sheetManager.IsBaked)
                sheetManager.CaptureMovedPoses();
            ApplyGrabEnabled();
            if (sheetManager != null && sheetManager.IsBaked && !sheetManager.HasInvasiveEdits)
                sheetManager.Unbake();
        }
    }

    private void ApplyGrabEnabled()
    {
        if (sheetManager == null) return;
        if (_active && sheetManager.IsBaked)
        {
            _enableQueuedFrame = Time.frameCount;
        }
        else
        {
            _enableQueuedFrame = -1;
            sheetManager.SetPiecesGrabbable(false);
        }
    }

    private void Update()
    {
        if (_enableQueuedFrame >= 0 && Time.frameCount > _enableQueuedFrame)
        {
            _enableQueuedFrame = -1;
            if (_active && sheetManager != null && sheetManager.IsBaked)
                sheetManager.SetPiecesGrabbable(true);
        }

        if (!_active || sheetManager == null || !sheetManager.IsBaked) return;
        if (Time.unscaledTime < _nextPollTime) return;
        _nextPollTime = Time.unscaledTime + PollInterval;

        sheetManager.CaptureMovedPoses();
    }
}
