using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;
using UnityEngine;

public abstract class ToolTemplate {
    public abstract FunctionDeclaration Declaration { get; }
    protected abstract Task<Dictionary<string, object>> Execute(Dictionary<string, object> args);

    public virtual bool IsAvailable() => true;

    public static async Task Run(AsyncSession session, FunctionCall call) {
        if (!registry.TryGetValue(call.Name, out var tool)) {
            Debug.LogWarning($"[ToolTemplate] Unknown tool: {call.Name}");
            return;
        }

        Dictionary<string, object> result;
        try {
            result = await tool.Execute(call.Args).ConfigureAwait(false);
        }
        catch (Exception e) {
            Debug.LogError($"[ToolTemplate] {call.Name} failed: {e}");
            result = new Dictionary<string, object> { { "error", e.Message } };
        }

        Sanitize(result);

        try {
            await Respond(session, call, result).ConfigureAwait(false);
        }
        catch (Exception e) {
            Debug.LogError($"[ToolTemplate] SendToolResponse failed: {e}");
            try {
                await Respond(session, call, new Dictionary<string, object> {
                    { "error", "The tool result could not be delivered. The action may still have applied; " +
                               "verify with GetSheetInfo before retrying." }
                }).ConfigureAwait(false);
            }
            catch (Exception e2) {
                Debug.LogError($"[ToolTemplate] SendToolResponse fallback failed: {e2}");
            }
        }
    }

    private static Task Respond(AsyncSession session, FunctionCall call, Dictionary<string, object> result) {
        return session.SendToolResponseAsync(new LiveSendToolResponseParameters {
            FunctionResponses = new List<FunctionResponse> {
                new FunctionResponse {
                    Id = call.Id,
                    Name = call.Name,
                    Response = result
                }
            }
        });
    }

    private static object Sanitize(object value) {
        switch (value) {
            case double d when double.IsNaN(d) || double.IsInfinity(d): return null;
            case float f when float.IsNaN(f) || float.IsInfinity(f): return null;
            case Dictionary<string, object> dict:
                foreach (var key in dict.Keys.ToList()) dict[key] = Sanitize(dict[key]);
                return dict;
            case List<object> list:
                for (int i = 0; i < list.Count; i++) list[i] = Sanitize(list[i]);
                return list;
            default: return value;
        }
    }

    static Dictionary<string, ToolTemplate> registry;
    public static IReadOnlyDictionary<string, ToolTemplate> Registry => registry;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init() {
        registry = new Dictionary<string, ToolTemplate>();
        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ToolTemplate)) && !t.IsAbstract && t.IsSealed);
        foreach (var type in types) {
            var instance = (ToolTemplate)Activator.CreateInstance(type);
            registry[instance.Declaration.Name] = instance;
        }
    }
}
