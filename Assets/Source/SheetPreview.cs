using UnityEngine;

public class SheetPreview
{
    private readonly SheetGenerator _sheet;
    private readonly SheetManager _sheets;
    private readonly Tooltip _tooltip;
    private readonly SheetPin _pin;
    private readonly Color _hoverColor;

    private int _lastRow = -1;
    private int _lastCol = -1;
    private int _lastSheetR0 = -1, _lastSheetR1 = -1, _lastSheetC0 = -1, _lastSheetC1 = -1;

    public SheetPreview(SheetGenerator sheet, SheetManager sheets, Tooltip tooltip, SheetPin pin, Color hoverColor)
    {
        _sheet = sheet;
        _sheets = sheets;
        _tooltip = tooltip;
        _pin = pin;
        _hoverColor = hoverColor;
    }

    public void ShowCell(SheetTarget t, DataSource data)
    {
        ClearSheetDedupe();
        if (t.dataRow == _lastRow && t.dataCol == _lastCol && _tooltip.IsVisible)
        {
            _tooltip.UpdatePosition(t.worldPoint);
            return;
        }
        _lastRow = t.dataRow;
        _lastCol = t.dataCol;
        if (SheetReadout.ShowCell(_tooltip, _sheet, data, t)) ShowCellOutline(t);
        else if (_pin != null) _pin.Hide();
    }

    private void ShowCellOutline(SheetTarget t)
    {
        if (_pin == null || _sheet == null) return;

        int visRow = _sheet.DataRowToVisible(t.dataRow);
        int visCol = _sheet.DataColToVisible(t.dataCol);
        if (visRow < 0 || visCol < 0)
        {
            _pin.Hide();
            return;
        }

        _pin.SetColor(_hoverColor);
        _pin.Show(SheetTarget.Sheet(visRow, visRow, visCol, visCol, t.worldPoint), _sheets);
    }

    public bool ShowSheet(SheetTarget t, DataSource data)
    {
        _lastRow = -1;
        _lastCol = -1;
        if (t.visRowMin == _lastSheetR0 && t.visRowMax == _lastSheetR1 &&
            t.visColMin == _lastSheetC0 && t.visColMax == _lastSheetC1 && _tooltip.IsVisible)
        {
            _tooltip.UpdatePosition(t.worldPoint);
            return true;
        }
        _lastSheetR0 = t.visRowMin;
        _lastSheetR1 = t.visRowMax;
        _lastSheetC0 = t.visColMin;
        _lastSheetC1 = t.visColMax;

        SheetStatsResult s = SheetReadout.ShowSheet(_tooltip, _sheet, data, t);
        if (!s.valid) return false;

        if (_pin != null)
        {
            _pin.SetColor(_hoverColor);
            _pin.Show(t, _sheets);
        }
        return true;
    }

    public void ClearSheetDedupe()
    {
        _lastSheetR0 = -1;
        _lastSheetR1 = -1;
        _lastSheetC0 = -1;
        _lastSheetC1 = -1;
    }

    public void Clear()
    {
        _lastRow = -1;
        _lastCol = -1;
        ClearSheetDedupe();
        if (_tooltip != null) _tooltip.Hide();
        if (_pin != null) _pin.Hide();
    }
}
