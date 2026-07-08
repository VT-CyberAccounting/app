using System.Collections.Generic;
using Google.GenAI.Types;
using UnityEngine;

public sealed class GetSheetInfo : SceneTool {

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "GetSheetInfo",
        Description = "Read the current state of the sheet and panels: row and column titles ('rows' and 'columns' " +
                      "are title lists where a title's 1-based position is its number; 'hiddenRows'/'hiddenColumns' " +
                      "list the numbers currently hidden), the category each axis represents (rowCategory and " +
                      "columnCategory, when the data provides them), sort modes, the active tool, the lock state, " +
                      "panels, the open dataset tabs, and a summary of tool edits and compare entries. Call this to " +
                      "map names to numbers or to answer questions about the layout, the open datasets, or what has " +
                      "been changed. Numbers are per-dataset: re-call this after switching datasets."
    };

    protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) {
        var data = Scene.Data;
        if (data == null) { result["error"] = "No data source found in scene."; return; }

        var columns = new List<object>();
        var hiddenColumns = new List<object>();
        for (int i = 0; i < data.ColumnCount; i++) {
            columns.Add(i < data.ColumnTitles.Count ? data.ColumnTitles[i] : "");
            if (!data.IsColumnVisible(i)) hiddenColumns.Add(i + 1);
        }

        var rows = new List<object>();
        var hiddenRows = new List<object>();
        for (int i = 0; i < data.RowCount; i++) {
            rows.Add(i < data.RowTitles.Count ? data.RowTitles[i] : "");
            if (!data.IsRowVisible(i)) hiddenRows.Add(i + 1);
        }

        result["columnCount"] = data.ColumnCount;
        result["rowCount"] = data.RowCount;
        if (!string.IsNullOrEmpty(data.RowAxisTitle)) result["rowCategory"] = data.RowAxisTitle;
        if (!string.IsNullOrEmpty(data.ColumnAxisTitle)) result["columnCategory"] = data.ColumnAxisTitle;
        result["columns"] = columns;
        result["rows"] = rows;
        if (hiddenColumns.Count > 0) result["hiddenColumns"] = hiddenColumns;
        if (hiddenRows.Count > 0) result["hiddenRows"] = hiddenRows;
        result["columnSort"] = data.ColumnSortMode.ToString();
        result["rowSort"] = data.RowSortMode.ToString();

        var sheetsMgr = Scene.Sheets;
        if (sheetsMgr != null && sheetsMgr.IsBaked)
            result["sheets"] = DescribePieces(sheetsMgr, data);

        var tools = Scene.Tools;
        result["activeTool"] = tools != null ? tools.SelectedTool.ToString() : "None";
        result["locked"] = tools != null && tools.IsLocked;

        var dataPanel = Scene.DataPanel;
        var toolPanel = Scene.ToolPanel;
        result["dataPanelOpen"] = dataPanel != null && dataPanel.IsVisible;
        result["toolPanelOpen"] = toolPanel != null && toolPanel.IsVisible;
        int dataTab = dataPanel != null ? dataPanel.ActiveWindow : -1;
        result["dataTab"] = dataTab == 1 ? "rows" : dataTab == 0 ? "columns" : "none";

        var datasets = Scene.Datasets;
        if (datasets != null && datasets.TabCount > 0) {
            var tabs = new List<object>();
            for (int i = 0; i < datasets.TabCount; i++) {
                tabs.Add(new Dictionary<string, object> {
                    { "number", i + 1 },
                    { "name", datasets.Tabs[i].label },
                    { "active", i == datasets.ActiveIndex },
                    { "locked", datasets.IsTabLocked(i) }
                });
            }
            result["datasets"] = tabs;
            if (datasets.ActiveIndex >= 0 && datasets.ActiveIndex < datasets.TabCount)
                result["dataset"] = datasets.Tabs[datasets.ActiveIndex].label;
            result["datasetCollapsed"] = dataPanel != null && dataPanel.IsCollapsed;
        }

        var inspect = Scene.Inspect;
        if (inspect != null) result["inspectMode"] = inspect.CurrentModeName;
        var slice = Scene.Slice;
        if (slice != null) result["sliceAxis"] = slice.CurrentAxisName;
        var compareTool = Scene.Compare;
        if (compareTool != null) result["compareMode"] = compareTool.CurrentModeName;
        var colorTool = Scene.Color;
        if (colorTool != null) result["selectedColor"] = colorTool.CurrentColorName;

        result["undoDepth"] = tools != null ? tools.Journal.Count : 0;
        var nextUndo = tools != null ? tools.Journal.Peek() : null;
        if (nextUndo != null) result["nextUndo"] = EditJournal.KindName(nextUndo.kind);

        var watch = Scene.Assistant;
        result["assistantActive"] = watch != null && watch.IsGeminiActive;

        result["edits"] = DescribeEdits();
    }

    private static List<object> DescribePieces(SheetManager mgr, DataSource data) {
        var pieces = new List<object>();
        var list = mgr.Sheets;
        var gen = mgr.sheetGenerator;
        var snap = mgr.HasColorOverrides ? mgr.CaptureState() : null;
        for (int i = 0; i < list.Count; i++) {
            var s = list[i];
            int cells = (s.rowMax - s.rowMin + 1) * (s.colMax - s.colMin + 1);
            double sum = 0;
            int n = 0;
            if (gen != null && data != null)
                for (int vr = s.rowMin; vr <= s.rowMax; vr++) {
                    int dr = gen.VisibleRowToData(vr);
                    if (dr < 0) continue;
                    for (int vc = s.colMin; vc <= s.colMax; vc++) {
                        int dc = gen.VisibleColToData(vc);
                        if (dc < 0) continue;
                        sum += data.GetValue(dr, dc);
                        n++;
                    }
                }
            var piece = new Dictionary<string, object> {
                { "id", i + 1 },
                { "cellCount", cells },
                { "sum", System.Math.Round(sum, 4) },
                { "mean", n > 0 ? (object)System.Math.Round(sum / n, 4) : null },
                { "rowRange", new List<object> { s.rowMin + 1, s.rowMax + 1 } },
                { "colRange", new List<object> { s.colMin + 1, s.colMax + 1 } },
                { "moved", s.moved }
            };
            if (s.piece != null) {
                Vector3 p = s.piece.transform.localPosition;
                piece["position"] = new Dictionary<string, object> {
                    { "columns", System.Math.Round(p.x, 3) },
                    { "up", System.Math.Round(p.y, 3) },
                    { "rows", System.Math.Round(p.z, 3) }
                };
            }
            string topColor = TopOverrideColor(snap, s);
            if (topColor != null) piece["color"] = topColor;
            pieces.Add(piece);
        }
        return pieces;
    }

    private static string TopOverrideColor(SheetManager.EditSnapshot snap, SheetManager.Sheet s) {
        if (snap == null || snap.colors == null) return null;
        for (int i = snap.colors.Count - 1; i >= 0; i--) {
            var c = snap.colors[i];
            if (c.rMin <= s.rowMax && c.rMax >= s.rowMin && c.cMin <= s.colMax && c.cMax >= s.colMin)
                return NearestColorName(c.color);
        }
        return null;
    }

    private static object DescribeEdits() {
        var mgr = Scene.Sheets;
        var compare = Scene.Compare;
        int compareCells = compare != null ? compare.CellEntryCount : 0;
        int compareSheets = compare != null ? compare.SheetEntryCount : 0;

        var snap = mgr != null && mgr.IsBaked && mgr.HasInvasiveEdits ? mgr.CaptureState() : null;
        if (snap == null && compareCells == 0 && compareSheets == 0) return "none";

        var edits = new Dictionary<string, object>();

        if (snap != null) {
            int moved = 0;
            if (snap.sheets != null)
                for (int i = 0; i < snap.sheets.Count; i++)
                    if (snap.sheets[i].moved) moved++;

            var colors = new List<object>();
            if (snap.colors != null)
                for (int i = 0; i < snap.colors.Count; i++) {
                    var c = snap.colors[i];
                    int cells = (c.rMax - c.rMin + 1) * (c.cMax - c.cMin + 1);
                    colors.Add(new Dictionary<string, object> {
                        { "color", NearestColorName(c.color) },
                        { "cells", cells }
                    });
                }

            edits["pieceCount"] = snap.sheets != null ? snap.sheets.Count : 0;
            edits["movedPieceCount"] = moved;
            edits["colors"] = colors;
        }

        if (compareCells > 0) {
            edits["compareCellEntries"] = compareCells;
            var cells = new List<object>();
            var entries = compare.CellEntries;
            for (int i = 0; i < entries.Count; i++) {
                var e = entries[i];
                cells.Add(new Dictionary<string, object> {
                    { "column", e.ColumnTitle },
                    { "row", e.RowTitle },
                    { "value", System.Math.Round(e.Value, 4) },
                    { "color", NearestColorName(e.Color) }
                });
            }
            edits["compareCells"] = cells;
        }

        if (compareSheets > 0) {
            edits["compareSheetEntries"] = compareSheets;
            var sheets = new List<object>();
            var entries = compare.SheetLedgerEntries;
            for (int i = 0; i < entries.Count; i++) {
                var e = entries[i];
                sheets.Add(new Dictionary<string, object> {
                    { "cellCount", e.CellCount },
                    { "mean", System.Math.Round(e.Mean, 4) },
                    { "sum", System.Math.Round(e.Sum, 4) },
                    { "rowRange", new List<object> { e.RMin + 1, e.RMax + 1 } },
                    { "colRange", new List<object> { e.CMin + 1, e.CMax + 1 } },
                    { "color", NearestColorName(e.Color) }
                });
            }
            edits["compareSheets"] = sheets;
        }

        return edits;
    }

    private static string NearestColorName(Color c) {
        var tool = Scene.Color;
        if (tool == null || tool.palette == null || tool.palette.Length == 0)
            return $"#{ColorUtility.ToHtmlStringRGB(c)}";

        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < tool.palette.Length; i++) {
            Color p = tool.palette[i];
            float d = (p.r - c.r) * (p.r - c.r) + (p.g - c.g) * (p.g - c.g) + (p.b - c.b) * (p.b - c.b);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best < tool.PaletteNames.Count ? tool.PaletteNames[best] : $"#{ColorUtility.ToHtmlStringRGB(c)}";
    }
}
