using UnityEngine;

public static class Scene {

    private static SheetController sheetController;
    private static ToolController toolController;
    private static ToolPanelUI toolPanelUI;
    private static DataPanelUI dataPanelUI;
    private static DataSource dataSource;
    private static Watch watch;
    private static InspectTool inspectTool;
    private static CompareTool compareTool;
    private static SliceTool sliceTool;
    private static ColorTool colorTool;
    private static GrabTool grabTool;
    private static SheetManager sheetManager;
    private static SheetGenerator sheetGenerator;
    private static DatasetManager datasetManager;

    public static SheetController Sheet => Resolve(ref sheetController);
    public static ToolController Tools => Resolve(ref toolController);
    public static ToolPanelUI ToolPanel => Resolve(ref toolPanelUI);
    public static DataPanelUI DataPanel => Resolve(ref dataPanelUI);
    public static Watch Assistant => Resolve(ref watch);
    public static InspectTool Inspect => Resolve(ref inspectTool);
    public static CompareTool Compare => Resolve(ref compareTool);
    public static SliceTool Slice => Resolve(ref sliceTool);
    public static ColorTool Color => Resolve(ref colorTool);
    public static GrabTool Grab => Resolve(ref grabTool);
    public static SheetManager Sheets => Resolve(ref sheetManager);
    public static SheetGenerator Generator => Resolve(ref sheetGenerator);
    public static DatasetManager Datasets => DatasetManager.Instance != null ? DatasetManager.Instance : Resolve(ref datasetManager);

    public static DataSource Data {
        get {
            var active = DatasetManager.ActiveSource;
            if (active != null) return active;
            if (dataSource == null) {
                var sheet = Sheet;
                dataSource = sheet != null ? sheet.DataSource : Object.FindAnyObjectByType<DataSource>();
            }
            return dataSource;
        }
    }

    private static T Resolve<T>(ref T cached) where T : Object {
        if (cached == null) cached = Object.FindAnyObjectByType<T>();
        return cached;
    }
}
