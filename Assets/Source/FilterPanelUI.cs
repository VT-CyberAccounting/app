using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Oculus.Interaction;

public class FilterPanelUI : MonoBehaviour
{
    #region Inspector Fields

    [Header("Panel Settings")]
    public float panelWidth = 0.5f;
    public float panelHeight = 0.6f;
    public float distanceFromCamera = 1.5f;

    [Header("Appearance")]
    [Range(0.1f, 1.0f)]
    public float panelAlpha = 0.92f;

    [Header("References")]
    public SurfaceFilterController filterController;

    #endregion

    #region Private Fields

    private Canvas _canvas;
    private RectTransform _canvasRect;
    private GameObject _panelRoot;
    private Image _panelBgImage;
    private Image _titleBarImage;
    private Sprite _roundedSprite;

    private List<GameObject> _tabButtons = new List<GameObject>();
    private List<FilterTabContent> _tabContents = new List<FilterTabContent>();
    private int _activeTabIndex = 0;

    private static readonly Color _tabActiveText = new Color(0f, 0.82f, 0.9f, 1f);
    private static readonly Color _tabActiveBar = new Color(0f, 0.82f, 0.9f, 1f);
    private static readonly Color _tabInactiveText = new Color(0.42f, 0.44f, 0.52f, 1f);
    private static readonly Color _titleText = new Color(0.91f, 0.92f, 0.94f, 1f);
    private static readonly Color _resetText = new Color(0.92f, 0.63f, 0.2f, 1f);
    private static readonly Color _closeIcon = new Color(0.89f, 0.29f, 0.29f, 1f);

    private static readonly string[] _tabNames = { "Columns", "Industries", "Years", "Countries" };

    #endregion

    #region Alpha-Dependent Colors

    private Color PanelBg => new Color(0.07f, 0.086f, 0.118f, panelAlpha);
    private Color TitleBarBg => new Color(0.098f, 0.118f, 0.157f, Mathf.Min(panelAlpha + 0.03f, 1f));
    private Color PanelBorder => new Color(0f, 0.82f, 0.9f, 0.25f * panelAlpha);
    private Color Divider => new Color(1f, 1f, 1f, 0.06f * panelAlpha);
    private Color TabActiveBg => new Color(0f, 0.82f, 0.9f, 0.05f * panelAlpha);
    private Color ResetBg => new Color(0.92f, 0.63f, 0.2f, 0.12f * panelAlpha);
    private Color ResetBorder => new Color(0.92f, 0.63f, 0.2f, 0.3f * panelAlpha);
    private Color CloseBg => new Color(0.89f, 0.29f, 0.29f, 0.15f * panelAlpha);

    #endregion

    #region Lifecycle

    private void Start()
    {
        StartCoroutine(WaitAndBuild());
    }

    private System.Collections.IEnumerator WaitAndBuild()
    {
        while (CSVDataManager.Instance == null || !CSVDataManager.Instance.IsLoaded)
            yield return null;

        while (filterController == null || SurfaceFilterController.Instance == null)
            yield return null;

        _roundedSprite = CreateRoundedSprite(12);
        BuildPanel();
        PositionInFrontOfCamera();
    }

    #endregion

    #region Rounded Sprite Generation

    private Sprite CreateRoundedSprite(int radius)
    {
        int size = radius * 2 + 2;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color white = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = 0f;
                float dy = 0f;

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
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    #endregion

    #region Panel Construction

    private void BuildPanel()
    {
        CreateCanvas();
        CreatePanelRoot();
        CreateTitleBar();
        CreateTabBar();
        CreateTabContents();
        SwitchToTab(0);
    }

    private void CreateCanvas()
    {
        GameObject canvasObj = new GameObject("FilterCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);

        _canvas = canvasObj.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;

        _canvasRect = canvasObj.GetComponent<RectTransform>();
        _canvasRect.sizeDelta = new Vector2(panelWidth * 1000f, panelHeight * 1000f);
        _canvasRect.localScale = Vector3.one * 0.001f;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        BoxCollider collider = canvasObj.AddComponent<BoxCollider>();
        collider.size = new Vector3(panelWidth * 1000f, panelHeight * 1000f, 1f);

        var pointable = canvasObj.AddComponent<Oculus.Interaction.PointableCanvas>();
        pointable.InjectCanvas(_canvas);
    }

    private void CreatePanelRoot()
    {
        GameObject borderObj = new GameObject("PanelBorder", typeof(RectTransform), typeof(Image));
        borderObj.transform.SetParent(_canvas.transform, false);

        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;

        Image borderImg = borderObj.GetComponent<Image>();
        borderImg.sprite = _roundedSprite;
        borderImg.type = Image.Type.Sliced;
        borderImg.color = PanelBorder;

        _panelRoot = new GameObject("PanelRoot", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        _panelRoot.transform.SetParent(borderObj.transform, false);

        RectTransform rect = _panelRoot.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(1f, 1f);
        rect.offsetMax = new Vector2(-1f, -1f);

        _panelBgImage = _panelRoot.GetComponent<Image>();
        _panelBgImage.sprite = _roundedSprite;
        _panelBgImage.type = Image.Type.Sliced;
        _panelBgImage.color = PanelBg;

        VerticalLayoutGroup vlg = _panelRoot.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 0f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
    }

    #endregion

    #region Title Bar

    private void CreateTitleBar()
    {
        GameObject titleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        titleBar.transform.SetParent(_panelRoot.transform, false);

        LayoutElement titleLE = titleBar.GetComponent<LayoutElement>();
        titleLE.minHeight = 48f;
        titleLE.preferredHeight = 48f;
        titleLE.flexibleHeight = 0f;

        _titleBarImage = titleBar.GetComponent<Image>();
        _titleBarImage.color = TitleBarBg;

        HorizontalLayoutGroup hlg = titleBar.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 10, 8, 8);
        hlg.spacing = 8f;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        CreateTitleLabel(titleBar.transform);
        CreateSpacer(titleBar.transform);
        CreatePillButton(titleBar.transform, "Reset all", ResetBg, ResetBorder, _resetText, 80f, OnResetAllClicked);
        CreateCloseButton(titleBar.transform);
    }

    private void CreateTitleLabel(Transform parent)
    {
        GameObject labelObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelObj.transform.SetParent(parent, false);

        LayoutElement le = labelObj.GetComponent<LayoutElement>();
        le.preferredWidth = 120f;

        TextMeshProUGUI text = labelObj.GetComponent<TextMeshProUGUI>();
        text.text = "Data filters";
        text.fontSize = 16f;
        text.fontStyle = FontStyles.Bold;
        text.color = _titleText;
        text.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private void CreateSpacer(Transform parent)
    {
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(parent, false);

        LayoutElement le = spacer.GetComponent<LayoutElement>();
        le.flexibleWidth = 1f;
    }

    private void CreatePillButton(Transform parent, string label, Color bg, Color border, Color textColor, float width, UnityEngine.Events.UnityAction onClick)
    {
        GameObject borderObj = new GameObject($"{label}_Border", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        borderObj.transform.SetParent(parent, false);

        LayoutElement borderLE = borderObj.GetComponent<LayoutElement>();
        borderLE.preferredWidth = width;

        Image borderImg = borderObj.GetComponent<Image>();
        borderImg.sprite = _roundedSprite;
        borderImg.type = Image.Type.Sliced;
        borderImg.color = border;

        GameObject btnObj = new GameObject($"{label}_Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(borderObj.transform, false);

        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = Vector2.zero;
        btnRect.anchorMax = Vector2.one;
        btnRect.offsetMin = new Vector2(1f, 1f);
        btnRect.offsetMax = new Vector2(-1f, -1f);

        Image btnBg = btnObj.GetComponent<Image>();
        btnBg.sprite = _roundedSprite;
        btnBg.type = Image.Type.Sliced;
        btnBg.color = bg;

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 12f;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.Center;

        Button btn = btnObj.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(onClick);
    }

    private void CreateCloseButton(Transform parent)
    {
        GameObject borderObj = new GameObject("Close_Border", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        borderObj.transform.SetParent(parent, false);

        LayoutElement le = borderObj.GetComponent<LayoutElement>();
        le.preferredWidth = 32f;

        Image borderImg = borderObj.GetComponent<Image>();
        borderImg.sprite = _roundedSprite;
        borderImg.type = Image.Type.Sliced;
        borderImg.color = CloseBg;

        GameObject btnObj = new GameObject("Close_Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(borderObj.transform, false);

        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = Vector2.zero;
        btnRect.anchorMax = Vector2.one;
        btnRect.offsetMin = new Vector2(1f, 1f);
        btnRect.offsetMax = new Vector2(-1f, -1f);

        Image btnBg = btnObj.GetComponent<Image>();
        btnBg.sprite = _roundedSprite;
        btnBg.type = Image.Type.Sliced;
        btnBg.color = CloseBg;

        GameObject textObj = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        text.text = "X";
        text.fontSize = 16f;
        text.fontStyle = FontStyles.Bold;
        text.color = _closeIcon;
        text.alignment = TextAlignmentOptions.Center;

        Button btn = btnObj.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(OnCloseClicked);
    }

    #endregion

    #region Tab Bar

    private void CreateTabBar()
    {
        GameObject tabBar = new GameObject("TabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        tabBar.transform.SetParent(_panelRoot.transform, false);

        LayoutElement tabLE = tabBar.GetComponent<LayoutElement>();
        tabLE.minHeight = 40f;
        tabLE.preferredHeight = 40f;
        tabLE.flexibleHeight = 0f;

        HorizontalLayoutGroup hlg = tabBar.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 0f;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        for (int i = 0; i < _tabNames.Length; i++)
        {
            int tabIndex = i;
            GameObject tabBtn = CreateTabButton(tabBar.transform, _tabNames[i], () => SwitchToTab(tabIndex));
            _tabButtons.Add(tabBtn);
        }

        GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        divider.transform.SetParent(_panelRoot.transform, false);

        LayoutElement divLE = divider.GetComponent<LayoutElement>();
        divLE.minHeight = 1f;
        divLE.preferredHeight = 1f;
        divLE.flexibleHeight = 0f;

        Image divImg = divider.GetComponent<Image>();
        divImg.color = Divider;
    }

    private GameObject CreateTabButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject tabObj = new GameObject($"Tab_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        tabObj.transform.SetParent(parent, false);

        Image tabBg = tabObj.GetComponent<Image>();
        tabBg.color = Color.clear;

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(tabObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 13f;
        text.color = _tabInactiveText;
        text.alignment = TextAlignmentOptions.Center;

        GameObject underline = new GameObject("Underline", typeof(RectTransform), typeof(Image));
        underline.transform.SetParent(tabObj.transform, false);

        RectTransform underRect = underline.GetComponent<RectTransform>();
        underRect.anchorMin = new Vector2(0.1f, 0f);
        underRect.anchorMax = new Vector2(0.9f, 0f);
        underRect.pivot = new Vector2(0.5f, 0f);
        underRect.sizeDelta = new Vector2(0f, 2f);

        Image underImg = underline.GetComponent<Image>();
        underImg.color = Color.clear;

        Button btn = tabObj.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(onClick);

        return tabObj;
    }

    #endregion

    #region Tab Content

    private void CreateTabContents()
    {
        CSVDataManager data = CSVDataManager.Instance;

        CreateColumnTab(data);
        CreateIndustryTab(data);
        CreateYearTab(data);
        CreateCountryTab(data);
    }

    private GameObject CreateTabContainer(string name)
    {
        GameObject container = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        container.transform.SetParent(_panelRoot.transform, false);

        LayoutElement le = container.GetComponent<LayoutElement>();
        le.flexibleHeight = 1f;

        return container;
    }

    private void CreateColumnTab(CSVDataManager data)
    {
        GameObject container = CreateTabContainer("ColumnsContent");
        FilterTabContent tab = container.AddComponent<FilterTabContent>();

        List<FilterTabContent.ToggleItem> items = new List<FilterTabContent.ToggleItem>();
        for (int i = 0; i < data.NumericColumnNames.Count; i++)
        {
            items.Add(new FilterTabContent.ToggleItem
            {
                Label = data.NumericColumnNames[i],
                Key = i.ToString(),
                StartActive = true
            });
        }

        tab.Initialize(items, OnColumnToggle, panelAlpha, _roundedSprite);
        _tabContents.Add(tab);
    }

    private void CreateIndustryTab(CSVDataManager data)
    {
        GameObject container = CreateTabContainer("IndustriesContent");
        FilterTabContent tab = container.AddComponent<FilterTabContent>();

        List<FilterTabContent.ToggleItem> items = new List<FilterTabContent.ToggleItem>();
        foreach (string industry in data.AllIndustries)
        {
            items.Add(new FilterTabContent.ToggleItem
            {
                Label = industry,
                Key = industry,
                StartActive = true
            });
        }

        tab.Initialize(items, OnIndustryToggle, panelAlpha, _roundedSprite);
        _tabContents.Add(tab);
    }

    private void CreateYearTab(CSVDataManager data)
    {
        GameObject container = CreateTabContainer("YearsContent");
        FilterTabContent tab = container.AddComponent<FilterTabContent>();

        List<FilterTabContent.ToggleItem> items = new List<FilterTabContent.ToggleItem>();
        foreach (string year in data.AllYears)
        {
            items.Add(new FilterTabContent.ToggleItem
            {
                Label = year,
                Key = year,
                StartActive = true
            });
        }

        tab.Initialize(items, OnYearToggle, panelAlpha, _roundedSprite);
        _tabContents.Add(tab);
    }

    private void CreateCountryTab(CSVDataManager data)
    {
        GameObject container = CreateTabContainer("CountriesContent");
        FilterTabContent tab = container.AddComponent<FilterTabContent>();

        List<FilterTabContent.ToggleItem> items = new List<FilterTabContent.ToggleItem>();
        foreach (string country in data.AllCountries)
        {
            items.Add(new FilterTabContent.ToggleItem
            {
                Label = country,
                Key = country,
                StartActive = true
            });
        }

        tab.Initialize(items, OnCountryToggle, panelAlpha, _roundedSprite);
        _tabContents.Add(tab);
    }

    #endregion

    #region Tab Switching

    private void SwitchToTab(int index)
    {
        _activeTabIndex = index;

        for (int i = 0; i < _tabButtons.Count; i++)
        {
            bool active = (i == index);
            GameObject tabBtn = _tabButtons[i];

            Image bg = tabBtn.GetComponent<Image>();
            bg.color = active ? TabActiveBg : Color.clear;

            TextMeshProUGUI text = tabBtn.GetComponentInChildren<TextMeshProUGUI>();
            text.color = active ? _tabActiveText : _tabInactiveText;
            text.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;

            Transform underline = tabBtn.transform.Find("Underline");
            if (underline != null)
            {
                Image underImg = underline.GetComponent<Image>();
                underImg.color = active ? _tabActiveBar : Color.clear;
            }
        }

        for (int i = 0; i < _tabContents.Count; i++)
            _tabContents[i].gameObject.SetActive(i == index);
    }

    #endregion

    #region Filter Callbacks

    private void OnColumnToggle(string key, bool active)
    {
        if (filterController == null) return;
        if (int.TryParse(key, out int colIndex))
            filterController.SetColumnVisible(colIndex, active);
    }

    private void OnIndustryToggle(string key, bool active)
    {
        if (filterController == null) return;
        filterController.SetIndustryActive(key, active);
    }

    private void OnYearToggle(string key, bool active)
    {
        if (filterController == null) return;
        filterController.SetYearActive(key, active);
    }

    private void OnCountryToggle(string key, bool active)
    {
        if (filterController == null) return;
        filterController.SetCountryActive(key, active);
    }

    #endregion

    #region Title Bar Actions

    private void OnResetAllClicked()
    {
        if (filterController != null)
            filterController.ResetAllFilters();

        SwitchToTab(0);
        RebuildAllTabStates();
    }

    private void OnCloseClicked()
    {
        _canvas.gameObject.SetActive(false);
    }

    #endregion

    #region Public Methods

    public void ShowPanel()
    {
        if (_canvas == null) return;
        _canvas.gameObject.SetActive(true);
        PositionInFrontOfCamera();
    }

    public void HidePanel()
    {
        if (_canvas == null) return;
        _canvas.gameObject.SetActive(false);
    }

    public void TogglePanel()
    {
        if (_canvas == null) return;

        if (_canvas.gameObject.activeSelf)
            HidePanel();
        else
            ShowPanel();
    }

    public bool IsVisible => _canvas != null && _canvas.gameObject.activeSelf;

    #endregion

    #region Positioning

    private void PositionInFrontOfCamera()
    {
        Transform cam = Camera.main?.transform;
        if (cam == null) return;

        Vector3 forward = cam.forward;
        forward.y = 0f;
        forward.Normalize();

        transform.position = cam.position + forward * distanceFromCamera + Vector3.up * 0.1f;
        transform.rotation = Quaternion.LookRotation(forward);
    }

    #endregion

    #region State Sync

    private void RebuildAllTabStates()
    {
        CSVDataManager data = CSVDataManager.Instance;
        if (data == null) return;

        if (_tabContents.Count > 0)
        {
            for (int i = 0; i < data.NumericColumnNames.Count; i++)
                _tabContents[0].SetToggleState(i.ToString(), filterController.IsColumnVisible(i));
        }

        if (_tabContents.Count > 1)
        {
            foreach (string ind in data.AllIndustries)
                _tabContents[1].SetToggleState(ind, filterController.IsIndustryActive(ind));
        }

        if (_tabContents.Count > 2)
        {
            foreach (string year in data.AllYears)
                _tabContents[2].SetToggleState(year, filterController.IsYearActive(year));
        }

        if (_tabContents.Count > 3)
        {
            foreach (string country in data.AllCountries)
                _tabContents[3].SetToggleState(country, filterController.IsCountryActive(country));
        }
    }

    #endregion
}