using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class DataTooltipBuilder
{
#if UNITY_EDITOR

    private static readonly Color PanelBg = MetaTokens.Alpha(MetaTokens.Sheet, 0.92f);
    private static readonly Color PanelBorder = MetaTokens.Alpha(MetaTokens.White, 0.1f);
    private static readonly Color AccentColor = MetaTokens.Blue;
    private static readonly Color PrimaryText = MetaTokens.TextPrimary;
    private static readonly Color SecondaryText = MetaTokens.NeutralC0;

    private const float CanvasWidth = 300f;
    private const float CanvasHeight = 150f;
    private const float LeftPad = MetaTokens.PanelGutter;
    private const float ContentWidth = 272f;
    private const float TitleHeight = 24f;

    [MenuItem("Tools/Build Tooltip")]
    public static void BuildDataTooltip()
    {
        GameObject existing = GameObject.Find("Tooltip");
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Tooltip Exists",
                "A Tooltip already exists in the scene. Replace it?",
                "Replace", "Cancel");
            if (!replace) return;
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject root = new GameObject("Tooltip");
        Undo.RegisterCreatedObjectUndo(root, "Build Tooltip");
        root.AddComponent<Tooltip>();

        GameObject canvasObj = CreateCanvas(root);
        GameObject borderObj = CreatePanelBorder(canvasObj);
        GameObject panelObj = CreatePanelRoot(borderObj);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();

        CreateSwatch(panelRect, LeftPad, -8f);

        GameObject cellGroup = CreateGroup(panelRect, "CellGroup");
        PopulateCellGroup(cellGroup.GetComponent<RectTransform>());

        GameObject hintGroup = CreateGroup(panelRect, "HintGroup");
        PopulateHintGroup(hintGroup.GetComponent<RectTransform>());
        hintGroup.SetActive(false);

        GameObject headerGroup = CreateGroup(panelRect, "HeaderGroup");
        PopulateHeaderGroup(headerGroup.GetComponent<RectTransform>());
        headerGroup.SetActive(false);

        GameObject sheetGroup = CreateGroup(panelRect, "SheetGroup");
        PopulateSheetGroup(sheetGroup.GetComponent<RectTransform>());
        sheetGroup.SetActive(false);

        ApplyFonts(root);

        Selection.activeGameObject = root;
        Debug.Log("[DataTooltipBuilder] Tooltip built successfully. Remember to wire InspectTool.tooltip.");
    }

    private static TMP_FontAsset LoadFont(string assetName)
    {
        string[] guids = AssetDatabase.FindAssets($"{assetName} t:TMP_FontAsset");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static void ApplyFonts(GameObject root)
    {
        TMP_FontAsset body = LoadFont("Inter-Medium SDF");
        TMP_FontAsset headline = LoadFont("Inter-Bold SDF");

        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            bool bold = (texts[i].fontStyle & FontStyles.Bold) != 0;
            if (bold && headline != null)
            {
                texts[i].font = headline;
                texts[i].fontStyle &= ~FontStyles.Bold;
            }
            else if (body != null)
            {
                texts[i].font = body;
            }
        }

        Tooltip tooltip = root.GetComponent<Tooltip>();
        if (tooltip == null) return;

        SerializedObject so = new SerializedObject(tooltip);
        SerializedProperty bodyProp = so.FindProperty("bodyFont");
        SerializedProperty titleProp = so.FindProperty("titleFont");
        if (bodyProp != null && body != null) bodyProp.objectReferenceValue = body;
        if (titleProp != null && headline != null) titleProp.objectReferenceValue = headline;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateCanvas(GameObject root)
    {
        GameObject canvasObj = new GameObject("TooltipCanvas");
        canvasObj.transform.SetParent(root.transform, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
        canvasRect.localScale = Vector3.one * 0.001f;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        canvasObj.AddComponent<GraphicRaycaster>();

        return canvasObj;
    }

    private static GameObject CreatePanelBorder(GameObject canvasObj)
    {
        GameObject borderObj = new GameObject("PanelBorder");
        borderObj.transform.SetParent(canvasObj.transform, false);

        RectTransform rect = borderObj.AddComponent<RectTransform>();
        PanelBuilderUI.StretchFull(rect);

        Image img = borderObj.AddComponent<Image>();
        img.sprite = RoundedSprite.Get(12);
        img.type = Image.Type.Sliced;
        img.color = PanelBorder;

        return borderObj;
    }

    private static GameObject CreatePanelRoot(GameObject borderObj)
    {
        GameObject panelRoot = new GameObject("PanelRoot");
        panelRoot.transform.SetParent(borderObj.transform, false);

        RectTransform rect = panelRoot.AddComponent<RectTransform>();
        PanelBuilderUI.StretchFull(rect);
        rect.offsetMin = new Vector2(2f, 2f);
        rect.offsetMax = new Vector2(-2f, -2f);

        Image img = panelRoot.AddComponent<Image>();
        img.sprite = RoundedSprite.Get(12);
        img.type = Image.Type.Sliced;
        img.color = PanelBg;

        return panelRoot;
    }

    private static GameObject CreateGroup(RectTransform parent, string name)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);

        RectTransform rect = group.AddComponent<RectTransform>();
        PanelBuilderUI.StretchFull(rect);

        return group;
    }

    private static void PopulateCellGroup(RectTransform parent)
    {
        float y = -6f;

        CreateText(parent, "CellTitle", "Cell",
            LeftPad + 30f, y, ContentWidth - 30f, TitleHeight, 12f, PrimaryText, FontStyles.Normal, wrap: true);
        y -= TitleHeight + MetaTokens.Spacing;

        CreateStackRow(parent, "Column", "ColumnValue", "Revenue", ref y);
        CreateStackRow(parent, "Row", "RowValue", "2020", ref y);
        CreateStackRow(parent, "Value", "ValueValue", "1,234", ref y);
    }

    private static void CreateStackRow(RectTransform parent, string labelText,
        string valueName, string placeholder, ref float y)
    {
        float rowHeight = 18f;

        CreateText(parent, labelText + "Label", labelText,
            LeftPad, y, 64f, rowHeight, 12f, SecondaryText, FontStyles.Normal);

        CreateText(parent, valueName, placeholder,
            LeftPad + 64f, y, ContentWidth - 64f, rowHeight, 12f, PrimaryText, FontStyles.Normal);

        y -= rowHeight + 8f;
    }

    private static void PopulateHintGroup(RectTransform parent)
    {
        float y = -6f;

        CreateText(parent, "HintTitle", "Inspect",
            LeftPad, y, ContentWidth, TitleHeight, 12f, PrimaryText, FontStyles.Normal, wrap: true);
        y -= TitleHeight + MetaTokens.Spacing;

        CreateText(parent, "HintBody", "Explanation of what this button does.",
            LeftPad, y, ContentWidth, 96f, 12f, SecondaryText, FontStyles.Normal, wrap: true);
    }

    private static void PopulateHeaderGroup(RectTransform parent)
    {
        float y = -6f;

        CreateText(parent, "HeaderTitle", "Revenue",
            LeftPad, y, ContentWidth, TitleHeight, 12f, PrimaryText, FontStyles.Normal, wrap: true);
        y -= TitleHeight + MetaTokens.Spacing;

        CreateLocatedRow(parent, "Max", "HeaderMax", "HeaderMaxLoc", "0", "2021", ref y);
        CreateLocatedRow(parent, "Min", "HeaderMin", "HeaderMinLoc", "0", "2014", ref y);
        CreateStackRow(parent, "Mean", "HeaderMean", "0", ref y);
        CreateStackRow(parent, "Cells", "HeaderCells", "0", ref y);
    }

    private static void CreateLocatedRow(RectTransform parent, string labelText,
        string valueName, string locName, string valuePlaceholder, string locPlaceholder, ref float y)
    {
        float rowHeight = 18f;

        CreateText(parent, labelText + "Label", labelText,
            LeftPad, y, 64f, rowHeight, 12f, SecondaryText, FontStyles.Normal);

        CreateText(parent, valueName, valuePlaceholder,
            LeftPad + 64f, y, 72f, rowHeight, 12f, PrimaryText, FontStyles.Normal);

        CreateText(parent, locName, locPlaceholder,
            LeftPad + 136f, y, ContentWidth - 136f, rowHeight, 12f, SecondaryText, FontStyles.Normal);

        y -= rowHeight + 8f;
    }

    private static void PopulateSheetGroup(RectTransform parent)
    {
        float y = -6f;

        CreateText(parent, "SheetTitle", "Sheet",
            LeftPad + 30f, y, ContentWidth - 30f, TitleHeight, 12f, PrimaryText, FontStyles.Normal, wrap: true);
        y -= TitleHeight + MetaTokens.Spacing;

        CreateLocatedRow(parent, "Maximum", "SheetMax", "SheetMaxLoc", "0", "Revenue  •  2021", ref y);
        CreateLocatedRow(parent, "Minimum", "SheetMin", "SheetMinLoc", "0", "Cost  •  2014", ref y);
        CreateStackRow(parent, "Mean", "SheetMean", "0", ref y);
        CreateStackRow(parent, "Cells", "SheetCells", "0", ref y);
    }

    private static void CreateSwatch(RectTransform parent, float x, float y)
    {
        GameObject obj = new GameObject("ColorSwatch");
        obj.transform.SetParent(parent, false);

        Image img = obj.AddComponent<Image>();
        img.sprite = RoundedSprite.Get(12);
        img.type = Image.Type.Sliced;
        img.color = AccentColor;

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y - 2f);
        rect.sizeDelta = new Vector2(20f, 20f);
    }

    private static void CreateText(RectTransform parent, string name, string placeholder,
        float x, float y, float width, float height, float fontSize,
        Color color, FontStyles style, bool wrap = false)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = placeholder;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = wrap ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left;
        tmp.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
        tmp.enableWordWrapping = wrap;
        tmp.raycastTarget = false;

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

#endif
}
