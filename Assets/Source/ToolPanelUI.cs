using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ToolPanelUI : PanelUI
{
    [UnityEngine.Serialization.FormerlySerializedAs("controller")]
    public ToolController toolController;

    [UnityEngine.Serialization.FormerlySerializedAs("dataPanel")]
    public DataPanelUI dataPanelUI;
    [UnityEngine.Serialization.FormerlySerializedAs("surfaceGenerator")]
    [UnityEngine.Serialization.FormerlySerializedAs("regionGenerator")]
    public SheetGenerator sheetGenerator;

    private readonly List<ToolButtonVisual> _toolButtons = new List<ToolButtonVisual>();
    private readonly Dictionary<ToolType, GameObject> _toolContents = new Dictionary<ToolType, GameObject>();
    private GameObject _emptyContent;
    private TextMeshProUGUI _emptyText;

    private ToolType _suspendedTool = ToolType.None;

    private static readonly Color TabActiveBg = MetaTokens.Alpha(MetaTokens.Blue, 0.30f);
    private static readonly Color TabActiveText = MetaTokens.TextPrimary;
    private static readonly Color TabInactiveBg = MetaTokens.Alpha(MetaTokens.White, 0.01f);
    private static readonly Color TabInactiveText = MetaTokens.NeutralC0;

    private class ToolButtonVisual
    {
        public ToolType Tool;
        public Image Inner;
        public TextMeshProUGUI Text;
        public PointerHighlight Highlight;
        public Color InactiveBg;
    }

    private void Awake()
    {
        _canvas = GetComponentInChildren<Canvas>(true);
        if (toolController == null) toolController = GetComponent<ToolController>();
        if (dataPanelUI != null) dataPanelUI.toolPanelUI = this;
        if (sheetGenerator == null) sheetGenerator = FindAnyObjectByType<SheetGenerator>();

        ApplyPanelSize();
        _panelGrab = GetComponent<PanelGrab>();
        if (_panelGrab != null) _panelGrab.SetGrabbable(false);
        BindToolButtons();
        BindToolContents();
        BindTitleBar();
        ApplyFonts();

        if (toolController != null)
        {
            toolController.OnToolChanged += OnToolChanged;
            toolController.OnLockStateChanged += OnLockStateChanged;
        }

        OnToolChanged(toolController != null ? toolController.SelectedTool : ToolType.None);

        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (DatasetManager.Instance != null)
        {
            DatasetManager.Instance.OnTabsChanged += RefreshEmptyText;
            DatasetManager.Instance.OnActiveTabChanged += OnActiveTabChanged;
        }
        RefreshEmptyText();
    }

    private void OnDestroy()
    {
        if (toolController != null)
        {
            toolController.OnToolChanged -= OnToolChanged;
            toolController.OnLockStateChanged -= OnLockStateChanged;
        }
        if (DatasetManager.Instance != null)
        {
            DatasetManager.Instance.OnTabsChanged -= RefreshEmptyText;
            DatasetManager.Instance.OnActiveTabChanged -= OnActiveTabChanged;
        }
    }

    private void OnActiveTabChanged(int index)
    {
        _suspendedTool = ToolType.None;
    }

    private void RefreshEmptyText()
    {
        if (_emptyText == null) return;
        _emptyText.text = HintText.HasDataSource ? HintText.NoToolSelected : HintText.NoDataSource();
    }

    private void BindToolButtons()
    {
        Transform toolRail = UITransformSearch.FindDeep(transform, "ToolRail");
        if (toolRail == null) return;

        VerticalLayoutGroup railLayout = toolRail.GetComponent<VerticalLayoutGroup>();
        if (railLayout != null)
        {
            railLayout.childForceExpandWidth = false;
            railLayout.padding.left = (int)MetaTokens.PanelGutter;
            railLayout.padding.right = (int)MetaTokens.PanelGutter;
        }

        LayoutElement railLE = toolRail.GetComponent<LayoutElement>();
        if (railLE != null)
        {
            float hPadding = railLayout != null ? railLayout.padding.left + railLayout.padding.right : 0f;
            railLE.preferredWidth = MetaTokens.ToolButtonWidth + hPadding;
        }

        for (int i = 0; i < ToolPanelConstants.Tools.Length; i++)
        {
            ToolType tool = ToolPanelConstants.Tools[i];
            Transform btnT = toolRail.Find($"Tool_{ToolPanelConstants.Label(tool)}");
            if (btnT == null) continue;

            LayoutElement btnLE = btnT.GetComponent<LayoutElement>();
            if (btnLE != null)
            {
                btnLE.preferredWidth = MetaTokens.ToolButtonWidth;
                btnLE.flexibleWidth = 0f;
            }

            UIButton.Handle h = UIButton.Style(btnT.gameObject, fontSize: MetaTokens.Body1);
            ToolButtonVisual visual = new ToolButtonVisual
            {
                Tool = tool,
                Inner = h.Inner,
                Text = h.Text,
                Highlight = h.Highlight,
                InactiveBg = UIButton.InnerIdle
            };
            _toolButtons.Add(visual);

            HintTrigger.Attach(btnT.gameObject,
                ToolPanelConstants.Label(tool), ToolPanelConstants.Hint(tool));

            ToolType captured = tool;
            Button btn = btnT.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => OnToolButtonClicked(captured));
        }
    }

    private void BindToolContents()
    {
        Transform contentArea = UITransformSearch.FindDeep(transform, "ContentArea");
        if (contentArea == null) return;

        _toolContents.Clear();
        for (int i = 0; i < ToolPanelConstants.Tools.Length; i++)
        {
            ToolType tool = ToolPanelConstants.Tools[i];
            Transform content = contentArea.Find($"{ToolPanelConstants.Label(tool)}Content");
            if (content == null) continue;

            _toolContents[tool] = content.gameObject;

            Transform descT = UITransformSearch.FindDeep(content, "Description");
            if (descT != null)
            {
                TextMeshProUGUI descLabel = descT.GetComponentInChildren<TextMeshProUGUI>(true);
                if (descLabel != null) descLabel.text = ToolPanelConstants.Description(tool);
            }

            VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
            if (contentLayout != null)
            {
                contentLayout.padding.left = (int)MetaTokens.PanelGutter;
                contentLayout.padding.right = (int)MetaTokens.PanelGutter;
                contentLayout.padding.bottom = (int)MetaTokens.Spacing;
            }

            Transform resetBtn = UITransformSearch.FindDeep(content, "Reset_Btn");
            if (resetBtn != null)
            {
                Transform border = UITransformSearch.FindDeep(content, "Reset_Border");
                if (border != null && border != resetBtn.parent) Destroy(border.gameObject);
                Destroy(resetBtn.parent != null && resetBtn.parent.name.EndsWith("_Border")
                    ? resetBtn.parent.gameObject
                    : resetBtn.gameObject);
            }
        }

        Transform empty = contentArea.Find("EmptyContent");
        if (empty != null)
        {
            _emptyContent = empty.gameObject;
            _emptyText = empty.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void BindTitleBar()
    {
        Transform titleBar = UITransformSearch.FindDeep(transform, "TitleBar");
        if (titleBar == null) return;

        HorizontalLayoutGroup titleLayout = titleBar.GetComponent<HorizontalLayoutGroup>();
        if (titleLayout != null)
            titleLayout.padding = new RectOffset(titleLayout.padding.left, titleLayout.padding.right,
                (int)MetaTokens.Spacing, (int)MetaTokens.Spacing);

        LayoutElement titleLE = titleBar.GetComponent<LayoutElement>();
        if (titleLE != null)
        {
            titleLE.minHeight = MetaTokens.ButtonHeight + MetaTokens.PanelGutter;
            titleLE.preferredHeight = MetaTokens.ButtonHeight + MetaTokens.PanelGutter;
        }

        Transform resetBtn = UITransformSearch.FindDeep(titleBar, "Reset All_Btn");
        if (resetBtn != null)
        {
            Button btn = resetBtn.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(OnResetAll);

            Transform textT = resetBtn.Find("Text");
            TextMeshProUGUI label = textT != null
                ? textT.GetComponent<TextMeshProUGUI>()
                : resetBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = "Undo All";

            PointerHighlight.AttachButtonFeedback(resetBtn, TabActiveText);
            HintTrigger.Attach(resetBtn.gameObject, "Undo All",
                "This button undoes all edits.");

            Transform anchor = resetBtn.parent != null && resetBtn.parent.name.EndsWith("_Border")
                ? resetBtn.parent
                : resetBtn;
            UIButton.Handle undo = UIButton.Create(anchor.parent, "Undo_Btn", "Undo", width: 80f);
            undo.Root.transform.SetSiblingIndex(anchor.GetSiblingIndex());
            undo.Button.onClick.AddListener(OnUndo);
            HintTrigger.Attach(undo.Root, "Undo",
                "This button undoes the most recent edit.");
        }
    }

    private void OnUndo()
    {
        if (toolController != null) toolController.UndoLast();
    }

    private void OnToolButtonClicked(ToolType tool)
    {
        if (toolController == null) return;
        toolController.SelectTool(tool);
    }

    private void OnResetAll()
    {
        if (toolController != null)
        {
            toolController.ResetAll();
            toolController.DeselectTool();
        }
        _suspendedTool = ToolType.None;
    }

    private void OnToolChanged(ToolType selected)
    {
        for (int i = 0; i < _toolButtons.Count; i++)
            ApplyToolButtonVisual(_toolButtons[i], _toolButtons[i].Tool == selected);

        foreach (KeyValuePair<ToolType, GameObject> pair in _toolContents)
            pair.Value.SetActive(pair.Key == selected);

        if (_emptyContent != null)
        {
            bool showEmpty = selected == ToolType.None;
            _emptyContent.SetActive(showEmpty);
            if (showEmpty) RefreshEmptyText();
        }
    }

    private void OnLockStateChanged(bool locked)
    {
        if (dataPanelUI != null) dataPanelUI.SetLocked(locked);
    }

    private void ApplyToolButtonVisual(ToolButtonVisual visual, bool active)
    {
        if (visual == null) return;
        Color rest = active ? TabActiveBg : visual.InactiveBg;
        if (visual.Highlight != null) visual.Highlight.SetRest(rest);
        else if (visual.Inner != null) visual.Inner.color = rest;
        if (visual.Text != null) visual.Text.color = active ? TabActiveText : TabInactiveText;
    }

    protected override float XOffsetFromCamera => 0.28f;

    public override void ShowPanel()
    {
        if (_canvas == null) return;
        ShowCanvas();

        if (toolController != null && _suspendedTool != ToolType.None)
        {
            toolController.SelectTool(_suspendedTool);
            _suspendedTool = ToolType.None;
        }
    }

    public override void HidePanel()
    {
        if (_canvas == null) return;
        if (_canvas.gameObject.activeSelf && toolController != null)
        {
            _suspendedTool = toolController.SelectedTool;
            toolController.DeselectTool();
        }
        HideCanvas();
    }

    public GameObject GetToolContent(ToolType tool) =>
        _toolContents.TryGetValue(tool, out GameObject go) ? go : null;
}
