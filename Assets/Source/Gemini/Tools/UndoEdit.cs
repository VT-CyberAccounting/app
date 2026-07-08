using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class UndoEdit : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "UndoEdit",
        Description = "Undo the most recent edit (the tool panel's Undo button). All edits — slices, moved pieces, " +
                      "colors, and compare pins — share one timeline, and undo always removes the newest first; call " +
                      "repeatedly to step further back. Pass 'tool' only to assert what you expect to undo: if the " +
                      "newest edit came from a different tool, this refuses and tells you what would be undone " +
                      "instead, so you can confirm with the user. The result's 'remaining' is the number of edits " +
                      "left; 'nextUndo' is what the next undo would remove.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "tool", new Schema { Type = Type.String,
                    Enum = new List<string> { "compare", "slice", "color", "grab" },
                    Description = "Optional: the tool whose edit you expect to be newest." } }
            }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        var controller = Scene.Tools;
        if (controller == null) { result["error"] = "Tool controller not found in scene."; return; }

        var top = controller.Journal.Peek();
        if (top == null) {
            result["undone"] = "nothing";
            result["note"] = "There are no edits to undo.";
            return;
        }

        if (TryGet(args, "tool", out var toolArg)) {
            string requested = AsString(toolArg)?.Trim().ToLowerInvariant();
            EditJournal.Kind? expected = requested switch {
                "compare" => EditJournal.Kind.Pin,
                "slice" => EditJournal.Kind.Slice,
                "color" => EditJournal.Kind.Color,
                "grab" => EditJournal.Kind.Move,
                _ => null
            };
            if (expected == null) {
                result["error"] = $"Unknown tool '{AsString(toolArg)}'. Use compare, slice, color, or grab.";
                return;
            }
            if (top.kind != expected.Value) {
                Refuse(result, "matching newest edit",
                    $"The newest edit is a {EditJournal.KindName(top.kind)}, not a {requested} edit. Undo always " +
                    "removes the newest edit first; confirm with the user, then call again without 'tool' to step " +
                    "back through the newer edits.");
                result["nextUndo"] = EditJournal.KindName(top.kind);
                result["remaining"] = controller.Journal.Count;
                return;
            }
        }

        controller.UndoLast();
        result["undone"] = EditJournal.KindName(top.kind);
        result["remaining"] = controller.Journal.Count;
        var next = controller.Journal.Peek();
        if (next != null) result["nextUndo"] = EditJournal.KindName(next.kind);
    }
}
