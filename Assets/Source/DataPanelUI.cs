using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;

public class DataPanelUI : PanelUI
{
    [FormerlySerializedAs("filterController")]
    [FormerlySerializedAs("dataController")]
    [UnityEngine.Serialization.FormerlySerializedAs("regionController")]
    public SheetController sheetController;
    [UnityEngine.Serialization.FormerlySerializedAs("toolPanel")]
    public ToolPanelUI toolPanelUI;

    private static readonly Color RailBg = MetaTokens.Alpha(MetaTokens.White, 0.03f);
    private static readonly Color PromptText = MetaTokens.Neutral8E;
    private static readonly float RailWidth = MetaTokens.ToolButtonWidth + MetaTokens.PanelGutter * 2f;

    private DatasetManager _datasets;
    private DatasetRail _rail;
    private FilterContent _content;

    private Transform _contentSlot;
    private GameObject _filterRoot;
    private GameObject _emptyPrompt;
    private TextMeshProUGUI _emptyPromptLabel;
    private LockShade _contentShade;
    private LockShade _resetShade;
    private bool _collapsed;
    private bool _locked;

    private void Start()
    {
        _canvas = GetComponentInChildren<Canvas>(true);
        ApplyPanelSize();
        _panelGrab = GetComponent<PanelGrab>();
        if (_panelGrab != null) _panelGrab.SetGrabbable(false);

        _datasets = DatasetManager.Instance;

        BuildLayout();
        BindTitleBar();
        ApplyFonts();

        if (_datasets != null)
        {
            _datasets.OnTabsChanged += UpdateEmptyState;
            _datasets.OnActiveTabChanged += OnActiveDatasetChanged;
        }

        DataSource source = _datasets != null ? _datasets.Active : null;
        if (source == null && sheetController != null) source = sheetController.DataSource;
        Rebind(source);
        UpdateEmptyState();

        if (_canvas != null) _canvas.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_datasets != null)
        {
            _datasets.OnTabsChanged -= UpdateEmptyState;
            _datasets.OnActiveTabChanged -= OnActiveDatasetChanged;
        }
        if (_rail != null) _rail.Dispose();
        if (_content != null) _content.Dispose();
    }

    // ---- layout --------------------------------------------------------

    private void BuildLayout()
    {
        Transform panelRoot = UITransformSearch.FindDeep(transform, "PanelRoot");
        if (panelRoot == null)
        {
            Debug.LogError("[DataPanelUI] PanelRoot not found; rebuild the Data Panel (Tools > Build Data Panel).");
            return;
        }

        for (int i = panelRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = panelRoot.GetChild(i);
            if (child.name != "TitleBar") Destroy(child.gameObject);
        }

        GameObject body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(panelRoot, false);
        HorizontalLayoutGroup bodyHlg = body.AddComponent<HorizontalLayoutGroup>();
        bodyHlg.spacing = 0f;
        bodyHlg.childControlWidth = true;
        bodyHlg.childControlHeight = true;
        bodyHlg.childForceExpandWidth = false;
        bodyHlg.childForceExpandHeight = true;
        LayoutElement bodyLE = body.AddComponent<LayoutElement>();
        bodyLE.flexibleHeight = 1f;

        GameObject railSlot = new GameObject("DatasetRail", typeof(RectTransform), typeof(Image));
        railSlot.transform.SetParent(body.transform, false);
        Image railImg = railSlot.GetComponent<Image>();
        railImg.color = RailBg;
        railImg.raycastTarget = false;
        LayoutElement railLE = railSlot.AddComponent<LayoutElement>();
        railLE.preferredWidth = RailWidth;
        railLE.flexibleWidth = 0f;

        GameObject contentSlot = new GameObject("Content", typeof(RectTransform));
        contentSlot.transform.SetParent(body.transform, false);
        LayoutElement contentLE = contentSlot.AddComponent<LayoutElement>();
        contentLE.flexibleWidth = 1f;
        _contentSlot = contentSlot.transform;

        _filterRoot = NewStretchChild(contentSlot.transform, "FilterRoot");
        _emptyPrompt = BuildEmptyPrompt(contentSlot.transform);
        _contentShade = LockShade.Attach(contentSlot.transform,
            MetaTokens.RadiusCard - MetaTokens.Spacing,
            corners: RoundedSprite.Corner.BottomRight,
            insetRight: MetaTokens.Spacing, insetBottom: MetaTokens.Spacing);

        _rail = new DatasetRail(railSlot.GetComponent<RectTransform>(), _datasets, bodyFont, OnDatasetChipClicked);
        _content = new FilterContent(_filterRoot.GetComponent<RectTransform>(), sheetController, bodyFont);
    }

    private static GameObject NewStretchChild(Transform parent, string name)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return obj;
    }

    private GameObject BuildEmptyPrompt(Transform parent)
    {
        GameObject obj = NewStretchChild(parent, "EmptyPrompt");
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.text = HintText.NoDataSource();
        text.fontSize = MetaTokens.Body1;
        text.color = PromptText;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        if (bodyFont != null) text.font = bodyFont;
        obj.SetActive(false);
        _emptyPromptLabel = text;
        return obj;
    }

    private void OnDatasetChipClicked(int index)
    {
        if (_datasets == null) return;

        if (!_collapsed && index == _datasets.ActiveIndex) CollapseData();
        else ShowDataset(index);
    }

    public bool IsCollapsed => _collapsed;

    public void ShowDataset(int index)
    {
        if (_datasets == null) return;

        _collapsed = false;
        if (index != _datasets.ActiveIndex) _datasets.SwitchTab(index);

        if (_rail != null) _rail.SetCollapsed(false);
        UpdateEmptyState();
    }

    public void CollapseData()
    {
        if (_datasets == null || _collapsed) return;

        _collapsed = true;
        if (_rail != null) _rail.SetCollapsed(true);
        UpdateEmptyState();
    }

    private void OnActiveDatasetChanged(int index)
    {
        if (_collapsed)
        {
            _collapsed = false;
            if (_rail != null) _rail.SetCollapsed(false);
        }
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        bool hasData = _datasets != null && _datasets.TabCount > 0;
        bool showFilter = hasData && !_collapsed;
        if (sheetController != null) sheetController.SetSheetPresented(showFilter);
        if (_filterRoot != null) _filterRoot.SetActive(showFilter);
        if (_emptyPrompt != null) _emptyPrompt.SetActive(!showFilter);
        if (_emptyPromptLabel != null)
            _emptyPromptLabel.text = hasData
                ? "Select a dataset to view its columns and rows."
                : HintText.NoDataSource();
    }

    private void BindTitleBar()
    {
        Transform resetBtn = UITransformSearch.FindDeep(transform, "Reset All_Btn");
        if (resetBtn == null) return;
        Button btn = resetBtn.GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(OnResetAll);
        HintTrigger.Attach(resetBtn.gameObject, "Reset All",
            "This button resets all filters and sorts.")
            .SetLockOverride(() => _locked, "Locked", HintText.WindowLocked);
        _resetShade = LockShade.Attach(resetBtn, MetaTokens.RadiusButton);
    }

    private void OnResetAll()
    {
        if (_locked) return;
        if (sheetController != null) sheetController.ResetAllFilters();
    }

    // ---- public API (DatasetManager, ToolPanelUI, Watch, Gemini tools) --

    public void Rebind(DataSource source)
    {
        if (_content != null) _content.Bind(source);
    }

    public void ShowWindow(int index)
    {
        if (_content != null)
            _content.ShowAxis(index == 1 ? FilterContent.Axis.Rows : FilterContent.Axis.Columns);
    }

    public void CloseWindows()
    {
        if (_content != null) _content.CloseSections();
    }

    public int ActiveWindow => _content != null ? _content.ActiveAxisIndex : -1;

    public void SetLocked(bool locked)
    {
        _locked = locked;
        if (_contentShade != null) _contentShade.SetLocked(locked);
        if (_resetShade != null) _resetShade.SetLocked(locked);
        if (_content != null) _content.SetLocked(locked);
        if (_rail != null) _rail.RefreshLockState();
    }

    // ---- placement / visibility ---------------------------------------

    protected override float XOffsetFromCamera => -0.28f;

    public override void ShowPanel()
    {
        if (_canvas == null) return;
        ShowCanvas();
    }

    public override void HidePanel()
    {
        if (_canvas == null) return;
        HideCanvas();
    }
}
