using System;
using System.Collections.Generic;
using Google.GenAI.Types;
using Type = Google.GenAI.Types.Type;

public sealed class GetSheetData : SceneTool {

    private const int MaxCells = 600;

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "GetSheetData",
        Description = "Read the actual cell values of the sheet the user is looking at, with a per-column summary " +
                      "(count, min, max, mean, sum). Use this to describe or analyze the data itself. " +
                      "By default it covers the currently visible rows and columns.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "columns", new Schema { Type = Type.Array, Items = new Schema { Type = Type.Integer },
                    Description = "Column numbers to read. Omit to use all visible columns." } },
                { "rows", new Schema { Type = Type.Array, Items = new Schema { Type = Type.Integer },
                    Description = "Row numbers to read. Omit to use all visible rows." } },
                { "includeValues", new Schema { Type = Type.Boolean,
                    Description = "True to include individual cell values, not just the summary." } }
            }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        var data = Scene.Data;
        if (data == null) { result["error"] = "No data source found in scene."; return; }

        TryGet(args, "columns", out var colsArg);
        TryGet(args, "rows", out var rowsArg);
        TryGet(args, "includeValues", out var includeArg);
        bool includeValues = includeArg != null && AsBool(includeArg);

        var cols = Resolve(colsArg, data.ColumnCount, data.IsColumnVisible);
        var rows = Resolve(rowsArg, data.RowCount, data.IsRowVisible);

        string dataset = ActiveDatasetLabel();
        if (dataset != null) result["dataset"] = dataset;

        var columns = new List<object>();
        foreach (int c in cols) {
            double min = 0, max = 0, sum = 0;
            bool any = false;
            foreach (int r in rows) {
                float v = data.GetValue(r, c);
                if (!any) { min = max = v; any = true; }
                else { if (v < min) min = v; if (v > max) max = v; }
                sum += v;
            }
            columns.Add(new Dictionary<string, object> {
                { "number", c + 1 },
                { "title", c < data.ColumnTitles.Count ? data.ColumnTitles[c] : "" },
                { "count", rows.Count },
                { "min", any ? (object)Round(min) : null },
                { "max", any ? (object)Round(max) : null },
                { "mean", any ? (object)Round(sum / rows.Count) : null },
                { "sum", Round(sum) }
            });
        }

        result["columnCount"] = cols.Count;
        result["rowCount"] = rows.Count;
        result["columns"] = columns;

        if (!includeValues) return;

        int cellBudget = MaxCells;
        var rowData = new List<object>();
        bool truncated = false;
        foreach (int r in rows) {
            if (cols.Count > 0 && cellBudget < cols.Count) { truncated = true; break; }
            var values = new List<object>();
            foreach (int c in cols) values.Add(Round(data.GetValue(r, c)));
            cellBudget -= cols.Count;
            rowData.Add(new Dictionary<string, object> {
                { "number", r + 1 },
                { "title", r < data.RowTitles.Count ? data.RowTitles[r] : "" },
                { "values", values }
            });
        }

        result["rows"] = rowData;
        if (truncated) result["truncated"] = true;
    }

    private static List<int> Resolve(object arg, int count, Func<int, bool> isVisible) {
        var indices = new List<int>();
        if (arg != null) {
            foreach (int n in AsIntList(arg)) {
                int idx = n - 1;
                if (idx >= 0 && idx < count) indices.Add(idx);
            }
        }
        else {
            for (int i = 0; i < count; i++)
                if (isVisible(i)) indices.Add(i);
        }
        return indices;
    }

    private static double Round(double v) => Math.Round(v, 4);
}
