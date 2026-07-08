using System;
using UnityEngine;
using Oculus.Interaction;

public class SheetTargetSelector
{
    private readonly SheetGenerator _sheet;
    private readonly RayInteractable _ray;

    public event Action<SheetTarget> OnPreview;

    public event Action<SheetTarget> OnCommit;

    public event Action OnCleared;

    private bool _enabled;
    private bool _pinching;
    private bool _dragged;
    private int _anchorVisRow;
    private int _anchorVisCol;

    public SheetTargetSelector(SheetGenerator sheet, RayInteractable ray)
    {
        _sheet = sheet;
        _ray = ray;
    }

    public void Enable()
    {
        if (_enabled || _ray == null) return;
        _ray.WhenPointerEventRaised += OnPointerEvent;
        _enabled = true;
    }

    public void Disable()
    {
        if (!_enabled) return;
        if (_ray != null) _ray.WhenPointerEventRaised -= OnPointerEvent;
        _enabled = false;
        _pinching = false;
        _dragged = false;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Hover:
                EmitHover(evt.Pose.position);
                break;

            case PointerEventType.Move:
                if (_pinching) EmitDrag(evt.Pose.position);
                else EmitHover(evt.Pose.position);
                break;

            case PointerEventType.Select:
                BeginPinch(evt.Pose.position);
                break;

            case PointerEventType.Unselect:
                EndPinch(evt.Pose.position);
                break;

            case PointerEventType.Unhover:
            case PointerEventType.Cancel:
                _pinching = false;
                _dragged = false;
                OnCleared?.Invoke();
                break;
        }
    }

    private void EmitHover(Vector3 world)
    {
        SheetTarget t = CellTarget(world);
        if (t.kind == SheetTarget.Kind.None) { OnCleared?.Invoke(); return; }
        OnPreview?.Invoke(t);
    }

    private void BeginPinch(Vector3 world)
    {
        (int vr, int vc) = _sheet.GetNearestVisibleCell(world);
        if (vr < 0)
        {
            _pinching = false;
            return;
        }

        _pinching = true;
        _dragged = false;
        _anchorVisRow = vr;
        _anchorVisCol = vc;

        SheetTarget t = CellTarget(world);
        if (t.kind != SheetTarget.Kind.None) OnPreview?.Invoke(t);
    }

    private void EmitDrag(Vector3 world)
    {
        (int vr, int vc) = _sheet.GetNearestVisibleCell(world);
        if (vr < 0) return;

        if (vr != _anchorVisRow || vc != _anchorVisCol) _dragged = true;

        if (!_dragged)
        {
            SheetTarget cell = CellTarget(world);
            if (cell.kind != SheetTarget.Kind.None) OnPreview?.Invoke(cell);
            return;
        }

        OnPreview?.Invoke(SheetTargetAt(vr, vc, world));
    }

    private void EndPinch(Vector3 world)
    {
        if (!_pinching) return;
        _pinching = false;

        SheetTarget t;
        if (_dragged)
        {
            (int vr, int vc) = _sheet.GetNearestVisibleCell(world);
            t = vr < 0 ? CellTargetFromAnchor(world) : SheetTargetAt(vr, vc, world);
        }
        else
        {
            t = CellTarget(world);
        }

        _dragged = false;
        if (t.kind == SheetTarget.Kind.None) return;

        OnPreview?.Invoke(t);
        OnCommit?.Invoke(t);
    }

    private SheetTarget CellTarget(Vector3 world)
    {
        (int row, int col) = _sheet.GetNearestCell(world);
        if (row < 0 || col < 0) return default;
        return SheetTarget.Cell(row, col, world);
    }

    private SheetTarget CellTargetFromAnchor(Vector3 world)
    {
        int dataRow = _sheet.VisibleRowToData(_anchorVisRow);
        int dataCol = _sheet.VisibleColToData(_anchorVisCol);
        if (dataRow < 0 || dataCol < 0) return default;
        return SheetTarget.Cell(dataRow, dataCol, world);
    }

    private SheetTarget SheetTargetAt(int vr, int vc, Vector3 world)
    {
        int rMin = Mathf.Min(_anchorVisRow, vr);
        int rMax = Mathf.Max(_anchorVisRow, vr);
        int cMin = Mathf.Min(_anchorVisCol, vc);
        int cMax = Mathf.Max(_anchorVisCol, vc);
        return SheetTarget.Sheet(rMin, rMax, cMin, cMax, world);
    }
}
