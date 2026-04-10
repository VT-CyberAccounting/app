using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class DataTooltipBuilder
{
#if UNITY_EDITOR

    private static readonly Color PanelBg = new Color(0.07f, 0.086f, 0.118f, 0.92f);
    private static readonly Color PanelBorder = new Color(0f, 0.82f, 0.9f, 0.23f);
    private static readonly Color AccentColor = new Color(0f, 0.82f, 0.9f, 1f);
    private static readonly Color PrimaryText = new Color(0.91f, 0.92f, 0.94f, 1f);
    private static readonly Color SecondaryText = new Color(0.42f, 0.44f, 0.52f, 1f);
    private static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.055f);

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

    [MenuItem("Tools/Build Data Tooltip")]
    public static void BuildDataTooltip()
    {
        GameObject existing = GameObject.Find("DataTooltip");
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Data Tooltip Exists",
                "A DataTooltip already exists in the scene. Replace it?",
                "Replace", "Cancel");
            if (!replace) return;
            Undo.DestroyObjectImmediate(existing);
        }

        _roundedSprite = null;

        GameObject root = new GameObject("DataTooltip");
        Undo.RegisterCreatedObjectUndo(root, "Build Data Tooltip");

        GameObject canvasObj = CreateCanvas(root);
        GameObject borderObj = CreatePanelBorder(canvasObj);
        GameObject panelObj = CreatePanelRoot(borderObj);

        float y = -12f;
        float leftPad = 14f;
        float contentWidth = 292f;

        CreateSwatch(panelObj.GetComponent<RectTransform>(), leftPad, y);

        CreateText(panelObj.GetComponent<RectTransform>(), "Ticker", "AAPL",
            leftPad + 30f, y, contentWidth - 30f, 24f, 16f, PrimaryText, FontStyles.Bold);
        y -= 24f;

        CreateText(panelObj.GetComponent<RectTransform>(), "Company", "Apple Inc.",
            leftPad, y, contentWidth, 20f, 12f, SecondaryText, FontStyles.Normal);
        y -= 26f;

        CreateDivider(panelObj.GetComponent<RectTransform>(), leftPad, y, contentWidth);
        y -= 10f;

        CreateLabeledRow(panelObj.GetComponent<RectTransform>(), "Industry", "IndustryValue", "Manufacturing", leftPad, ref y, contentWidth);
        CreateLabeledRow(panelObj.GetComponent<RectTransform>(), "Country", "CountryValue", "USA", leftPad, ref y, contentWidth);
        CreateLabeledRow(panelObj.GetComponent<RectTransform>(), "Year", "YearValue", "2020", leftPad, ref y, contentWidth);

        CreateDivider(panelObj.GetComponent<RectTransform>(), leftPad, y, contentWidth);
        y -= 10f;

        CreateLabeledRow(panelObj.GetComponent<RectTransform>(), "Metric", "ColumnValue", "Total Assets", leftPad, ref y, contentWidth);
        CreateLabeledRow(panelObj.GetComponent<RectTransform>(), "Value", "ValueValue", "1.2 billion", leftPad, ref y, contentWidth);

        Selection.activeGameObject = root;
        Debug.Log("[DataTooltipBuilder] Data tooltip built successfully.");
    }

    private static GameObject CreateCanvas(GameObject root)
    {
        GameObject canvasObj = new GameObject("TooltipCanvas");
        canvasObj.transform.SetParent(root.transform, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(320f, 260f);
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

        return panelRoot;
    }

    private static void CreateSwatch(RectTransform parent, float x, float y)
    {
        GameObject obj = new GameObject("ColorSwatch");
        obj.transform.SetParent(parent, false);

        Image img = obj.AddComponent<Image>();
        img.sprite = GetRoundedSprite();
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
        Color color, FontStyles style)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = placeholder;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.enableWordWrapping = false;

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void CreateDivider(RectTransform parent, float x, float y, float width)
    {
        GameObject obj = new GameObject("Divider");
        obj.transform.SetParent(parent, false);

        Image img = obj.AddComponent<Image>();
        img.color = DividerColor;

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, 1f);
    }

    private static void CreateLabeledRow(RectTransform parent, string labelText, string valueName,
        string placeholder, float x, ref float y, float contentWidth)
    {
        float rowHeight = 22f;

        CreateText(parent, labelText + "Label", labelText,
            x, y, contentWidth * 0.35f, rowHeight, 12f, SecondaryText, FontStyles.Normal);

        CreateText(parent, valueName, placeholder,
            x + contentWidth * 0.35f, y, contentWidth * 0.65f, rowHeight, 13f, PrimaryText, FontStyles.Normal);

        y -= rowHeight + 4f;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

#endif
}