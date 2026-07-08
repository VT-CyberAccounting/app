using System.Collections.Generic;
using Google.GenAI.Types;
using Oculus.Interaction;
using UnityEngine;

public sealed class SliceSheet : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "SliceSheet",
        Description = "Cut a sheet piece for the user along the armed slice axis, as if they clicked the cut line " +
                      "on the sheet. Needs the Slice tool selected and a cut axis armed with SetSliceAxis. With the " +
                      "columns axis, after=N cuts between column N and N+1; with the rows axis, between row N and " +
                      "N+1. Slicing renumbers piece ids, so call GetSheetInfo before using ids again.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "after", new Schema { Type = Type.Integer,
                    Description = "Cut after this column/row of the target piece (per the armed axis)." } },
                { "sheet", new Schema { Type = Type.Integer, Description = "Target piece." } }
            },
            Required = new List<string> { "after" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        if (RequireToolSelected(ToolType.Slice, result)) return;

        var slice = Scene.Slice;
        var gen = Scene.Generator;
        if (slice == null || gen == null) { result["error"] = "Slice tool or sheet not found in scene."; return; }

        string axis = slice.CurrentAxisName;
        if (axis == "none") {
            Refuse(result, "armed slice axis",
                "The Slice tool has no cut axis armed. Arm it with SetSliceAxis(axis: 'columns' or 'rows').");
            return;
        }

        if (!TryResolveTargetBounds(args, result, out int rowMin, out int rowMax, out int colMin, out int colMax, out int pieceId)) return;

        TryGet(args, "after", out var afterArg);
        int after = AsInt(afterArg);

        bool columns = axis == "columns";
        int span = columns ? colMax - colMin + 1 : rowMax - rowMin + 1;
        if (span < 2) {
            result["error"] = $"That piece has only one {(columns ? "column" : "row")}; it cannot be sliced along that axis.";
            return;
        }
        if (after < 1 || after > span - 1) {
            result["error"] = $"'after' must be between 1 and {span - 1} for that piece along the {axis} axis.";
            return;
        }

        int vrA, vcA, vrB, vcB;
        if (columns) {
            int midRow = (rowMin + rowMax) / 2;
            vrA = midRow; vcA = colMin + after - 1;
            vrB = midRow; vcB = vcA + 1;
        }
        else {
            int midCol = (colMin + colMax) / 2;
            vcA = midCol; vrA = rowMin + after - 1;
            vcB = midCol; vrB = vrA + 1;
        }

        if (!gen.VisibleCellToWorld(vrA, vcA, out Vector3 a) ||
            !gen.VisibleCellToWorld(vrB, vcB, out Vector3 b)) {
            result["error"] = "Could not locate the cut line; is a sheet shown?";
            return;
        }

        var mgr = Scene.Sheets;
        int piecesBefore = mgr != null && mgr.IsBaked ? mgr.Sheets.Count : 1;

        Vector3 mid = (a + b) * 0.5f;
        if (!gen.PublishSyntheticPointer(PointerEventType.Select, mid)) {
            result["error"] = "The sheet has no pointer target.";
            return;
        }
        gen.PublishSyntheticPointer(PointerEventType.Unhover, mid);

        int piecesAfter = mgr != null && mgr.IsBaked ? mgr.Sheets.Count : 1;
        if (piecesAfter <= piecesBefore) {
            result["error"] = "The cut could not be made there.";
            return;
        }

        result["sliced"] = axis;
        result["after"] = after;
        if (pieceId > 0) result["sheet"] = pieceId;
        result["pieceCount"] = piecesAfter;
        result["note"] = "Piece ids were renumbered; call GetSheetInfo before using piece ids again.";
    }
}
