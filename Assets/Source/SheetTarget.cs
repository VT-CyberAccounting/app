using UnityEngine;

public struct SheetTarget
{
    public enum Kind { None, Cell, Sheet }

    public Kind kind;
    public Vector3 worldPoint;

    public int dataRow;
    public int dataCol;

    public int visRowMin;
    public int visRowMax;
    public int visColMin;
    public int visColMax;

    public static SheetTarget Cell(int dataRow, int dataCol, Vector3 world) => new SheetTarget
    {
        kind = Kind.Cell,
        dataRow = dataRow,
        dataCol = dataCol,
        worldPoint = world
    };

    public static SheetTarget Sheet(int rMin, int rMax, int cMin, int cMax, Vector3 world) => new SheetTarget
    {
        kind = Kind.Sheet,
        visRowMin = rMin,
        visRowMax = rMax,
        visColMin = cMin,
        visColMax = cMax,
        worldPoint = world
    };

    public int CellCount
    {
        get
        {
            if (kind == Kind.Cell) return 1;
            if (kind == Kind.Sheet) return (visRowMax - visRowMin + 1) * (visColMax - visColMin + 1);
            return 0;
        }
    }
}

public struct SheetStatsResult
{
    public bool valid;
    public int count;
    public float min;
    public float max;
    public float average;
    public float sum;
    public float normalizedAverage;

    public int maxRow;
    public int maxCol;
    public int minRow;
    public int minCol;
}

public static class SheetStats
{
    public static SheetStatsResult Compute(SheetGenerator sheet, DataSource data, SheetTarget target)
    {
        SheetStatsResult result = default;
        if (sheet == null || data == null || target.kind != SheetTarget.Kind.Sheet) return result;

        float min = float.MaxValue;
        float max = float.MinValue;
        int minRow = -1, minCol = -1, maxRow = -1, maxCol = -1;
        double sum = 0.0;
        int count = 0;

        for (int vr = target.visRowMin; vr <= target.visRowMax; vr++)
        {
            int dataRow = sheet.VisibleRowToData(vr);
            if (dataRow < 0) continue;

            for (int vc = target.visColMin; vc <= target.visColMax; vc++)
            {
                int dataCol = sheet.VisibleColToData(vc);
                if (dataCol < 0) continue;

                float v = data.GetValue(dataRow, dataCol);
                if (v < min) { min = v; minRow = dataRow; minCol = dataCol; }
                if (v > max) { max = v; maxRow = dataRow; maxCol = dataCol; }
                sum += v;
                count++;
            }
        }

        if (count == 0) return result;

        result.valid = true;
        result.count = count;
        result.min = min;
        result.max = max;
        result.minRow = minRow;
        result.minCol = minCol;
        result.maxRow = maxRow;
        result.maxCol = maxCol;
        result.average = (float)(sum / count);
        result.sum = (float)sum;

        float range = data.GlobalMax - data.GlobalMin;
        result.normalizedAverage = range > 0f ? (result.average - data.GlobalMin) / range : 0f;
        return result;
    }
}
