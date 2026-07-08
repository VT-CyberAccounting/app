using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class SortSheet : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "SortSheet",
        Description = "Sort the rows or columns of the sheet ascending, descending, or back to original order.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "axis", new Schema { Type = Type.String, Enum = new List<string> { "rows", "columns" } } },
                { "mode", new Schema { Type = Type.String,
                    Enum = new List<string> { "original", "ascending", "descending" },
                    Description = "'original' restores the unsorted order." } },
                { "dataset", new Schema { Type = Type.String,
                    Description = "Dataset the user named, if any." } }
            },
            Required = new List<string> { "axis", "mode" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        if (SheetLocked(result)) return;
        if (RequireActiveDataset(args, result)) return;
        if (RequireDatasetExpanded(result)) return;

        var sheet = Scene.Sheet;
        if (sheet == null) { result["error"] = "Sheet not found in scene."; return; }

        TryGet(args, "axis", out var axisArg);
        TryGet(args, "mode", out var modeArg);
        string axis = AsString(axisArg)?.Trim().ToLowerInvariant();
        string modeStr = AsString(modeArg)?.Trim().ToLowerInvariant();

        DataSource.SortMode mode;
        switch (modeStr) {
            case "ascending": mode = DataSource.SortMode.Ascending; break;
            case "descending": mode = DataSource.SortMode.Descending; break;
            case "original": mode = DataSource.SortMode.Original; break;
            default: result["error"] = $"Unknown sort mode '{modeStr}'."; return;
        }

        if (axis == "rows") sheet.SortRows(mode);
        else if (axis == "columns") sheet.SortColumns(mode);
        else { result["error"] = $"Unknown axis '{axis}'. Use 'rows' or 'columns'."; return; }

        result["axis"] = axis;
        result["mode"] = modeStr;
        string dataset = ActiveDatasetLabel();
        if (dataset != null) result["dataset"] = dataset;
    }
}
