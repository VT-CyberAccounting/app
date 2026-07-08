using System.Collections.Generic;
using Google.GenAI.Types;
using Oculus.Interaction;
using UnityEngine;

public sealed class AddCompareEntry : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "AddCompareEntry",
        Description = "Pin a cell or a sheet piece into the Compare tool's ledger for the user, as if they pinched " +
                      "it on the sheet. Needs the Compare tool selected with the matching section open (Compare " +
                      "Cells for a cell, Compare Sheets for a piece). The result echoes the pinned entry so you can " +
                      "report its numbers.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "target", new Schema { Type = Type.String, Enum = new List<string> { "cell", "sheet" } } },
                { "column", new Schema { Type = Type.Integer,
                    Description = "Column within the target piece (required for 'cell')." } },
                { "row", new Schema { Type = Type.Integer,
                    Description = "Row within the target piece (required for 'cell')." } },
                { "sheet", new Schema { Type = Type.Integer, Description = "Target piece." } }
            },
            Required = new List<string> { "target" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        if (RequireToolSelected(ToolType.Compare, result)) return;

        var compare = Scene.Compare;
        var gen = Scene.Generator;
        if (compare == null || gen == null) { result["error"] = "Compare tool or sheet not found in scene."; return; }

        TryGet(args, "target", out var targetArg);
        string target = AsString(targetArg)?.Trim().ToLowerInvariant();
        if (target != "cell" && target != "sheet") {
            result["error"] = $"Unknown target '{AsString(targetArg)}'. Use 'cell' or 'sheet'.";
            return;
        }

        string neededMode = target == "cell" ? "cells" : "sheets";
        if (compare.CurrentModeName != neededMode) {
            Refuse(result, $"Compare {neededMode} section",
                $"The Compare tool's {neededMode} section is not open. Open it with SetCompareMode(mode: '{neededMode}').");
            return;
        }

        if (!TryResolveTargetBounds(args, result, out int rowMin, out int rowMax, out int colMin, out int colMax, out int pieceId)) return;

        if (target == "cell") PinCell(compare, gen, args, result, rowMin, rowMax, colMin, colMax);
        else PinSheet(compare, gen, result, rowMin, rowMax, colMin, colMax, pieceId);
    }

    private static void PinCell(CompareTool compare, SheetGenerator gen,
        Dictionary<string, object> args, Dictionary<string, object> result,
        int rowMin, int rowMax, int colMin, int colMax) {
        if (!TryGet(args, "column", out var cArg) || !TryGet(args, "row", out var rArg)) {
            result["error"] = "row and column are required for a cell entry.";
            return;
        }

        int vc = colMin + (AsInt(cArg) - 1);
        int vr = rowMin + (AsInt(rArg) - 1);
        if (vc < colMin || vc > colMax || vr < rowMin || vr > rowMax) {
            result["error"] = "That cell is out of range for that sheet.";
            return;
        }

        if (!gen.VisibleCellToWorld(vr, vc, out Vector3 world)) {
            result["error"] = "Could not locate that cell; is a sheet shown?";
            return;
        }

        var topBefore = compare.CellEntryCount > 0 ? compare.CellEntries[0] : null;
        if (!gen.PublishSyntheticPointer(PointerEventType.Select, world) ||
            !gen.PublishSyntheticPointer(PointerEventType.Unselect, world)) {
            result["error"] = "The sheet has no pointer target.";
            return;
        }
        gen.PublishSyntheticPointer(PointerEventType.Cancel, world);

        var topAfter = compare.CellEntryCount > 0 ? compare.CellEntries[0] : null;
        if (topAfter == null || ReferenceEquals(topAfter, topBefore)) {
            result["error"] = "The cell could not be pinned.";
            return;
        }

        result["pinned"] = "cell";
        result["column"] = topAfter.ColumnTitle;
        result["row"] = topAfter.RowTitle;
        result["value"] = System.Math.Round(topAfter.Value, 4);
        result["entryCount"] = compare.CellEntryCount;
    }

    private static void PinSheet(CompareTool compare, SheetGenerator gen, Dictionary<string, object> result,
        int rowMin, int rowMax, int colMin, int colMax, int pieceId) {
        if (rowMin == rowMax && colMin == colMax) {
            result["error"] = "That piece is a single cell; pin it as a cell in the Compare Cells section instead.";
            return;
        }

        if (!gen.VisibleCellToWorld(rowMin, colMin, out Vector3 worldA) ||
            !gen.VisibleCellToWorld(rowMax, colMax, out Vector3 worldB)) {
            result["error"] = "Could not locate that piece; is a sheet shown?";
            return;
        }

        var topBefore = compare.SheetEntryCount > 0 ? compare.SheetLedgerEntries[0] : null;
        if (!gen.PublishSyntheticPointer(PointerEventType.Select, worldA)) {
            result["error"] = "The sheet has no pointer target.";
            return;
        }
        gen.PublishSyntheticPointer(PointerEventType.Move, worldB);
        gen.PublishSyntheticPointer(PointerEventType.Unselect, worldB);
        gen.PublishSyntheticPointer(PointerEventType.Cancel, worldB);

        var topAfter = compare.SheetEntryCount > 0 ? compare.SheetLedgerEntries[0] : null;
        if (topAfter == null || ReferenceEquals(topAfter, topBefore)) {
            result["error"] = "The piece could not be pinned.";
            return;
        }

        result["pinned"] = "sheet";
        if (pieceId > 0) result["sheet"] = pieceId;
        result["cellCount"] = topAfter.CellCount;
        result["mean"] = System.Math.Round(topAfter.Mean, 4);
        result["sum"] = System.Math.Round(topAfter.Sum, 4);
        result["entryCount"] = compare.SheetEntryCount;
    }
}
