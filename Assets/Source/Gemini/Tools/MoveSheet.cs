using System.Collections.Generic;
using Google.GenAI.Types;
using UnityEngine;
using Type = Google.GenAI.Types.Type;

public sealed class MoveSheet : SceneTool {

    private const float DefaultDistance = 0.15f;
    private const float MinDistance = 0.02f;
    private const float MaxDistance = 1f;

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "MoveSheet",
        Description = "Move a sheet piece a short distance for the user, as if they grabbed and slid it. Needs the " +
                      "Grab tool selected. Movement respects collisions with other pieces, so the piece may travel " +
                      "less than asked (the result reports the actual distance).",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "direction", new Schema { Type = Type.String,
                    Enum = new List<string> { "left", "right", "forward", "back", "up", "down" },
                    Description = "From the user's point of view; 'forward' is away from them." } },
                { "distance", new Schema { Type = Type.Number,
                    Description = "Meters (default 0.15, clamped to 0.02-1)." } },
                { "sheet", new Schema { Type = Type.Integer, Description = "Target piece." } }
            },
            Required = new List<string> { "direction" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        if (RequireToolSelected(ToolType.Grab, result)) return;

        var mgr = Scene.Sheets;
        var gen = Scene.Generator;
        var grab = Scene.Grab;
        if (mgr == null || gen == null) { result["error"] = "Sheet not found in scene."; return; }
        if (!mgr.IsBaked) { result["error"] = "The sheet is not ready to move; is a sheet shown?"; return; }

        if (!TryResolveTargetBounds(args, result, out _, out _, out _, out _, out int pieceId)) return;
        if (pieceId < 1 || pieceId > mgr.Sheets.Count) { result["error"] = "Could not resolve that piece."; return; }
        var sheet = mgr.Sheets[pieceId - 1];
        if (sheet.piece == null) { result["error"] = "That piece cannot be moved right now."; return; }

        TryGet(args, "direction", out var dirArg);
        string direction = AsString(dirArg)?.Trim().ToLowerInvariant();
        if (!TryWorldDirection(direction, out Vector3 worldDir)) {
            result["error"] = $"Unknown direction '{AsString(dirArg)}'. Use left, right, forward, back, up, or down.";
            return;
        }

        float distance = DefaultDistance;
        if (TryGet(args, "distance", out var distArg))
            distance = Mathf.Clamp(AsFloat(distArg), MinDistance, MaxDistance);

        Transform pieceT = sheet.piece.transform;
        Vector3 prePos = pieceT.localPosition;
        Quaternion preRot = pieceT.localRotation;

        Vector3 localDelta = gen.transform.InverseTransformVector(worldDir * distance);
        Vector3 candidate = prePos + localDelta;
        float yMax = grab != null ? grab.yGrabBounds : 1f;
        candidate.y = Mathf.Clamp(candidate.y, 0f, yMax);

        Vector3 resolved = mgr.ResolveGrabPosition(sheet.piece, prePos, candidate);
        pieceT.localPosition = resolved;
        mgr.SettlePiece(sheet.piece);
        mgr.NotifyMoveCommitted(sheet.piece, prePos, preRot);

        float actual = gen.transform.TransformVector(pieceT.localPosition - prePos).magnitude;
        result["moved"] = direction;
        result["sheet"] = pieceId;
        result["requestedMeters"] = System.Math.Round(distance, 3);
        result["actualMeters"] = System.Math.Round(actual, 3);
        if (actual < distance * 0.5f)
            result["note"] = "The piece was blocked by another piece or a bound and moved less than asked.";
    }

    private static bool TryWorldDirection(string direction, out Vector3 world) {
        world = Vector3.zero;
        if (direction == "up") { world = Vector3.up; return true; }
        if (direction == "down") { world = Vector3.down; return true; }

        Transform cam = CameraRig.MainTransform;
        if (cam == null) return false;

        Vector3 forward = cam.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        switch (direction) {
            case "forward": world = forward; return true;
            case "back": world = -forward; return true;
            case "left": world = -right; return true;
            case "right": world = right; return true;
        }
        return false;
    }
}
