using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class DataPanelBuilder
{
#if UNITY_EDITOR

    [MenuItem("Tools/Build Data Panel")]
    public static void BuildDataPanel()
    {
        GameObject root = PanelBuilderUI.PrepareRoot("Data Panel");
        if (root == null) return;

        DataPanelUI panelUI = PanelBuilderUI.GetOrAddComponent<DataPanelUI>(root);
        WireDataPanelReferences(panelUI);

        PanelBuilderUI.PanelShell shell = PanelBuilderUI.BuildShell(root, "DataCanvas", "Data Panel");
        PanelBuilderUI.FinishShell(root, shell.canvas, "[DataPanelBuilder]");
        PanelBuilderUI.RewireWatchReferences(panelUI, null);

        Debug.Log("[DataPanelBuilder] Data panel built. The dataset rail and filter content are generated at runtime.");
    }

    private static void WireDataPanelReferences(DataPanelUI panelUI)
    {
        SheetController controller = Object.FindAnyObjectByType<SheetController>();
        if (controller == null)
        {
            Debug.LogWarning("[DataPanelBuilder] No SheetController found in scene; leave DataPanelUI.sheetController unassigned until one exists.");
            return;
        }

        SerializedObject so = new SerializedObject(panelUI);
        SerializedProperty controllerProp = so.FindProperty("sheetController");
        if (controllerProp != null) controllerProp.objectReferenceValue = controller;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

#endif
}
