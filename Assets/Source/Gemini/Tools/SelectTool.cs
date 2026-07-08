using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class SelectTool : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "SelectTool",
        Description = "Select (arm) a tool in the tool panel, or clear the selection with 'none'. " +
                      "Needs the tool panel open (its buttons are only on screen then).",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "tool", new Schema { Type = Type.String,
                    Enum = new List<string> { "inspect", "compare", "slice", "color", "grab", "none" } } }
            },
            Required = new List<string> { "tool" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        TryGet(args, "tool", out var toolArg);
        if (!TryParseTool(AsString(toolArg), out var tool)) {
            result["error"] = $"Unknown tool '{AsString(toolArg)}'.";
            return;
        }

        var controller = Scene.Tools;
        if (controller == null) { result["error"] = "Tool controller not found in scene."; return; }

        if (tool == ToolType.None) controller.DeselectTool();
        else {
            if (RequireToolPanelOpen(result, "selecting a tool")) return;
            if (controller.SelectedTool != tool) controller.SelectTool(tool);
        }

        result["selected"] = controller.SelectedTool.ToString();
    }
}
