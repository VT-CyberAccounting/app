using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class SetInspectMode : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "SetInspectMode",
        Description = "Set what the Inspect tool reads when the user points at the sheet: a single cell or sheet " +
                      "('cell'), a whole column ('columns'), or a whole row ('rows').",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "mode", new Schema { Type = Type.String, Enum = new List<string> { "cell", "columns", "rows" } } }
            },
            Required = new List<string> { "mode" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        var inspect = Scene.Inspect;
        if (inspect == null) { result["error"] = "Inspect tool not found in scene."; return; }

        if (RequireToolPanelOpen(result, "the Inspect mode buttons")) return;
        if (RequireToolSelected(ToolType.Inspect, result)) return;

        TryGet(args, "mode", out var modeArg);
        string mode = AsString(modeArg)?.Trim().ToLowerInvariant();

        if (!inspect.SetInspectMode(mode)) {
            result["error"] = $"Unknown mode '{mode}'. Use 'cell', 'columns', or 'rows'.";
            return;
        }

        result["mode"] = inspect.CurrentModeName;
    }
}
