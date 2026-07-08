using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class SetSliceAxis : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "SetSliceAxis",
        Description = "Arm the Slice tool's cut axis: 'columns' for a vertical cut between columns, 'rows' for a " +
                      "horizontal cut between rows, or 'none' to disarm it. The user then points at the sheet to " +
                      "cut, or you cut with SliceSheet.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "axis", new Schema { Type = Type.String, Enum = new List<string> { "columns", "rows", "none" } } }
            },
            Required = new List<string> { "axis" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        var slice = Scene.Slice;
        if (slice == null) { result["error"] = "Slice tool not found in scene."; return; }

        if (RequireToolPanelOpen(result, "the Slice axis buttons")) return;
        if (RequireToolSelected(ToolType.Slice, result)) return;

        TryGet(args, "axis", out var axisArg);
        string axis = AsString(axisArg)?.Trim().ToLowerInvariant();

        if (!slice.SetAxis(axis)) {
            result["error"] = $"Unknown axis '{axis}'. Use 'columns' or 'rows'.";
            return;
        }

        result["axis"] = slice.CurrentAxisName;
    }
}
