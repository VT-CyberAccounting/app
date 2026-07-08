using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class ResetFilters : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "ResetFilters",
        Description = "Restore all rows and columns to visible and clear any sorting (the data panel's Reset All).",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "dataset", new Schema { Type = Type.String,
                    Description = "Dataset the user named, if any." } }
            }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        if (SheetLocked(result)) return;
        if (RequireActiveDataset(args, result)) return;
        if (RequireDatasetExpanded(result)) return;

        var sheet = Scene.Sheet;
        if (sheet == null) { result["error"] = "Sheet not found in scene."; return; }

        sheet.ResetAllFilters();
        string dataset = ActiveDatasetLabel();
        if (dataset != null) result["dataset"] = dataset;
    }
}
