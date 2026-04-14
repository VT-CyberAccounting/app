using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Google.GenAI.Types;
using UnityEngine;

public sealed class QueryFinancials : ToolTemplate {
    private static readonly HttpClient http = new HttpClient();
    private static string schema;

    static QueryFinancials() {
        var basePath = Application.streamingAssetsPath;
        if (basePath.StartsWith("file://"))
            basePath = basePath.Substring("file://".Length);
        var path = Path.Combine(basePath, "schema.graphql");
        try {
            schema = System.IO.File.ReadAllText(path);
        } catch (System.Exception e) {
            Debug.LogError($"[QueryFinancials] Failed to load schema: {e.Message}");
            schema = "(schema unavailable)";
        }
    }

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "QueryFinancials",
        Description = "Execute a GraphQL query against the financial database.\n\n" + schema,
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "query", new Schema {
                    Type = Type.String,
                    Description = "A valid GraphQL query string per the schema above"
                }}
            },
            Required = new List<string> { "query" }
        }
    };

    protected override async Task<Dictionary<string, object>> Execute(Dictionary<string, object> args) {
        var query = args["query"].ToString();
        var payload = JsonSerializer.Serialize(new { query });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await http.PostAsync("https://cyberacc.discovery.cs.vt.edu/graphql", content);
        var json = await response.Content.ReadAsStringAsync();

        try {
            var root = JsonNode.Parse(json);
            var nodes = root?["data"]?["sln"]?["nodes"]?.AsArray();
            if (nodes != null && nodes.Count > 30) {
                while (nodes.Count > 30)
                    nodes.RemoveAt(nodes.Count - 1);
            }
            json = root.ToJsonString();
        } catch {
            Debug.LogWarning("[QueryFinancials] Failed to truncate nodes, returning raw response");
        }

        return new Dictionary<string, object> { { "result", json } };
    }
}
