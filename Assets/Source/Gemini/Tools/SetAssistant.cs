using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class SetAssistant : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "SetAssistant",
        Description = "Turn the Gemini voice assistant on or off, or toggle it (the Gemini button on the wrist watch). " +
                      "Turning it off ends voice control until the user re-enables it, so confirm before turning it off.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "state", new Schema { Type = Type.String, Enum = new List<string> { "on", "off", "toggle" } } }
            },
            Required = new List<string> { "state" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        var watch = Scene.Assistant;
        if (watch == null) { result["error"] = "Watch not found in scene."; return; }

        TryGet(args, "state", out var stateArg);
        string state = AsString(stateArg)?.Trim().ToLowerInvariant();

        bool active;
        switch (state) {
            case "on": active = true; break;
            case "off": active = false; break;
            case "toggle": active = !watch.IsGeminiActive; break;
            default: result["error"] = $"Unknown state '{state}'. Use 'on', 'off', or 'toggle'."; return;
        }

        watch.SetGeminiActive(active);
        result["active"] = active;
    }
}
