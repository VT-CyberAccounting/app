using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Google.GenAI.Types;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class QueryFinancials : ToolTemplate {
    private const string DatabaseScene = "database reader";

    private static readonly HttpClient http = new HttpClient();

    public override bool IsAvailable() =>
        SceneManager.GetSceneByName(DatabaseScene).isLoaded;

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "QueryFinancials",
        Description = "Execute a GraphQL query against the financial database. Call GetFinancialsSchema first in a " +
                      "session to learn the schema. An auth token is supplied automatically as the GraphQL variable " +
                      "$authToken; declare and pass it where the schema requires (never ask the user for it). If the " +
                      "tool replies that access is not verified, tell the user to scan the verification QR code.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "query", new Schema {
                    Type = Type.String,
                    Description = "A valid GraphQL query string per the schema from GetFinancialsSchema."
                }}
            },
            Required = new List<string> { "query" }
        }
    };

    protected override async Task<Dictionary<string, object>> Execute(Dictionary<string, object> args) {
        if (!BackendAuth.Has)
            return new Dictionary<string, object> {
                { "error", "Not verified. Ask the user to scan the verification QR code to unlock the financial database." }
            };

        var query = args["query"].ToString();
        var payload = JsonSerializer.Serialize(new { query, variables = new { authToken = BackendAuth.Token } });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await http.PostAsync("https://cyberacc.discovery.cs.vt.edu/graphql", content);
        var json = await response.Content.ReadAsStringAsync();

        var groups = new Dictionary<string, List<VarPoint>>();
        try {
            var root = JsonNode.Parse(json);
            var nodes = root?["data"]?["sln"]?["nodes"]?.AsArray();
            if (nodes != null) {
                while (nodes.Count > 30)
                    nodes.RemoveAt(nodes.Count - 1);
                CollectPoints(nodes, groups);
                json = root.ToJsonString();
            }
        } catch {
            Debug.LogWarning("[QueryFinancials] Failed to parse response, returning raw response");
        }

        if (groups.Count > 0)
            await MainThread.Run(() => {
                UnityEngine.Profiling.Profiler.BeginSample("GeminiTool.QueryFinancials");
                try { Apply(groups); }
                finally { UnityEngine.Profiling.Profiler.EndSample(); }
            });

        return new Dictionary<string, object> { { "result", json } };
    }

    private struct VarPoint {
        public string company;
        public int year;
        public float value;
    }

    private static readonly HashSet<string> Identifiers = new HashSet<string> {
        "id", "year", "gvkey", "sic"
    };

    private static void CollectPoints(JsonArray nodes, Dictionary<string, List<VarPoint>> groups) {
        foreach (var n in nodes) {
            if (!(n is JsonObject obj)) continue;
            string company = Str(obj, "ticker") ?? Str(obj, "name") ?? Str(obj, "cik");
            if (string.IsNullOrEmpty(company)) continue;
            int year = Num(obj, "year", out double y) ? (int)y : 0;

            foreach (var field in obj) {
                if (Identifiers.Contains(field.Key)) continue;
                if (!(field.Value is JsonValue jv) || !jv.TryGetValue(out double v)) continue;
                if (!groups.TryGetValue(field.Key, out var list)) {
                    list = new List<VarPoint>();
                    groups[field.Key] = list;
                }
                list.Add(new VarPoint { company = company, year = year, value = (float)v });
            }
        }
    }

    private static void Apply(Dictionary<string, List<VarPoint>> groups) {
        var mgr = DatasetManager.Instance;
        if (mgr == null) {
            Debug.LogWarning("[QueryFinancials] No DatasetManager in scene; cannot populate tabs.");
            return;
        }
        foreach (var g in groups) {
            var tab = mgr.GetOrCreateVariableTab(g.Key);
            if (tab == null) continue;
            for (int i = 0; i < g.Value.Count; i++)
                tab.AddPoint(g.Value[i].company, g.Value[i].year, g.Value[i].value);
            tab.Rebuild();
        }
    }

    private static string Str(JsonObject obj, string key) {
        return obj[key] is JsonValue jv && jv.TryGetValue(out string s) ? s : null;
    }

    private static bool Num(JsonObject obj, string key, out double value) {
        value = 0d;
        return obj[key] is JsonValue jv && jv.TryGetValue(out value);
    }
}
