using UnityEngine;

public abstract class SheetTool : MonoBehaviour
{
    [UnityEngine.Serialization.FormerlySerializedAs("controller")]
    public ToolController toolController;
    [UnityEngine.Serialization.FormerlySerializedAs("surfaceGenerator")]
    [UnityEngine.Serialization.FormerlySerializedAs("regionGenerator")]
    public SheetGenerator sheetGenerator;
    [UnityEngine.Serialization.FormerlySerializedAs("regionManager")]
    public SheetManager sheetManager;

    protected void ResolveSheetRefs(bool createSheetManager)
    {
        if (toolController == null) toolController = FindAnyObjectByType<ToolController>();
        if (sheetGenerator == null) sheetGenerator = FindAnyObjectByType<SheetGenerator>();
        if (sheetManager == null) sheetManager = FindAnyObjectByType<SheetManager>();
        if (createSheetManager && sheetManager == null && sheetGenerator != null)
            sheetManager = sheetGenerator.gameObject.AddComponent<SheetManager>();
    }
}
