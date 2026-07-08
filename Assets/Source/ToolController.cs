using System;
using System.Collections.Generic;
using UnityEngine;

public enum ToolType { None, Inspect, Compare, Slice, Color, Grab }

public class ToolController : MonoBehaviour
{
    public event Action<ToolType> OnToolChanged;

    public event Action<bool> OnLockStateChanged;

    public event Action<ToolType> OnToolReset;

    public SheetManager sheetManager;

    public EditJournal Journal { get; } = new EditJournal();

    public ToolType SelectedTool { get; private set; } = ToolType.None;

    private bool _locked;
    private readonly HashSet<object> _lockSources = new HashSet<object>();

    public bool IsLocked => _locked;

    private void Start()
    {
        if (sheetManager == null) sheetManager = FindAnyObjectByType<SheetManager>();
        if (sheetManager != null)
        {
            sheetManager.OnEditStateChanged += OnSheetEditState;
            sheetManager.OnEditsDroppedByReload += OnEditsDroppedByReload;
        }
        OnSheetEditState();
    }

    private void OnDestroy()
    {
        if (sheetManager != null)
        {
            sheetManager.OnEditStateChanged -= OnSheetEditState;
            sheetManager.OnEditsDroppedByReload -= OnEditsDroppedByReload;
        }
    }

    private void OnEditsDroppedByReload() => Journal.DropSheetEdits();

    private void OnSheetEditState() =>
        SetLock(sheetManager, sheetManager != null && sheetManager.HasInvasiveEdits);

    public void SetLock(object source, bool locked)
    {
        if (source == null) return;
        bool changed = locked ? _lockSources.Add(source) : _lockSources.Remove(source);
        if (changed) RefreshLock();
    }

    private void RefreshLock()
    {
        bool locked = _lockSources.Count > 0;
        if (locked == _locked) return;
        _locked = locked;
        OnLockStateChanged?.Invoke(_locked);
    }

    public void SelectTool(ToolType tool)
    {
        ToolType next = SelectedTool == tool ? ToolType.None : tool;
        if (next == SelectedTool) return;

        SelectedTool = next;
        OnToolChanged?.Invoke(SelectedTool);
    }

    public void DeselectTool()
    {
        if (SelectedTool == ToolType.None) return;

        SelectedTool = ToolType.None;
        OnToolChanged?.Invoke(SelectedTool);
    }

    public void ResetTool(ToolType tool)
    {
        OnToolReset?.Invoke(tool);
    }

    public bool UndoLast()
    {
        EditJournal.Record rec = Journal.Pop();
        if (rec == null) return false;

        switch (rec.kind)
        {
            case EditJournal.Kind.Slice:
                if (sheetManager == null || !sheetManager.UndoSlice(rec.slice))
                    Debug.LogWarning("[ToolController] Slice undo record no longer matched the sheet.");
                break;
            case EditJournal.Kind.Move:
                if (sheetManager == null || !sheetManager.RestoreSheetPose(
                        rec.move.rMin, rec.move.rMax, rec.move.cMin, rec.move.cMax,
                        rec.move.prePos, rec.move.preRot))
                    Debug.LogWarning("[ToolController] Move undo record no longer matched the sheet.");
                break;
            case EditJournal.Kind.Color:
                if (sheetManager != null) sheetManager.UndoColorOverride();
                break;
            case EditJournal.Kind.Pin:
                CompareTool compare = Scene.Compare;
                if (compare == null || !compare.UndoPin(rec))
                    Debug.LogWarning("[ToolController] Pin undo record no longer matched the ledger.");
                break;
        }
        return true;
    }

    public void ResetAll()
    {
        for (int i = 0; i < ToolPanelConstants.Tools.Length; i++)
            ResetTool(ToolPanelConstants.Tools[i]);
        Journal.Clear();
    }
}
