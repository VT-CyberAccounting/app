using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class SetCompareMode : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "SetCompareMode",
        Description = "Set what the Compare tool collects: individual cells ('cells') or whole sheets ('sheets'). " +
                      "The user pinches a cell or pinch-drags a sheet to add it, or you pin one with AddCompareEntry.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "mode", new Schema { Type = Type.String, Enum = new List<string> { "cells", "sheets" } } }
            },
            Required = new List<string> { "mode" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        var compare = Scene.Compare;
        if (compare == null) { result["error"] = "Compare tool not found in scene."; return; }

        if (RequireToolPanelOpen(result, "the Compare section buttons")) return;
        if (RequireToolSelected(ToolType.Compare, result)) return;

        TryGet(args, "mode", out var modeArg);
        string mode = AsString(modeArg)?.Trim().ToLowerInvariant();

        if (!compare.SetCompareMode(mode)) {
            result["error"] = $"Unknown mode '{mode}'. Use 'cells' or 'sheets'.";
            return;
        }

        result["mode"] = compare.CurrentModeName;
    }
}
