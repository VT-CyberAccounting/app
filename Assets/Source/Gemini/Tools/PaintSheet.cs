using System.Collections.Generic;
using Google.GenAI.Types;
using Oculus.Interaction;
using UnityEngine;

public sealed class PaintSheet : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "PaintSheet",
        Description = "Paint a sheet piece with the Color tool's chosen color for the user, as if they clicked the " +
                      "piece. Needs the Color tool selected and a color chosen with SetColor. Color applies to " +
                      "whole pieces: to paint a single column or row, slice it into its own piece first.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "sheet", new Schema { Type = Type.Integer, Description = "Target piece." } }
            }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        if (RequireToolSelected(ToolType.Color, result)) return;

        var color = Scene.Color;
        var gen = Scene.Generator;
        if (color == null || gen == null) { result["error"] = "Color tool or sheet not found in scene."; return; }

        if (color.CurrentColorName == "none") {
            Refuse(result, "chosen color",
                "No paint color is chosen. Choose one with SetColor first.");
            return;
        }

        if (!TryResolveTargetBounds(args, result, out int rowMin, out int rowMax, out int colMin, out int colMax, out int pieceId)) return;

        int midRow = (rowMin + rowMax) / 2;
        int midCol = (colMin + colMax) / 2;
        if (!gen.VisibleCellToWorld(midRow, midCol, out Vector3 world)) {
            result["error"] = "Could not locate that piece; is a sheet shown?";
            return;
        }

        var mgr = Scene.Sheets;
        int before = mgr != null ? mgr.ColorOverrideCount : 0;
        if (!gen.PublishSyntheticPointer(PointerEventType.Select, world)) {
            result["error"] = "The sheet has no pointer target.";
            return;
        }

        int afterCount = mgr != null ? mgr.ColorOverrideCount : 0;
        if (afterCount <= before) {
            result["error"] = "The piece could not be painted.";
            return;
        }

        result["painted"] = color.CurrentColorName;
        if (pieceId > 0) result["sheet"] = pieceId;
    }
}
