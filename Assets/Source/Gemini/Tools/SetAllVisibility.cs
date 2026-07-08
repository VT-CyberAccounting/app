using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class SetAllVisibility : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "SetAllVisibility",
        Description = "Show or hide all rows or all columns at once (select all / deselect all).",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "axis", new Schema { Type = Type.String, Enum = new List<string> { "rows", "columns" } } },
                { "visible", new Schema { Type = Type.Boolean,
                    Description = "True to show all, false to hide all." } },
                { "dataset", new Schema { Type = Type.String,
                    Description = "Dataset the user named, if any." } }
            },
            Required = new List<string> { "axis", "visible" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        if (SheetLocked(result)) return;
        if (RequireActiveDataset(args, result)) return;
        if (RequireDatasetExpanded(result)) return;

        var sheet = Scene.Sheet;
        if (sheet == null) { result["error"] = "Sheet not found in scene."; return; }

        TryGet(args, "axis", out var axisArg);
        TryGet(args, "visible", out var visibleArg);
        string axis = AsString(axisArg)?.Trim().ToLowerInvariant();
        bool visible = AsBool(visibleArg);

        if (axis == "rows") sheet.SetAllRowsVisible(visible);
        else if (axis == "columns") sheet.SetAllColumnsVisible(visible);
        else { result["error"] = $"Unknown axis '{axis}'. Use 'rows' or 'columns'."; return; }

        result["axis"] = axis;
        result["visible"] = visible;
        string dataset = ActiveDatasetLabel();
        if (dataset != null) result["dataset"] = dataset;
    }
}
