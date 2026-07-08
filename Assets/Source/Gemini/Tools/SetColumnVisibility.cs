using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class SetColumnVisibility : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "SetColumnVisibility",
        Description = "Show or hide specific columns by number. The result echoes the affected column titles — " +
                      "check they match what the user asked for.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "columns", new Schema { Type = Type.Array, Items = new Schema { Type = Type.Integer },
                    Description = "Column numbers to affect." } },
                { "visible", new Schema { Type = Type.Boolean,
                    Description = "True to show, false to hide." } },
                { "dataset", new Schema { Type = Type.String,
                    Description = "Dataset the user named, if any." } }
            },
            Required = new List<string> { "columns", "visible" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        ApplyVisibility(args, result, "columns", true);
    }
}
