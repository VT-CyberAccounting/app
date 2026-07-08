using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class SetPanel : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "SetPanel",
        Description = "Open, close, or toggle the data panel or the tool panel.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "panel", new Schema { Type = Type.String, Enum = new List<string> { "data", "tool" } } },
                { "state", new Schema { Type = Type.String, Enum = new List<string> { "open", "close", "toggle" } } }
            },
            Required = new List<string> { "panel", "state" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        TryGet(args, "panel", out var panelArg);
        TryGet(args, "state", out var stateArg);
        string panel = AsString(panelArg);
        string state = AsString(stateArg)?.Trim().ToLowerInvariant();

        bool isData = panel != null && panel.Trim().ToLowerInvariant() == "data";
        bool isVisible;

        if (isData) {
            var p = Scene.DataPanel;
            if (p == null) { result["error"] = "Data panel not found in scene."; return; }
            Apply(state, p.IsVisible, p.ShowPanel, p.HidePanel, p.TogglePanel);
            isVisible = p.IsVisible;
        }
        else {
            var p = Scene.ToolPanel;
            if (p == null) { result["error"] = "Tool panel not found in scene."; return; }
            Apply(state, p.IsVisible, p.ShowPanel, p.HidePanel, p.TogglePanel);
            isVisible = p.IsVisible;
        }

        result["visible"] = isVisible;
    }

    private static void Apply(string state, bool visible, System.Action show, System.Action hide, System.Action toggle) {
        switch (state) {
            case "open": if (!visible) show(); break;
            case "close": if (visible) hide(); break;
            default: toggle(); break;
        }
    }
}
