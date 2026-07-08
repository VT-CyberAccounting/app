using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class CloseTab : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "CloseTab",
        Description = "Close what is currently open, the same as the user tapping a tab they already have open. " +
                      "'tool' deselects the active tool in the tool panel. 'dataset' collapses the open dataset: it " +
                      "hides the Sheet and the data panel body (so no sheet is shown and tools have nothing to act " +
                      "on) but keeps the dataset loaded and switchable. This never deletes data.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "target", new Schema { Type = Type.String,
                    Enum = new List<string> { "tool", "dataset" } } }
            },
            Required = new List<string> { "target" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        TryGet(args, "target", out var arg);
        string target = AsString(arg)?.Trim().ToLowerInvariant();

        switch (target) {
            case "tool": CloseTool(result); break;
            case "dataset": CloseDataset(result); break;
            default: result["error"] = $"Unknown target '{AsString(arg)}'. Use 'tool' or 'dataset'."; break;
        }
    }

    private static void CloseTool(Dictionary<string, object> result) {
        var tools = Scene.Tools;
        if (tools == null) { result["error"] = "Tool controller not found in scene."; return; }

        result["closed"] = "tool";
        if (tools.SelectedTool == ToolType.None) {
            result["note"] = "No tool was selected.";
            return;
        }

        result["was"] = tools.SelectedTool.ToString();
        tools.DeselectTool();
        result["selected"] = tools.SelectedTool.ToString();
    }

    private static void CloseDataset(Dictionary<string, object> result) {
        var panel = Scene.DataPanel;
        if (panel == null) { result["error"] = "Data panel not found in scene."; return; }

        result["closed"] = "dataset";
        if (panel.IsCollapsed) {
            result["note"] = "The dataset was already collapsed.";
            return;
        }

        var datasets = Scene.Datasets;
        if (datasets != null && datasets.ActiveIndex >= 0 && datasets.ActiveIndex < datasets.TabCount)
            result["was"] = datasets.Tabs[datasets.ActiveIndex].label;

        panel.CollapseData();
    }
}
