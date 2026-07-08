using System.Collections.Generic;
using System.Threading.Tasks;
using Google.GenAI.Types;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public sealed class GetFinancialsSchema : ToolTemplate {

    private static Task<string> schemaTask;

    public override bool IsAvailable() =>
        SceneManager.GetSceneByName("database reader").isLoaded;

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "GetFinancialsSchema",
        Description = "Get the GraphQL schema of the financial database. Call this once per session before your " +
                      "first QueryFinancials query and write queries against the schema it returns."
    };

    protected override async Task<Dictionary<string, object>> Execute(Dictionary<string, object> args) {
        if (schemaTask == null) schemaTask = LoadSchema();
        string schema = await schemaTask.ConfigureAwait(false);
        if (string.IsNullOrEmpty(schema)) {
            schemaTask = null;
            return new Dictionary<string, object> {
                { "error", "The schema could not be loaded; the financial database may be unavailable." }
            };
        }
        return new Dictionary<string, object> { { "schema", schema } };
    }

    private static Task<string> LoadSchema() {
        var tcs = new TaskCompletionSource<string>();
        _ = MainThread.Run(() => {
            var path = Application.streamingAssetsPath + "/schema.graphql";
            if (!path.Contains("://"))
                path = "file://" + path;
            var req = UnityWebRequest.Get(path);
            var op = req.SendWebRequest();
            op.completed += _ => {
                if (req.result == UnityWebRequest.Result.Success) {
                    tcs.SetResult(req.downloadHandler.text);
                }
                else {
                    Debug.LogError($"[GetFinancialsSchema] Failed to load schema from {path}: {req.error}");
                    tcs.SetResult("");
                }
                req.Dispose();
            };
        });
        return tcs.Task;
    }
}
