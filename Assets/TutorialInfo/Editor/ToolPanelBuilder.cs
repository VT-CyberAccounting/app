using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ToolPanelBuilder
{
#if UNITY_EDITOR

    private const float RailHPadding = MetaTokens.PanelGutter;
    private const float RailWidth = MetaTokens.ToolButtonWidth + RailHPadding * 2f;

    private static readonly Color RailBg = MetaTokens.Alpha(MetaTokens.White, 0.03f);
    private static readonly Color ResetBg = MetaTokens.Alpha(MetaTokens.White, 0.05f);
    private static readonly Color ResetBorder = MetaTokens.SheetAlt;
    private static readonly Color ResetText = MetaTokens.NeutralC0;
    private static readonly Color HeaderText = MetaTokens.TextPrimary;
    private static readonly Color BodyText = MetaTokens.NeutralC0;
    private static readonly Color HintTextColor = MetaTokens.Neutral8E;

    [MenuItem("Tools/Build Tool Panel")]
    public static void BuildToolPanel()
    {
        GameObject root = PanelBuilderUI.PrepareRoot("Tool Panel");
        if (root == null) return;

        ToolController controller = PanelBuilderUI.GetOrAddComponent<ToolController>(root);
        ToolPanelUI panelUI = PanelBuilderUI.GetOrAddComponent<ToolPanelUI>(root);
        WireToolPanelReferences(panelUI, controller);

        PanelBuilderUI.PanelShell shell = PanelBuilderUI.BuildShell(root, "ToolCanvas", "Tool Panel");
        GameObject contentArea = CreateBody(shell.panelRoot);
        CreateToolContents(contentArea);
        PanelBuilderUI.FinishShell(root, shell.canvas, "[ToolPanelBuilder]");
        PanelBuilderUI.RewireWatchReferences(null, panelUI);

        Debug.Log("[ToolPanelBuilder] Tool panel built successfully.");
    }

    private static void WireToolPanelReferences(ToolPanelUI panelUI, ToolController controller)
    {
        SerializedObject so = new SerializedObject(panelUI);
        SerializedProperty controllerProp = so.FindProperty("toolController");
        if (controllerProp != null) controllerProp.objectReferenceValue = controller;

        DataPanelUI dataPanel = Object.FindAnyObjectByType<DataPanelUI>();
        if (dataPanel != null)
        {
            SerializedProperty dataPanelProp = so.FindProperty("dataPanelUI");
            if (dataPanelProp != null) dataPanelProp.objectReferenceValue = dataPanel;
        }
        else
        {
            Debug.LogWarning("[ToolPanelBuilder] No DataPanelUI found in scene; leave ToolPanelUI.dataPanelUI unassigned until one exists.");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateBody(GameObject panelRoot)
    {
        GameObject body = new GameObject("Body");
        body.transform.SetParent(panelRoot.transform, false);
        body.AddComponent<RectTransform>();

        LayoutElement le = body.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f;

        HorizontalLayoutGroup hlg = body.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 0f;
        hlg.padding = new RectOffset(0, 0, 0, 0);
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        CreateToolRail(body);
        return CreateContentArea(body);
    }

    private static void CreateToolRail(GameObject body)
    {
        GameObject rail = new GameObject("ToolRail");
        rail.transform.SetParent(body.transform, false);
        rail.AddComponent<RectTransform>();

        LayoutElement le = rail.AddComponent<LayoutElement>();
        le.preferredWidth = RailWidth;
        le.flexibleWidth = 0f;

        Image bg = rail.AddComponent<Image>();
        bg.color = RailBg;
        bg.raycastTarget = false;

        VerticalLayoutGroup vlg = rail.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = MetaTokens.Spacing;
        vlg.padding = new RectOffset((int)RailHPadding, (int)RailHPadding,
            (int)MetaTokens.PanelGutter, (int)MetaTokens.PanelGutter);
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childAlignment = TextAnchor.UpperCenter;

        for (int i = 0; i < ToolPanelConstants.Tools.Length; i++)
            CreateToolButton(rail.transform, ToolPanelConstants.Label(ToolPanelConstants.Tools[i]));
    }

    private static GameObject CreateContentArea(GameObject body)
    {
        GameObject contentArea = new GameObject("ContentArea");
        contentArea.transform.SetParent(body.transform, false);
        contentArea.AddComponent<RectTransform>();

        LayoutElement le = contentArea.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;

        return contentArea;
    }

    private static void CreateToolButton(Transform parent, string label)
    {
        UIButton.Create(parent, $"Tool_{label}", label,
            width: MetaTokens.ToolButtonWidth, fontSize: MetaTokens.Body1);
    }

    private static void CreateToolContents(GameObject contentArea)
    {
        CreateEmptyContent(contentArea);
        for (int i = 0; i < ToolPanelConstants.Tools.Length; i++)
            CreateToolContent(contentArea, ToolPanelConstants.Tools[i]);
    }

    private static GameObject CreateContentContainer(GameObject parent, string name)
    {
        GameObject container = new GameObject(name);
        container.transform.SetParent(parent.transform, false);

        RectTransform rt = container.AddComponent<RectTransform>();
        PanelBuilderUI.StretchFull(rt);

        VerticalLayoutGroup vlg = container.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = MetaTokens.Spacing;
        vlg.padding = new RectOffset((int)MetaTokens.PanelGutter, (int)MetaTokens.PanelGutter,
            (int)MetaTokens.Spacing, (int)MetaTokens.PanelGutter);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childAlignment = TextAnchor.UpperLeft;

        container.SetActive(false);
        return container;
    }

    private static void CreateEmptyContent(GameObject contentArea)
    {
        GameObject container = CreateContentContainer(contentArea, "EmptyContent");

        VerticalLayoutGroup vlg = container.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) vlg.childAlignment = TextAnchor.MiddleCenter;

        CreateLabel(container.transform, "Hint", HintText.NoToolSelected,
            MetaTokens.Headline3, FontStyles.Normal, HintTextColor, TextAlignmentOptions.Center, 0f, 1f);
    }

    private static void CreateToolContent(GameObject contentArea, ToolType tool)
    {
        string label = ToolPanelConstants.Label(tool);
        GameObject container = CreateContentContainer(contentArea, $"{label}Content");

        CreateLabel(container.transform, "Header", label,
            MetaTokens.ToolHeader, FontStyles.Normal, HeaderText, TextAlignmentOptions.MidlineLeft, 30f, 0f);

        CreateLabel(container.transform, "Description", ToolPanelConstants.Description(tool),
            MetaTokens.Body1, FontStyles.Normal, BodyText, TextAlignmentOptions.TopLeft, 60f, 0f);

        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(container.transform, false);
        spacer.AddComponent<RectTransform>();
        LayoutElement spacerLE = spacer.AddComponent<LayoutElement>();
        spacerLE.flexibleHeight = 1f;
    }

    private static void CreateLabel(Transform parent, string name, string content, float fontSize,
        FontStyles style, Color color, TextAlignmentOptions alignment, float preferredHeight, float flexibleHeight)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();

        LayoutElement le = obj.AddComponent<LayoutElement>();
        if (preferredHeight > 0f) le.preferredHeight = preferredHeight;
        if (flexibleHeight > 0f) le.flexibleHeight = flexibleHeight;

        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
    }

#endif
}
