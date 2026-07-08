using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oculus.Interaction;

public class ColorTool : SheetTool
{
    [UnityEngine.Serialization.FormerlySerializedAs("toolPanel")]
    public ToolPanelUI toolPanelUI;

    public Color[] palette =
    {
        new Color(1.00f, 1.00f, 1.00f),
        new Color(0.90f, 0.22f, 0.22f),
        new Color(0.95f, 0.55f, 0.15f),
        new Color(0.95f, 0.85f, 0.22f),
        new Color(0.32f, 0.80f, 0.38f),
        new Color(0.25f, 0.55f, 0.95f),
        new Color(0.29f, 0.00f, 0.51f),
        new Color(0.56f, 0.00f, 1.00f),
        new Color(0.00f, 0.00f, 0.00f),
    };

    public string[] paletteNames =
    {
        "White", "Red", "Orange", "Yellow", "Green", "Blue", "Indigo", "Violet", "Black",
    };

    public Color highlightColor = MetaTokens.Blue;

    private Color _color;
    private int _selected = -1;
    private bool _swatchHintLatched;
    private readonly List<Image> _swatchFrames = new List<Image>();
    private readonly List<PointerHighlight> _swatchHighlights = new List<PointerHighlight>();

    private RayInteractable _ray;
    private bool _hooked;
    private bool _active;

    private static readonly Color UnselectedFrame = new Color(0f, 0f, 0f, 0f);

    private void Start()
    {
        ResolveSheetRefs(true);
        if (toolPanelUI == null) toolPanelUI = FindAnyObjectByType<ToolPanelUI>();

        if (sheetGenerator != null)
            _ray = sheetGenerator.GetComponentInChildren<RayInteractable>(true);

        BuildPicker();

        if (toolController != null)
        {
            toolController.OnToolChanged += OnToolChanged;
            toolController.OnToolReset += OnToolReset;
        }

        SetActive(toolController != null && toolController.SelectedTool == ToolType.Color);
    }

    private void OnDestroy()
    {
        if (toolController != null)
        {
            toolController.OnToolChanged -= OnToolChanged;
            toolController.OnToolReset -= OnToolReset;
        }
        Unhook();
    }

    private void OnToolChanged(ToolType selected) => SetActive(selected == ToolType.Color);

    private void OnToolReset(ToolType tool)
    {
        if (tool != ToolType.Color) return;
        if (sheetManager != null) sheetManager.ResetColors();
        ClearSelection();
    }

    private void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active;
        if (active) Hook();
        else Unhook();
    }

    private void Hook()
    {
        if (_hooked || _ray == null) return;
        _ray.WhenPointerEventRaised += OnPointerEvent;
        _hooked = true;
    }

    private void Unhook()
    {
        if (!_hooked) return;
        if (_ray != null) _ray.WhenPointerEventRaised -= OnPointerEvent;
        _hooked = false;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
            Commit(evt.Pose.position);
    }

    private void Commit(Vector3 world)
    {
        if (_selected < 0) return;
        if (sheetManager == null || sheetGenerator == null) return;

        (int vr, int vc) = sheetGenerator.GetNearestVisibleCell(world);
        if (vr < 0) return;

        bool wasBaked = sheetManager.IsBaked;
        if (!wasBaked) sheetManager.EnsureBaked();

        SheetManager.Sheet sheet = sheetManager.SheetAt(vr, vc);
        if (sheet == null)
        {
            if (!wasBaked) sheetManager.Unbake();
            return;
        }

        sheetManager.AddColorOverride(sheet, _color);
        if (toolController != null) toolController.Journal.PushColor();
    }

    private void SetColor(int index)
    {
        _selected = index;
        _color = palette[index];
        _swatchHintLatched = true;
        RefreshSwatches();
    }

    public IReadOnlyList<string> PaletteNames => paletteNames;

    public string CurrentColorName =>
        _selected >= 0 && _selected < paletteNames.Length ? paletteNames[_selected] : "none";

    public bool SelectColorByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string target = name.Trim().ToLowerInvariant();
        for (int i = 0; i < paletteNames.Length; i++)
        {
            if (paletteNames[i].ToLowerInvariant() == target)
            {
                SetColor(i);
                return true;
            }
        }
        return false;
    }

    private void ClearSelection()
    {
        _selected = -1;
        RefreshSwatches();
    }

    private void RefreshSwatches()
    {
        for (int i = 0; i < _swatchFrames.Count; i++)
        {
            Color frameColor = i == _selected ? highlightColor : UnselectedFrame;
            if (_swatchFrames[i] != null) _swatchFrames[i].color = frameColor;
            if (i < _swatchHighlights.Count && _swatchHighlights[i] != null)
                _swatchHighlights[i].SetColors(frameColor, frameColor);
        }
    }

    private void BuildPicker()
    {
        if (toolPanelUI == null) return;
        GameObject content = toolPanelUI.GetToolContent(ToolType.Color);
        if (content == null) return;

        GameObject grid = new GameObject("SwatchGrid");
        grid.transform.SetParent(content.transform, false);
        grid.AddComponent<RectTransform>();

        LayoutElement le = grid.AddComponent<LayoutElement>();
        le.minHeight = 150f;
        le.preferredHeight = 150f;
        le.flexibleHeight = 0f;

        GridLayoutGroup glg = grid.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(44f, 44f);
        glg.spacing = new Vector2(0f, 0f);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 3;

        for (int i = 0; i < palette.Length; i++)
            CreateSwatch(grid.transform, i);

        Transform description = content.transform.Find("Description");
        int insertAt = description != null ? description.GetSiblingIndex() + 1 : 0;
        grid.transform.SetSiblingIndex(insertAt);

        ClearSelection();
    }

    private void CreateSwatch(Transform parent, int index)
    {
        GameObject sw = new GameObject($"Swatch_{index}");
        sw.transform.SetParent(parent, false);
        sw.AddComponent<RectTransform>();

        Image frame = sw.AddComponent<Image>();
        frame.sprite = RoundedSprite.Get((int)MetaTokens.RadiusButton);
        frame.type = Image.Type.Sliced;
        frame.color = UnselectedFrame;

        Button btn = sw.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = frame;
        int captured = index;
        btn.onClick.AddListener(() => SetColor(captured));

        PointerHighlight hl = sw.AddComponent<PointerHighlight>();
        hl.target = frame;
        hl.SetColors(UnselectedFrame, UnselectedFrame);
        _swatchHighlights.Add(hl);

        HintTrigger.AttachShared(sw, "Color",
            "This button allows you to select a color.",
            () => _swatchHintLatched);

        GameObject inner = new GameObject("Inner");
        inner.transform.SetParent(sw.transform, false);
        RectTransform irt = inner.AddComponent<RectTransform>();
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(3f, 3f);
        irt.offsetMax = new Vector2(-3f, -3f);

        Image innerImg = inner.AddComponent<Image>();
        innerImg.sprite = RoundedSprite.Get((int)MetaTokens.RadiusChip);
        innerImg.type = Image.Type.Sliced;
        innerImg.color = palette[index];
        innerImg.raycastTarget = false;

        _swatchFrames.Add(frame);
    }
}
