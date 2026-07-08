using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class UndoAllEdits : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "UndoAllEdits",
        Description = "Undo every edit made with the tools (all slices, colors, and moved pieces) and clear the tool " +
                      "selection (the tool panel's Undo All button). This is destructive; confirm with the user first. " +
                      "Note: this does not touch row/column visibility or sorting; that is ResetFilters."
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        var controller = Scene.Tools;
        if (controller == null) { result["error"] = "Tool controller not found in scene."; return; }

        controller.ResetAll();
        controller.DeselectTool();
        result["undone"] = "all";
    }
}
