using System;
using System.Collections.Generic;
using Google.GenAI.Types;
using Type = Google.GenAI.Types.Type;

public sealed class FindRowColumn : SceneTool {

    private const int MaxMatches = 20;

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "FindRowColumn",
        Description = "Look up rows or columns by name and get their 1-based numbers and visibility. Prefer this " +
                      "over GetSheetInfo when you only need to map a few names to numbers, especially on large " +
                      "datasets: it returns just the matches instead of every row and column. Matching is " +
                      "case-insensitive and matches partial names. It also reports open dataset tabs whose names " +
                      "match ('datasetMatches'): if the user's name matches a dataset rather than a row or column, " +
                      "they likely meant that dataset — switch with SwitchDataset instead.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "query", new Schema { Type = Type.String,
                    Description = "The name (or part of it) to search for." } },
                { "axis", new Schema { Type = Type.String, Enum = new List<string> { "rows", "columns", "both" },
                    Description = "Where to search. Defaults to both." } }
            },
            Required = new List<string> { "query" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        var data = Scene.Data;
        if (data == null) { result["error"] = "No data source found in scene."; return; }

        TryGet(args, "query", out var queryArg);
        string query = AsString(queryArg)?.Trim();
        if (string.IsNullOrEmpty(query)) { result["error"] = "query is required."; return; }

        TryGet(args, "axis", out var axisArg);
        string axis = AsString(axisArg)?.Trim().ToLowerInvariant();
        bool searchRows = axis != "columns";
        bool searchColumns = axis != "rows";

        string dataset = ActiveDatasetLabel();
        if (dataset != null) result["dataset"] = dataset;

        int rowHits = 0, colHits = 0;
        if (searchColumns) {
            var matches = Search(query, data.ColumnTitles, data.IsColumnVisible, out bool truncated);
            colHits = matches.Count;
            result["columns"] = matches;
            if (truncated) result["columnsTruncated"] = true;
        }
        if (searchRows) {
            var matches = Search(query, data.RowTitles, data.IsRowVisible, out bool truncated);
            rowHits = matches.Count;
            result["rows"] = matches;
            if (truncated) result["rowsTruncated"] = true;
        }

        var tabMatches = MatchDatasets(query);
        if (tabMatches.Count > 0) {
            result["datasetMatches"] = tabMatches;
            if (rowHits == 0 && colHits == 0)
                result["hint"] = "No rows or columns match, but an open dataset's name does — the user likely " +
                                 "means that dataset; open it with SwitchDataset.";
        }
    }

    private static List<object> MatchDatasets(string query) {
        var matches = new List<object>();
        var datasets = Scene.Datasets;
        if (datasets == null) return matches;

        for (int i = 0; i < datasets.TabCount; i++) {
            string label = datasets.Tabs[i].label;
            if (string.IsNullOrEmpty(label)) continue;
            bool hit = label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       query.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hit) continue;
            matches.Add(new Dictionary<string, object> {
                { "number", i + 1 },
                { "name", label },
                { "active", i == datasets.ActiveIndex }
            });
        }
        return matches;
    }

    private static List<object> Search(string query, IReadOnlyList<string> titles,
        Func<int, bool> isVisible, out bool truncated) {
        var matches = new List<object>();
        truncated = false;
        for (int i = 0; i < titles.Count; i++) {
            string title = titles[i];
            if (string.IsNullOrEmpty(title) ||
                title.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (matches.Count >= MaxMatches) { truncated = true; break; }
            matches.Add(new Dictionary<string, object> {
                { "number", i + 1 },
                { "title", title },
                { "visible", isVisible(i) }
            });
        }
        return matches;
    }
}
