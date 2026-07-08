using System.Collections.Generic;
using Google.GenAI.Types;
using Oculus.Interaction;
using UnityEngine;

public sealed class Inspect : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "Inspect",
        Description = "Point the Inspect tool at a column, row, or cell for the user and show its read-out, without " +
                      "the user pointing. Needs the Inspect tool selected with the matching mode (Inspect Columns, " +
                      "Inspect Rows, or cell); when the user explicitly asked for the inspection, set that up " +
                      "yourself and retry, otherwise offer to.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "mode", new Schema { Type = Type.String, Enum = new List<string> { "column", "row", "cell" } } },
                { "column", new Schema { Type = Type.Integer,
                    Description = "Column position within the target piece (required for 'column' and 'cell')." } },
                { "row", new Schema { Type = Type.Integer,
                    Description = "Row position within the target piece (required for 'row' and 'cell')." } },
                { "sheet", new Schema { Type = Type.Integer, Description = "Target piece." } }
            },
            Required = new List<string> { "mode" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        TryGet(args, "mode", out var modeArg);
        string mode = AsString(modeArg)?.Trim().ToLowerInvariant();
        string need = mode == "column" ? "columns" : mode == "row" ? "rows" : mode == "cell" ? "cell" : null;
        if (need == null) { result["error"] = $"Unknown mode '{AsString(modeArg)}'. Use column, row, or cell."; return; }

        if (RequireToolSelected(ToolType.Inspect, result)) return;

        var inspect = Scene.Inspect;
        if (inspect == null) { result["error"] = "Inspect tool not found in scene."; return; }
        if (inspect.CurrentModeName != need) {
            Refuse(result, need == "cell" ? "Inspect cell mode" : $"Inspect {need} mode",
                $"The Inspect tool is not in {(need == "cell" ? "cell" : need)} mode. " +
                $"Set it with SetInspectMode(mode: '{need}').");
            return;
        }

        var gen = Scene.Generator;
        if (gen == null) { result["error"] = "No sheet in scene."; return; }

        var sheetsMgr = Scene.Sheets;
        bool baked = sheetsMgr != null && sheetsMgr.IsBaked;
        SheetManager.Sheet piece = null;
        int pieceId = 0;
        if (baked) {
            var list = sheetsMgr.Sheets;
            if (TryGet(args, "sheet", out var sheetArg)) {
                pieceId = AsInt(sheetArg);
                int idx = pieceId - 1;
                if (idx < 0 || idx >= list.Count) { result["error"] = $"There is no sheet piece #{pieceId}."; return; }
                piece = list[idx];
            }
            else if (list.Count == 1) { piece = list[0]; pieceId = 1; }
            else {
                result["needsSheet"] = true;
                result["message"] = "The sheet is sliced into several pieces; ask the user which one.";
                result["sheets"] = SummarizePieces(list);
                return;
            }
        }

        int colMin = piece != null ? piece.colMin : 0;
        int colMax = piece != null ? piece.colMax : gen.VisibleColCount - 1;
        int rowMin = piece != null ? piece.rowMin : 0;
        int rowMax = piece != null ? piece.rowMax : gen.RowCount - 1;

        int vr, vc;
        if (mode == "column") {
            if (!TryGet(args, "column", out var cArg)) { result["error"] = "column is required for column mode."; return; }
            vc = colMin + (AsInt(cArg) - 1);
            if (vc < colMin || vc > colMax) { result["error"] = $"Column {AsInt(cArg)} is out of range for that sheet."; return; }
            vr = rowMin;
        }
        else if (mode == "row") {
            if (!TryGet(args, "row", out var rArg)) { result["error"] = "row is required for row mode."; return; }
            vr = rowMin + (AsInt(rArg) - 1);
            if (vr < rowMin || vr > rowMax) { result["error"] = $"Row {AsInt(rArg)} is out of range for that sheet."; return; }
            vc = colMin;
        }
        else {
            if (!TryGet(args, "column", out var cArg2) || !TryGet(args, "row", out var rArg2)) {
                result["error"] = "row and column are required for cell mode."; return;
            }
            vc = colMin + (AsInt(cArg2) - 1);
            vr = rowMin + (AsInt(rArg2) - 1);
            if (vc < colMin || vc > colMax || vr < rowMin || vr > rowMax) { result["error"] = "That cell is out of range for that sheet."; return; }
        }

        if (!gen.VisibleCellToWorld(vr, vc, out Vector3 world)) { result["error"] = "Could not locate that cell; is a sheet shown?"; return; }
        if (!gen.PublishSyntheticPointer(PointerEventType.Hover, world)) { result["error"] = "The sheet has no pointer target."; return; }

        result["inspected"] = mode;
        if (mode != "row") result["column"] = vc - colMin + 1;
        if (mode != "column") result["row"] = vr - rowMin + 1;
        if (piece != null) result["sheet"] = pieceId;
    }

    private static List<object> SummarizePieces(IReadOnlyList<SheetManager.Sheet> list) {
        var pieces = new List<object>();
        for (int i = 0; i < list.Count; i++) {
            var s = list[i];
            pieces.Add(new Dictionary<string, object> {
                { "id", i + 1 },
                { "cellCount", (s.rowMax - s.rowMin + 1) * (s.colMax - s.colMin + 1) }
            });
        }
        return pieces;
    }
}
