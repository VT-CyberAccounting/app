using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class SetRowVisibility : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "SetRowVisibility",
        Description = "Show or hide specific rows by number. The result echoes the affected row titles — " +
                      "check they match what the user asked for.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "rows", new Schema { Type = Type.Array, Items = new Schema { Type = Type.Integer },
                    Description = "Row numbers to affect." } },
                { "visible", new Schema { Type = Type.Boolean,
                    Description = "True to show, false to hide." } },
                { "dataset", new Schema { Type = Type.String,
                    Description = "Dataset the user named, if any." } }
            },
            Required = new List<string> { "rows", "visible" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        ApplyVisibility(args, result, "rows", false);
    }
}
