using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class FilterPanelBuilder
{
#if UNITY_EDITOR

    private static readonly string[] NumericColumns = {
        "Environmental", "Social", "Governance", "ESG_score",
        "Current Assets", "Assets", "Cash", "Inventory",
        "Current Marketable Securities", "Current Liabilities",
        "Liabilities", "Property, Plant and Equipment",
        "Preferred/Preference Stock", "Allowance for Doubtful Receivables",
        "Total Receivables", "Stockholders Equity", "Cost of Goods Sold",
        "Dividends - Preferred/Preference", "Dividends",
        "Earnings Before Interest and Taxes", "Earnings Per Share (Basic)",
        "Net Income (Loss)", "Net Income Adjusted for common stocks",
        "Sales/Turnover (Net)", "Interest and Related Expense",
        "Common Shares Outstanding", "Total Debt Including Current",
        "Price Close - Annual -", "Net receivables",
        "Total assets last year", "Net receivables last year",
        "Inventory last year", "Stockholder equity last year",
        "Cost of Goods Sold last year", "Common shares outstanding last year"
    };

    private static readonly string[] Industries = {
        "Mining", "Construction", "Manufactuing",
        "Transportation Public Utilities", "Wholesale Trade",
        "Retail Trade", "Services"
    };

    private static readonly string[] Years = { "2019", "2020" };

    private static readonly string[] Countries = {
        "BMU", "CAN", "CUW", "CYM", "GBR", "IRL",
        "ISR", "LBR", "MHL", "NLD", "USA", "VGB"
    };

    private static readonly string[] TabNames = { "Columns", "Industries", "Years", "Countries" };

    private static readonly Color PanelBg = new Color(0.07f, 0.086f, 0.118f, 0.92f);
    private static readonly Color PanelBorder = new Color(0f, 0.82f, 0.9f, 0.23f);
    private static readonly Color TitleBarBg = new Color(0.098f, 0.118f, 0.157f, 0.95f);
    private static readonly Color TitleText = new Color(0.91f, 0.92f, 0.94f, 1f);
    private static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.055f);

    private static readonly Color TabActiveText = new Color(0f, 0.82f, 0.9f, 1f);
    private static readonly Color TabActiveBg = new Color(0f, 0.82f, 0.9f, 0.046f);
    private static readonly Color TabActiveBar = new Color(0f, 0.82f, 0.9f, 1f);
    private static readonly Color TabInactiveText = new Color(0.42f, 0.44f, 0.52f, 1f);

    private static readonly Color ResetBg = new Color(0.92f, 0.63f, 0.2f, 0.11f);
    private static readonly Color ResetBorder = new Color(0.92f, 0.63f, 0.2f, 0.28f);
    private static readonly Color ResetText = new Color(0.92f, 0.63f, 0.2f, 1f);

    private static readonly Color CloseBg = new Color(0.89f, 0.29f, 0.29f, 0.14f);
    private static readonly Color CloseText = new Color(0.89f, 0.29f, 0.29f, 1f);

    private static readonly Color ActiveRowBg = new Color(0f, 0.82f, 0.9f, 0.092f);
    private static readonly Color ActiveTrack = new Color(0f, 0.82f, 0.9f, 1f);
    private static readonly Color ActiveTextColor = new Color(0.91f, 0.92f, 0.94f, 1f);
    private static readonly Color ActiveKnob = Color.white;

    private static readonly Color InactiveRowBg = new Color(1f, 1f, 1f, 0.028f);
    private static readonly Color InactiveTrack = new Color(0.16f, 0.18f, 0.23f, 0.92f);
    private static readonly Color InactiveText = new Color(0.31f, 0.33f, 0.41f, 1f);
    private static readonly Color InactiveKnob = new Color(0.31f, 0.33f, 0.41f, 1f);

    private static readonly Color BulkBtnBg = new Color(0f, 0f, 0f, 0.01f);
    private static readonly Color BulkBtnBorder = new Color(1f, 1f, 1f, 0.11f);
    private static readonly Color BulkBtnText = new Color(0.61f, 0.63f, 0.71f, 1f);

    private static Sprite _roundedSprite;

    private static Sprite GetRoundedSprite()
    {
        if (_roundedSprite != null) return _roundedSprite;

        int radius = 12;
        int size = radius * 2 + 2;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color white = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = 0f, dy = 0f;
                if (x < radius) dx = radius - x;
                else if (x >= size - radius) dx = x - (size - radius - 1);
                if (y < radius) dy = radius - y;
                else if (y >= size - radius) dy = y - (size - radius - 1);

                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= radius)
                    tex.SetPixel(x, y, white);
                else if (dist <= radius + 1f)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f - (dist - radius)));
                else
                    tex.SetPixel(x, y, clear);
            }
        }

        tex.Apply();
        Vector4 border = new Vector4(radius, radius, radius, radius);
        _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        return _roundedSprite;
    }

    [MenuItem("Tools/Build Filter Panel")]
    public static void BuildFilterPanel()
    {
        GameObject existing = GameObject.Find("FilterPanel");
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Filter Panel Exists",
                "A FilterPanel already exists in the scene. Replace it?",
                "Replace", "Cancel");
            if (!replace) return;
            Undo.DestroyObjectImmediate(existing);
        }

        _roundedSprite = null;

        GameObject root = new GameObject("FilterPanel");
        Undo.RegisterCreatedObjectUndo(root, "Build Filter Panel");

        GameObject canvasObj = CreateCanvas(root);
        GameObject borderObj = CreatePanelBorder(canvasObj);
        GameObject panelRoot = CreatePanelRoot(borderObj);
        CreateTitleBar(panelRoot);
        CreateTabBar(panelRoot);
        CreateDivider(panelRoot);
        CreateTabContents(panelRoot);

        Selection.activeGameObject = root;
        Debug.Log("[FilterPanelBuilder] Filter panel built successfully.");
    }

    private static GameObject CreateCanvas(GameObject root)
    {
        GameObject canvasObj = new GameObject("FilterCanvas");
        canvasObj.transform.SetParent(root.transform, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(500f, 600f);
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
        StretchFull(rect);

        Image img = borderObj.AddComponent<Image>();
        img.sprite = GetRoundedSprite();
        img.type = Image.Type.Sliced;
        img.color = PanelBorder;

        return borderObj;
    }

    private static GameObject CreatePanelRoot(GameObject borderObj)
    {
        GameObject panelRoot = new GameObject("PanelRoot");
        panelRoot.transform.SetParent(borderObj.transform, false);

        RectTransform rect = panelRoot.AddComponent<RectTransform>();
        StretchFull(rect);
        rect.offsetMin = new Vector2(1f, 1f);
        rect.offsetMax = new Vector2(-1f, -1f);

        Image img = panelRoot.AddComponent<Image>();
        img.sprite = GetRoundedSprite();
        img.type = Image.Type.Sliced;
        img.color = PanelBg;

        VerticalLayoutGroup vlg = panelRoot.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 0f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        return panelRoot;
    }

    private static void CreateTitleBar(GameObject panelRoot)
    {
        GameObject titleBar = new GameObject("TitleBar");
        titleBar.transform.SetParent(panelRoot.transform, false);
        titleBar.AddComponent<RectTransform>();

        LayoutElement le = titleBar.AddComponent<LayoutElement>();
        le.minHeight = 48f;
        le.preferredHeight = 48f;
        le.flexibleHeight = 0f;

        Image bg = titleBar.AddComponent<Image>();
        bg.color = TitleBarBg;

        HorizontalLayoutGroup hlg = titleBar.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 10, 8, 8);
        hlg.spacing = 8f;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        CreateTextElement(titleBar.transform, "Title", "Data filters", 16f, FontStyles.Bold, TitleText, TextAlignmentOptions.MidlineLeft, 120f);

        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(titleBar.transform, false);
        spacer.AddComponent<RectTransform>();
        LayoutElement spacerLE = spacer.AddComponent<LayoutElement>();
        spacerLE.flexibleWidth = 1f;

        CreatePillButton(titleBar.transform, "Reset all", ResetBg, ResetBorder, ResetText, 80f);
        CreatePillButton(titleBar.transform, "X", CloseBg, CloseBg, CloseText, 32f, 16f, FontStyles.Bold);
    }

    private static void CreateTabBar(GameObject panelRoot)
    {
        GameObject tabBar = new GameObject("TabBar");
        tabBar.transform.SetParent(panelRoot.transform, false);
        tabBar.AddComponent<RectTransform>();

        LayoutElement le = tabBar.AddComponent<LayoutElement>();
        le.minHeight = 40f;
        le.preferredHeight = 40f;
        le.flexibleHeight = 0f;

        HorizontalLayoutGroup hlg = tabBar.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 0f;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        for (int i = 0; i < TabNames.Length; i++)
            CreateTabButton(tabBar.transform, TabNames[i], i == 0);
    }

    private static void CreateTabButton(Transform parent, string label, bool isActive)
    {
        GameObject tabObj = new GameObject($"Tab_{label}");
        tabObj.transform.SetParent(parent, false);
        tabObj.AddComponent<RectTransform>();

        Image bg = tabObj.AddComponent<Image>();
        bg.color = isActive ? TabActiveBg : Color.clear;

        tabObj.AddComponent<Button>().transition = Selectable.Transition.None;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(tabObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        StretchFull(textRect);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 13f;
        text.color = isActive ? TabActiveText : TabInactiveText;
        text.fontStyle = isActive ? FontStyles.Bold : FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Center;

        GameObject underline = new GameObject("Underline");
        underline.transform.SetParent(tabObj.transform, false);

        RectTransform underRect = underline.AddComponent<RectTransform>();
        underRect.anchorMin = new Vector2(0.1f, 0f);
        underRect.anchorMax = new Vector2(0.9f, 0f);
        underRect.pivot = new Vector2(0.5f, 0f);
        underRect.sizeDelta = new Vector2(0f, 2f);

        Image underImg = underline.AddComponent<Image>();
        underImg.color = isActive ? TabActiveBar : Color.clear;
    }

    private static void CreateDivider(GameObject panelRoot)
    {
        GameObject divider = new GameObject("Divider");
        divider.transform.SetParent(panelRoot.transform, false);
        divider.AddComponent<RectTransform>();

        LayoutElement le = divider.AddComponent<LayoutElement>();
        le.minHeight = 1f;
        le.preferredHeight = 1f;
        le.flexibleHeight = 0f;

        Image img = divider.AddComponent<Image>();
        img.color = DividerColor;
    }

    private static void CreateTabContents(GameObject panelRoot)
    {
        CreateToggleTab(panelRoot, "ColumnsContent", NumericColumns, true);
        CreateToggleTab(panelRoot, "IndustriesContent", Industries, false);
        CreateToggleTab(panelRoot, "YearsContent", Years, false);
        CreateToggleTab(panelRoot, "CountriesContent", Countries, false);
    }

    private static void CreateToggleTab(GameObject panelRoot, string name, string[] items, bool visible)
    {
        GameObject container = new GameObject(name);
        container.transform.SetParent(panelRoot.transform, false);
        container.AddComponent<RectTransform>();

        LayoutElement le = container.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f;

        container.SetActive(visible);

        GameObject scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(container.transform, false);

        RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
        StretchFull(scrollRect);

        Image scrollBg = scrollObj.AddComponent<Image>();
        scrollBg.color = Color.clear;

        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollObj.transform, false);

        RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
        StretchFull(viewportRect);

        Image viewportImg = viewportObj.AddComponent<Image>();
        viewportImg.color = Color.white;

        Mask mask = viewportObj.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        scroll.viewport = viewportRect;

        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);

        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(12f, 0f);
        contentRect.offsetMax = new Vector2(-12f, 0f);

        VerticalLayoutGroup vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(0, 0, 8, 12);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;

        CreateBulkButtons(contentObj.transform);

        for (int i = 0; i < items.Length; i++)
        {
            string key = (name == "ColumnsContent") ? i.ToString() : items[i];
            CreateToggleRow(contentObj.transform, items[i], key, true);
        }
    }

    private static void CreateBulkButtons(Transform contentParent)
    {
        GameObject row = new GameObject("BulkButtons");
        row.transform.SetParent(contentParent, false);
        row.AddComponent<RectTransform>();

        LayoutElement le = row.AddComponent<LayoutElement>();
        le.minHeight = 36f;
        le.preferredHeight = 36f;
        le.flexibleHeight = 0f;

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.padding = new RectOffset(0, 0, 2, 2);
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        CreateBulkButton(row.transform, "Select all", 110f);
        CreateBulkButton(row.transform, "Deselect all", 110f);
    }

    private static void CreateBulkButton(Transform parent, string label, float width)
    {
        GameObject btnObj = new GameObject(label);
        btnObj.transform.SetParent(parent, false);
        btnObj.AddComponent<RectTransform>();

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.preferredWidth = width;

        Image bg = btnObj.AddComponent<Image>();
        bg.sprite = GetRoundedSprite();
        bg.type = Image.Type.Sliced;
        bg.color = BulkBtnBorder;

        btnObj.AddComponent<Button>().transition = Selectable.Transition.None;

        GameObject innerObj = new GameObject("Inner");
        innerObj.transform.SetParent(btnObj.transform, false);

        RectTransform innerRect = innerObj.AddComponent<RectTransform>();
        StretchFull(innerRect);
        innerRect.offsetMin = new Vector2(1f, 1f);
        innerRect.offsetMax = new Vector2(-1f, -1f);

        Image innerBg = innerObj.AddComponent<Image>();
        innerBg.sprite = GetRoundedSprite();
        innerBg.type = Image.Type.Sliced;
        innerBg.color = BulkBtnBg;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        StretchFull(textRect);
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 12f;
        text.color = BulkBtnText;
        text.alignment = TextAlignmentOptions.Center;
    }

    private static void CreateToggleRow(Transform contentParent, string label, string key, bool startActive)
    {
        GameObject rowObj = new GameObject($"Toggle_{key}");
        rowObj.transform.SetParent(contentParent, false);
        rowObj.AddComponent<RectTransform>();

        LayoutElement le = rowObj.AddComponent<LayoutElement>();
        le.minHeight = 44f;
        le.preferredHeight = 44f;
        le.flexibleHeight = 0f;

        Image rowBg = rowObj.AddComponent<Image>();
        rowBg.sprite = GetRoundedSprite();
        rowBg.type = Image.Type.Sliced;
        rowBg.color = startActive ? ActiveRowBg : InactiveRowBg;

        rowObj.AddComponent<Button>().transition = Selectable.Transition.None;

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(rowObj.transform, false);

        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(16f, 0f);
        labelRect.offsetMax = new Vector2(-60f, 0f);

        TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 14f;
        labelText.color = startActive ? ActiveTextColor : InactiveText;
        labelText.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject trackObj = new GameObject("Track");
        trackObj.transform.SetParent(rowObj.transform, false);

        RectTransform trackRect = trackObj.AddComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(1f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.pivot = new Vector2(1f, 0.5f);
        trackRect.anchoredPosition = new Vector2(-14f, 0f);
        trackRect.sizeDelta = new Vector2(40f, 22f);

        Image trackImg = trackObj.AddComponent<Image>();
        trackImg.sprite = GetRoundedSprite();
        trackImg.type = Image.Type.Sliced;
        trackImg.color = startActive ? ActiveTrack : InactiveTrack;

        GameObject knobObj = new GameObject("Knob");
        knobObj.transform.SetParent(trackObj.transform, false);

        RectTransform knobRect = knobObj.AddComponent<RectTransform>();
        knobRect.sizeDelta = new Vector2(16f, 16f);
        knobRect.anchorMin = new Vector2(0f, 0.5f);
        knobRect.anchorMax = new Vector2(0f, 0.5f);
        knobRect.pivot = new Vector2(0f, 0.5f);
        knobRect.anchoredPosition = startActive ? new Vector2(21f, 0f) : new Vector2(3f, 0f);

        Image knobImg = knobObj.AddComponent<Image>();
        knobImg.sprite = GetRoundedSprite();
        knobImg.type = Image.Type.Sliced;
        knobImg.color = startActive ? ActiveKnob : InactiveKnob;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void CreateTextElement(Transform parent, string name, string content, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment, float preferredWidth)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.preferredWidth = preferredWidth;

        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
    }

    private static void CreatePillButton(Transform parent, string label, Color bg, Color border, Color textColor, float width, float fontSize = 12f, FontStyles fontStyle = FontStyles.Normal)
    {
        GameObject borderObj = new GameObject($"{label}_Border");
        borderObj.transform.SetParent(parent, false);
        borderObj.AddComponent<RectTransform>();

        LayoutElement le = borderObj.AddComponent<LayoutElement>();
        le.preferredWidth = width;

        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.sprite = GetRoundedSprite();
        borderImg.type = Image.Type.Sliced;
        borderImg.color = border;

        GameObject btnObj = new GameObject($"{label}_Btn");
        btnObj.transform.SetParent(borderObj.transform, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        StretchFull(btnRect);
        btnRect.offsetMin = new Vector2(1f, 1f);
        btnRect.offsetMax = new Vector2(-1f, -1f);

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.sprite = GetRoundedSprite();
        btnBg.type = Image.Type.Sliced;
        btnBg.color = bg;

        btnObj.AddComponent<Button>().transition = Selectable.Transition.None;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        StretchFull(textRect);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.Center;
    }

#endif
}