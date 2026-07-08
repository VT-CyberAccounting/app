using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oculus.Interaction;
using Oculus.Interaction.Input;

#if UNITY_EDITOR
using UnityEditor;

public class WatchBuilder
{
    private const float ButtonWidth = 80f;
    private const float Padding = 6f;
    private const float Spacing = 6f;
    private const float AssistantThickness = ButtonWidth * 0.5f;

    private static readonly Color ResetBg = MetaTokens.Alpha(MetaTokens.White, 0.05f);
    private static readonly Color ResetBorder = MetaTokens.SheetAlt;
    private static readonly Color ResetText = MetaTokens.NeutralC0;
    private static readonly Color ButtonBackBg = MetaTokens.Alpha(MetaTokens.Sheet, 1f);

    [MenuItem("Tools/Build Watch")]
    public static void BuildWatch()
    {
        GameObject existing = GameObject.Find("Watch");
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Watch Exists",
                "A Watch already exists in the scene. Replace it? The canvas and interaction rigs are regenerated from prefabs.",
                "Replace", "Cancel");
            if (!replace) return;
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject root = new GameObject("Watch");
        Undo.RegisterCreatedObjectUndo(root, "Build Watch");

        Watch menu = root.AddComponent<Watch>();

        GameObject canvasObj = CreateCanvas(root);
        GameObject buttons = CreateButtonColumn(canvasObj);
        GameObject dataBtn = PanelBuilderUI.CreatePillButton(buttons.transform, "Data Panel", ResetBg, ResetBorder, ResetText, ButtonWidth, fontSize: 12f);
        GameObject toolBtn = PanelBuilderUI.CreatePillButton(buttons.transform, "Tool Panel", ResetBg, ResetBorder, ResetText, ButtonWidth, fontSize: 12f);
        PanelBuilderUI.CreateBackCover(dataBtn, ButtonBackBg, inset: 0f, name: "ButtonBack");
        PanelBuilderUI.CreateBackCover(toolBtn, ButtonBackBg, inset: 0f, name: "ButtonBack");
        GameObject geminiBtn = CreateGeminiButton(canvasObj);
        PanelBuilderUI.MakeFrontFacing(dataBtn);
        PanelBuilderUI.MakeFrontFacing(toolBtn);
        PanelBuilderUI.MakeFrontFacing(geminiBtn);
        ApplyFonts(root);

        WireReferences(menu);

        PanelBuilderUI.EnsureRayAndPokeInteractions(canvasObj);
        PanelBuilderUI.SetLayerRecursive(canvasObj, PanelBuilderUI.UIPanelLayerName);
        PanelBuilderUI.WireBackSideFilter(root, canvasObj);

        Selection.activeGameObject = root;
        Debug.Log("[WatchBuilder] Watch built. Verify the Hand reference in play mode.");
    }

    private static GameObject CreateCanvas(GameObject root)
    {
        GameObject canvasObj = new GameObject("WatchCanvas");
        canvasObj.transform.SetParent(root.transform, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform rect = canvasObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(
            ButtonWidth + Spacing + AssistantThickness,
            MetaTokens.ButtonHeight * 2f + Spacing + Padding * 2f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one * 0.001f;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        canvasObj.AddComponent<GraphicRaycaster>();

        return canvasObj;
    }

    private static GameObject CreateButtonColumn(GameObject canvasObj)
    {
        GameObject column = new GameObject("Buttons");
        column.transform.SetParent(canvasObj.transform, false);

        RectTransform rect = column.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(ButtonWidth, 0f);
        rect.anchoredPosition = new Vector2(AssistantThickness + Spacing, 0f);

        VerticalLayoutGroup vlg = column.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(0, 0, (int)Padding, (int)Padding);
        vlg.spacing = Spacing;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        return column;
    }

    private static GameObject CreateGeminiButton(GameObject canvasObj)
    {
        GameObject gemini = PanelBuilderUI.CreatePillButton(
            canvasObj.transform, "Gemini", ResetBg, ResetBorder, ResetText, ButtonWidth,
            fontSize: 12f, displayText: "Assistant");

        RectTransform rect = gemini.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(MetaTokens.ButtonHeight * 2f + Spacing, AssistantThickness);
        rect.anchoredPosition = new Vector2(AssistantThickness * 0.5f, 0f);
        rect.localEulerAngles = new Vector3(0f, 0f, -90f);

        PanelBuilderUI.CreateBackCover(gemini, ButtonBackBg, inset: 0f, name: "ButtonBack");
        return gemini;
    }

    private static void WireReferences(Watch menu)
    {
        SerializedObject so = new SerializedObject(menu);

        DataPanelUI dataPanel = Object.FindAnyObjectByType<DataPanelUI>();
        ToolPanelUI toolPanel = Object.FindAnyObjectByType<ToolPanelUI>();
        GeminiClient geminiClient = Object.FindAnyObjectByType<GeminiClient>();

        SerializedProperty dataProp = so.FindProperty("dataPanelUI");
        if (dataProp != null) dataProp.objectReferenceValue = dataPanel;
        SerializedProperty toolProp = so.FindProperty("toolPanelUI");
        if (toolProp != null) toolProp.objectReferenceValue = toolPanel;
        SerializedProperty geminiProp = so.FindProperty("geminiClient");
        if (geminiProp != null) geminiProp.objectReferenceValue = geminiClient;

        SerializedProperty handProp = so.FindProperty("_hand");
        Object leftHand = FindLeftHand();
        if (handProp != null && leftHand != null) handProp.objectReferenceValue = leftHand;

        so.ApplyModifiedPropertiesWithoutUndo();

        if (dataPanel == null || toolPanel == null)
            Debug.LogWarning("[WatchBuilder] DataPanelUI or ToolPanelUI not found; assign Watch panel references manually.");
        if (geminiClient == null)
            Debug.LogWarning("[WatchBuilder] GeminiClient not found; assign the Watch Gemini reference manually.");
        if (leftHand == null)
            Debug.LogWarning("[WatchBuilder] No left IHand found; assign the Watch Hand reference manually.");
    }

    private static readonly string[] ExcludedAncestors = { "Reticle", "Grab", "Snap", "Pinch", "Pointer" };

    private static Object FindLeftHand()
    {
        MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        Object fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            if (!(all[i] is IHand hand)) continue;
            if (IsExcludedHand(all[i].transform)) continue;
            if (!IsLeftHand(hand, all[i].transform)) continue;

            if (all[i].GetComponentInParent<HandVisual>(true) != null ||
                all[i].GetComponentInChildren<HandVisual>(true) != null)
                return all[i];

            if (fallback == null) fallback = all[i];
        }
        return fallback;
    }

    private static bool IsExcludedHand(Transform t)
    {
        while (t != null)
        {
            for (int i = 0; i < ExcludedAncestors.Length; i++)
                if (t.name.IndexOf(ExcludedAncestors[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            t = t.parent;
        }
        return false;
    }

    private static bool IsLeftHand(IHand hand, Transform t)
    {
        try
        {
            if (hand.Handedness == Handedness.Left) return true;
        }
        catch { }

        while (t != null)
        {
            if (t.name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            t = t.parent;
        }
        return false;
    }

    private static void ApplyFonts(GameObject root)
    {
        TMPro.TMP_FontAsset body = LoadFont("Inter-Medium SDF");
        if (body == null) return;

        TMPro.TextMeshProUGUI[] texts = root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
            texts[i].font = body;

        Watch watch = root.GetComponent<Watch>();
        if (watch == null) return;

        SerializedObject so = new SerializedObject(watch);
        SerializedProperty bodyProp = so.FindProperty("bodyFont");
        if (bodyProp != null) bodyProp.objectReferenceValue = body;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TMPro.TMP_FontAsset LoadFont(string assetName)
    {
        string[] guids = AssetDatabase.FindAssets($"{assetName} t:TMP_FontAsset");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}

#endif
