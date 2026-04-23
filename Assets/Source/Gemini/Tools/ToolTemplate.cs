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

    public static async Task Run(AsyncSession session, FunctionCall call) {
        if (!registry.TryGetValue(call.Name, out var tool)) {
            Debug.LogWarning($"[ToolTemplate] Unknown tool: {call.Name}");
            return;
        }
        var result = await tool.Execute(call.Args);
        await session.SendToolResponseAsync(new LiveSendToolResponseParameters {
            FunctionResponses = new List<FunctionResponse> {
                new FunctionResponse {
                    Id = call.Id,
                    Name = call.Name,
                    Response = result
                }
            }
        });
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
