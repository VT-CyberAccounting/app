using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine.Profiling;

public abstract class SceneTool : ToolTemplate {

    protected abstract void Run(Dictionary<string, object> args, Dictionary<string, object> result);

    protected override async Task<Dictionary<string, object>> Execute(Dictionary<string, object> args) {
        var result = new Dictionary<string, object>();
        string sample = "GeminiTool." + GetType().Name;
        await MainThread.Run(() => {
            Profiler.BeginSample(sample);
            try {
                Run(args ?? new Dictionary<string, object>(), result);
            }
            catch (Exception e) {
                result.Clear();
                result["error"] = e.Message;
            }
            finally {
                AppendState(result);
                Profiler.EndSample();
            }
        });
        if (!result.ContainsKey("error") && !result.ContainsKey("preconditionUnmet") &&
            !result.ContainsKey("needsSheet") && !result.ContainsKey("ok"))
            result["ok"] = true;
        return result;
    }

    protected static bool TryResolveTargetBounds(Dictionary<string, object> args, Dictionary<string, object> result,
        out int rowMin, out int rowMax, out int colMin, out int colMax, out int pieceId) {
        rowMin = rowMax = colMin = colMax = 0;
        pieceId = 0;

        var gen = Scene.Generator;
        if (gen == null) { result["error"] = "No sheet in scene."; return false; }
        if (!gen.IsPresented) {
            Refuse(result, "visible sheet",
                "The sheet is hidden because the dataset is collapsed. Offer to reopen it with SwitchDataset.");
            return false;
        }

        var mgr = Scene.Sheets;
        if (mgr != null && mgr.IsBaked) {
            var list = mgr.Sheets;
            SheetManager.Sheet piece;
            if (TryGet(args, "sheet", out var sheetArg)) {
                pieceId = AsInt(sheetArg);
                int idx = pieceId - 1;
                if (idx < 0 || idx >= list.Count) { result["error"] = $"There is no sheet piece #{pieceId}."; return false; }
                piece = list[idx];
            }
            else if (list.Count == 1) { piece = list[0]; pieceId = 1; }
            else {
                result["needsSheet"] = true;
                result["message"] = "The sheet is sliced into several pieces; ask the user which one.";
                var pieces = new List<object>();
                for (int i = 0; i < list.Count; i++) {
                    var s = list[i];
                    pieces.Add(new Dictionary<string, object> {
                        { "id", i + 1 },
                        { "cellCount", (s.rowMax - s.rowMin + 1) * (s.colMax - s.colMin + 1) }
                    });
                }
                result["sheets"] = pieces;
                return false;
            }
            rowMin = piece.rowMin; rowMax = piece.rowMax; colMin = piece.colMin; colMax = piece.colMax;
        }
        else {
            rowMin = 0; rowMax = gen.RowCount - 1;
            colMin = 0; colMax = gen.VisibleColCount - 1;
        }
        return true;
    }

    public static string StateSummary() {
        var holder = new Dictionary<string, object>();
        AppendState(holder);
        return holder.TryGetValue("state", out var state) ? JsonSerializer.Serialize(state) : "{}";
    }

    private static void AppendState(Dictionary<string, object> result) {
        try {
            var state = new Dictionary<string, object>();
            var tools = Scene.Tools;
            state["activeTool"] = tools != null ? tools.SelectedTool.ToString() : "None";
            state["locked"] = tools != null && tools.IsLocked;
            var toolPanel = Scene.ToolPanel;
            state["toolPanelOpen"] = toolPanel != null && toolPanel.IsVisible;
            var dataPanel = Scene.DataPanel;
            state["dataPanelOpen"] = dataPanel != null && dataPanel.IsVisible;
            string dataset = ActiveDatasetLabel();
            state["dataset"] = dataset ?? "none";
            if (dataset != null)
                state["datasetCollapsed"] = dataPanel != null && dataPanel.IsCollapsed;
            string notice = SheetNotices.ConsumeForAssistant();
            if (!string.IsNullOrEmpty(notice)) state["notice"] = notice;
            result["state"] = state;
        }
        catch (Exception) { }
    }

    protected static bool TryGet(Dictionary<string, object> args, string key, out object value) {
        value = null;
        return args != null && args.TryGetValue(key, out value) && value != null;
    }

    protected static int AsInt(object v) {
        if (v is JsonElement je)
            return je.ValueKind == JsonValueKind.Number ? je.GetInt32() : int.Parse(je.GetString());
        return Convert.ToInt32(v);
    }

    protected static float AsFloat(object v) {
        if (v is JsonElement je)
            return je.ValueKind == JsonValueKind.Number
                ? (float)je.GetDouble()
                : float.Parse(je.GetString(), System.Globalization.CultureInfo.InvariantCulture);
        return Convert.ToSingle(v);
    }

    protected static bool AsBool(object v) {
        if (v is JsonElement je) {
            if (je.ValueKind == JsonValueKind.True || je.ValueKind == JsonValueKind.False) return je.GetBoolean();
            if (je.ValueKind == JsonValueKind.String) return bool.Parse(je.GetString());
            if (je.ValueKind == JsonValueKind.Number) return je.GetInt32() != 0;
        }
        return Convert.ToBoolean(v);
    }

    protected static string AsString(object v) {
        if (v is JsonElement je)
            return je.ValueKind == JsonValueKind.String ? je.GetString() : je.ToString();
        return v?.ToString();
    }

    protected static bool TryParseTool(string s, out ToolType tool) {
        tool = ToolType.None;
        if (string.IsNullOrEmpty(s)) return false;
        switch (s.Trim().ToLowerInvariant()) {
            case "none": tool = ToolType.None; return true;
            case "inspect": tool = ToolType.Inspect; return true;
            case "compare": tool = ToolType.Compare; return true;
            case "slice": tool = ToolType.Slice; return true;
            case "color": case "colour": tool = ToolType.Color; return true;
            case "grab": tool = ToolType.Grab; return true;
        }
        return false;
    }

    protected static void Refuse(Dictionary<string, object> result, string unmet, string message) {
        result["preconditionUnmet"] = unmet;
        result["message"] = message;
    }

    protected static bool SheetLocked(Dictionary<string, object> result) {
        var tools = Scene.Tools;
        if (tools != null && tools.IsLocked) {
            Refuse(result, "unlocked data panel",
                "The data panel is locked by tool edits (slices, colors, moved pieces, or compare entries). " +
                "Ask the user to undo them first (UndoEdit or UndoAllEdits).");
            return true;
        }
        return false;
    }

    protected static bool RequireToolPanelOpen(Dictionary<string, object> result, string action) {
        var panel = Scene.ToolPanel;
        if (panel != null && panel.IsVisible) return false;
        Refuse(result, "open tool panel",
            $"The tool panel is closed, so {action} is not available — its buttons are not on screen. " +
            "Tell the user the tool panel must be open first and offer to open it with SetPanel.");
        return true;
    }

    protected static bool RequireToolSelected(ToolType tool, Dictionary<string, object> result) {
        var tools = Scene.Tools;
        if (tools != null && tools.SelectedTool == tool) return false;
        string name = tool.ToString();
        Refuse(result, $"{name} tool selected",
            $"The {name} tool is not selected, so its buttons are not on screen. " +
            $"Offer to select it with SelectTool(tool: '{name.ToLowerInvariant()}').");
        return true;
    }

    protected static string ActiveDatasetLabel() {
        var datasets = Scene.Datasets;
        if (datasets == null) return null;
        int i = datasets.ActiveIndex;
        return (i >= 0 && i < datasets.TabCount) ? datasets.Tabs[i].label : null;
    }

    protected static bool RequireActiveDataset(Dictionary<string, object> args, Dictionary<string, object> result) {
        if (!TryGet(args, "dataset", out var arg)) return false;
        string requested = AsString(arg)?.Trim();
        if (string.IsNullOrEmpty(requested)) return false;

        string active = ActiveDatasetLabel();
        if (active != null &&
            (string.Equals(active, requested, StringComparison.OrdinalIgnoreCase) ||
             active.IndexOf(requested, StringComparison.OrdinalIgnoreCase) >= 0))
            return false;

        Refuse(result, "matching open dataset",
            active == null
                ? $"No dataset is open, so '{requested}' cannot be changed. Ask the user to open a dataset first."
                : $"'{requested}' is not the open dataset; '{active}' is. Switch to it with SwitchDataset first, " +
                  $"or confirm the user actually means '{active}'.");
        if (active != null) result["openDataset"] = active;
        return true;
    }

    protected static bool RequireDatasetExpanded(Dictionary<string, object> result) {
        var panel = Scene.DataPanel;
        if (panel == null || !panel.IsCollapsed) return false;
        Refuse(result, "expanded dataset",
            "The open dataset is collapsed, so the sheet is hidden and the change would not be visible. " +
            "Offer to reopen it with SwitchDataset.");
        return true;
    }

    protected static List<int> AsIntList(object v) {
        var list = new List<int>();
        if (v is JsonElement je && je.ValueKind == JsonValueKind.Array) {
            foreach (var el in je.EnumerateArray()) list.Add(AsInt(el));
        }
        else if (v is System.Collections.IEnumerable en && !(v is string)) {
            foreach (var el in en) list.Add(AsInt(el));
        }
        else if (v != null) {
            list.Add(AsInt(v));
        }
        return list;
    }

    protected static void ApplyVisibility(Dictionary<string, object> args, Dictionary<string, object> result, string axisArg, bool isColumn) {
        if (SheetLocked(result)) return;
        if (RequireActiveDataset(args, result)) return;
        if (RequireDatasetExpanded(result)) return;

        var sheet = Scene.Sheet;
        var data = Scene.Data;
        if (sheet == null || data == null) { result["error"] = "Sheet not found in scene."; return; }

        TryGet(args, axisArg, out var idxArg);
        TryGet(args, "visible", out var visibleArg);
        var items = AsIntList(idxArg);
        bool visible = AsBool(visibleArg);

        int count = isColumn ? data.ColumnCount : data.RowCount;
        var titles = isColumn ? data.ColumnTitles : data.RowTitles;
        var applied = new List<object>();
        var outOfRange = new List<int>();
        sheet.BeginBatch();
        foreach (var n in items) {
            int idx = n - 1;
            if (idx < 0 || idx >= count) { outOfRange.Add(n); continue; }
            if (isColumn) sheet.SetColumnVisible(idx, visible);
            else sheet.SetRowVisible(idx, visible);
            applied.Add(new Dictionary<string, object> {
                { "number", n },
                { "title", idx < titles.Count ? titles[idx] : "" }
            });
        }
        sheet.EndBatch();

        result["visible"] = visible;
        result["applied"] = applied;
        if (outOfRange.Count > 0) result["outOfRange"] = outOfRange;
        string dataset = ActiveDatasetLabel();
        if (dataset != null) result["dataset"] = dataset;
    }
}
