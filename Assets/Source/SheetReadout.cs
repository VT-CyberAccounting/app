using System.Collections.Generic;
using UnityEngine;

public static class SheetReadout
{
    private static readonly List<Color> _swatchColors = new List<Color>(8);

    public static bool ShowCell(Tooltip tooltip, SheetGenerator sheet, DataSource data, SheetTarget t)
    {
        if (tooltip == null || data == null || !data.IsLoaded) return false;
        if (t.dataRow < 0 || t.dataRow >= data.RowCount) return false;
        if (t.dataCol < 0 || t.dataCol >= data.ColumnCount) return false;

        float raw = data.GetValue(t.dataRow, t.dataCol);
        float norm = data.GetNormalizedValue(t.dataRow, t.dataCol);

        Color swatch;
        int visRow = sheet != null ? sheet.DataRowToVisible(t.dataRow) : -1;
        int visCol = sheet != null ? sheet.DataColToVisible(t.dataCol) : -1;
        if (sheet != null && visRow >= 0 && visCol >= 0 && sheet.TryGetCellOverride(visRow, visCol, out Color ov))
            swatch = ov;
        else
            swatch = Heatmap.Sample(norm);

        tooltip.ShowCell(t.worldPoint, data.ColumnTitles[t.dataCol], data.RowTitles[t.dataRow], raw, swatch);
        return true;
    }

    public static SheetStatsResult ShowSheet(Tooltip tooltip, SheetGenerator sheet,
        DataSource data, SheetTarget t)
    {
        SheetStatsResult s = SheetStats.Compute(sheet, data, t);
        if (!s.valid || tooltip == null) return s;

        CollectOverrideColors(sheet, t);
        if (_swatchColors.Count == 0) _swatchColors.Add(Heatmap.Sample(s.normalizedAverage));

        string maxLoc = FromLabel(CellLocation(data, s.maxRow, s.maxCol));
        string minLoc = FromLabel(CellLocation(data, s.minRow, s.minCol));
        tooltip.ShowSheet(t.worldPoint, s.count, s.max, maxLoc, s.min, minLoc, s.average, s.sum, false, _swatchColors);
        return s;
    }

    public static SheetStatsResult ShowHeader(Tooltip tooltip, SheetGenerator sheet,
        DataSource data, SheetTarget t, bool isColumn)
    {
        SheetStatsResult s = SheetStats.Compute(sheet, data, t);
        if (!s.valid || tooltip == null) return s;

        string title = HeaderName(sheet, data, t, isColumn);
        tooltip.ShowHeader(t.worldPoint, title, s.count, s.average, s.sum, false);
        return s;
    }

    private static string HeaderName(SheetGenerator sheet, DataSource data, SheetTarget t, bool isColumn)
    {
        if (isColumn)
            return ColumnName(data, sheet != null ? sheet.VisibleColToData(t.visColMin) : t.visColMin);
        return RowName(data, sheet != null ? sheet.VisibleRowToData(t.visRowMin) : t.visRowMin);
    }

    private static string ColumnName(DataSource data, int dataCol) =>
        data != null && dataCol >= 0 && dataCol < data.ColumnCount ? data.ColumnTitles[dataCol] : string.Empty;

    private static string RowName(DataSource data, int dataRow) =>
        data != null && dataRow >= 0 && dataRow < data.RowCount ? data.RowTitles[dataRow] : string.Empty;

    private static void CollectOverrideColors(SheetGenerator sheet, SheetTarget t)
    {
        _swatchColors.Clear();
        if (sheet == null) return;

        for (int vr = t.visRowMin; vr <= t.visRowMax; vr++)
        {
            for (int vc = t.visColMin; vc <= t.visColMax; vc++)
            {
                if (!sheet.TryGetCellOverride(vr, vc, out Color c)) continue;

                bool seen = false;
                for (int i = 0; i < _swatchColors.Count; i++)
                {
                    if (ColorsMatch(_swatchColors[i], c)) { seen = true; break; }
                }
                if (!seen) _swatchColors.Add(c);
            }
        }
    }

    private static string FromLabel(string location) =>
        string.IsNullOrEmpty(location) ? location : "from " + location;

    private static bool ColorsMatch(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.004f
            && Mathf.Abs(a.g - b.g) < 0.004f
            && Mathf.Abs(a.b - b.b) < 0.004f;
    }

    public static string CellLocation(DataSource data, int dataRow, int dataCol)
    {
        if (data == null) return string.Empty;
        string col = dataCol >= 0 && dataCol < data.ColumnCount ? data.ColumnTitles[dataCol] : string.Empty;
        string row = dataRow >= 0 && dataRow < data.RowCount ? data.RowTitles[dataRow] : string.Empty;
        if (string.IsNullOrEmpty(row)) return col;
        if (string.IsNullOrEmpty(col)) return row;
        return $"{col}  •  {row}";
    }
}
