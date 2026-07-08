using System.Collections.Generic;
using System.Linq;
using Google.GenAI.Types;

public sealed class SetColor : SceneTool {

    public override FunctionDeclaration Declaration {
        get {
            var color = Scene.Color;
            var names = color != null
                ? color.PaletteNames.ToList()
                : new List<string> { "White", "Red", "Orange", "Yellow", "Green", "Blue", "Indigo", "Violet", "Black" };
            return new FunctionDeclaration {
                Name = "SetColor",
                Description = "Choose the paint color for the Color tool by name. The user then points at the " +
                              "sheet to paint, or you paint a piece with PaintSheet.",
                Parameters = new Schema {
                    Type = Type.Object,
                    Properties = new Dictionary<string, Schema> {
                        { "color", new Schema { Type = Type.String, Enum = names } }
                    },
                    Required = new List<string> { "color" }
                }
            };
        }
    }

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        var color = Scene.Color;
        if (color == null) { result["error"] = "Color tool not found in scene."; return; }

        if (RequireToolPanelOpen(result, "the color swatches")) return;
        if (RequireToolSelected(ToolType.Color, result)) return;

        TryGet(args, "color", out var colorArg);
        string name = AsString(colorArg);

        if (!color.SelectColorByName(name)) {
            result["error"] = $"Unknown color '{name}'. Available: {string.Join(", ", color.PaletteNames)}.";
            return;
        }

        result["color"] = color.CurrentColorName;
    }
}
