using System;
using System.Collections.Generic;
using Google.GenAI.Types;
using Type = Google.GenAI.Types.Type;

public sealed class SwitchDataset : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "SwitchDataset",
        Description = "Switch to one of the open dataset tabs, the same as the user tapping that tab. Each dataset " +
                      "keeps its own edits, compare pins, and undo history; switching restores them.",
        Parameters = new Schema {
            Type = Type.Object,
            Properties = new Dictionary<string, Schema> {
                { "dataset", new Schema { Type = Type.String,
                    Description = "The dataset's name or its tab number." } }
            },
            Required = new List<string> { "dataset" }
        }
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        var datasets = Scene.Datasets;
        if (datasets == null || datasets.TabCount == 0) { result["error"] = "No datasets are open."; return; }

        TryGet(args, "dataset", out var arg);
        string query = AsString(arg);
        if (!TryResolveIndex(datasets, query, out int index)) {
            result["error"] = $"No single open dataset matches '{query}'; if several match, ask the user which one.";
            result["available"] = ListLabels(datasets);
            return;
        }

        bool alreadyActive = index == datasets.ActiveIndex;

        var panel = Scene.DataPanel;
        if (panel != null) panel.ShowDataset(index);
        else if (!alreadyActive) datasets.SwitchTab(index);

        result["switched"] = datasets.Tabs[index].label;
        result["index"] = index + 1;
        if (alreadyActive) result["alreadyActive"] = true;

        var data = datasets.Active;
        if (data != null && data.IsLoaded) {
            result["rowCount"] = data.RowCount;
            result["columnCount"] = data.ColumnCount;
        }
        if (!alreadyActive)
            result["note"] = "Row and column numbers now refer to this dataset; call GetSheetInfo before using numbers.";
    }

    private static bool TryResolveIndex(DatasetManager datasets, string query, out int index) {
        index = -1;
        if (string.IsNullOrWhiteSpace(query)) return false;
        query = query.Trim();

        if (int.TryParse(query, out int number)) {
            index = number - 1;
            return index >= 0 && index < datasets.TabCount;
        }

        var tabs = datasets.Tabs;
        for (int i = 0; i < tabs.Count; i++)
            if (string.Equals(tabs[i].label, query, StringComparison.OrdinalIgnoreCase)) { index = i; return true; }

        int only = -1, hits = 0;
        for (int i = 0; i < tabs.Count; i++)
        {
            string label = tabs[i].label;
            if (string.IsNullOrEmpty(label)) continue;
            if (label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                query.IndexOf(label, StringComparison.OrdinalIgnoreCase) < 0) continue;
            only = i;
            hits++;
        }
        if (hits == 1) { index = only; return true; }
        return false;
    }

    private static List<object> ListLabels(DatasetManager datasets) {
        var list = new List<object>();
        var tabs = datasets.Tabs;
        for (int i = 0; i < tabs.Count; i++)
            list.Add(new Dictionary<string, object> { { "number", i + 1 }, { "name", tabs[i].label } });
        return list;
    }
}
