using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class SetDataTab : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "SetDataTab",
        Description = "Open the data panel's Columns or Rows section, or close both with 'none'. Needs the data " +
                      "panel open with a dataset showing. This only changes which list is shown; visibility tools " +
                      "work regardless of the open section.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "tab", new Schema { Type = Type.String, Enum = new List<string> { "columns", "rows", "none" } } }
            },
            Required = new List<string> { "tab" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        var panel = Scene.DataPanel;
        if (panel == null) { result["error"] = "Data panel not found in scene."; return; }

        if (!panel.IsVisible) {
            Refuse(result, "open data panel",
                "The data panel is closed, so its Columns/Rows tabs are not on screen. " +
                "Offer to open it with SetPanel.");
            return;
        }
        if (panel.IsCollapsed) {
            Refuse(result, "expanded dataset",
                "The open dataset is collapsed, so the tab list is hidden. " +
                "Offer to reopen it with SwitchDataset.");
            return;
        }

        TryGet(args, "tab", out var tabArg);
        string tab = AsString(tabArg)?.Trim().ToLowerInvariant();

        switch (tab) {
            case "column": case "columns": panel.ShowWindow(0); break;
            case "row": case "rows": panel.ShowWindow(1); break;
            case "none": panel.CloseWindows(); break;
            default: result["error"] = $"Unknown tab '{tab}'. Use 'columns', 'rows', or 'none'."; return;
        }

        int active = panel.ActiveWindow;
        result["tab"] = active == 1 ? "rows" : active == 0 ? "columns" : "none";
    }
}
