using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DatasetRail
{
    private readonly RectTransform _root;
    private readonly DatasetManager _datasets;
    private readonly TMP_FontAsset _bodyFont;
    private readonly System.Action<int> _onClick;
    private readonly List<UIButton.Handle> _chips = new List<UIButton.Handle>();
    private readonly List<LockShade> _chipShades = new List<LockShade>();
    private DatasetChipExpansion _expansion;
    private bool _hintLatched;
    private bool _collapsed;

    public DatasetRail(RectTransform parent, DatasetManager datasets, TMP_FontAsset bodyFont, System.Action<int> onClick)
    {
        _datasets = datasets;
        _bodyFont = bodyFont;
        _onClick = onClick;
        _root = BuildContainer(parent);

        Canvas canvas = _root.GetComponentInParent<Canvas>();
        if (canvas != null)
            _expansion = DatasetChipExpansion.Create(canvas.transform, _bodyFont, HandleClick, () => _hintLatched);

        if (_datasets != null)
        {
            _datasets.OnTabsChanged += Refresh;
            _datasets.OnActiveTabChanged += OnActiveChanged;
        }
        Refresh();
    }

    public void Dispose()
    {
        if (_expansion != null) { Object.Destroy(_expansion.gameObject); _expansion = null; }
        if (_datasets == null) return;
        _datasets.OnTabsChanged -= Refresh;
        _datasets.OnActiveTabChanged -= OnActiveChanged;
    }

    public int Count => _chips.Count;

    private RectTransform BuildContainer(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.Destroy(parent.GetChild(i).gameObject);

        VerticalLayoutGroup vlg = parent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = MetaTokens.Spacing;
        vlg.padding = new RectOffset((int)MetaTokens.PanelGutter, (int)MetaTokens.PanelGutter,
            (int)MetaTokens.Spacing, (int)MetaTokens.PanelGutter);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        return parent;
    }

    private void OnActiveChanged(int index) => ApplyActive();

    private void Refresh()
    {
        if (_expansion != null) _expansion.Hide();
        for (int i = _root.childCount - 1; i >= 0; i--)
            Object.Destroy(_root.GetChild(i).gameObject);
        _chips.Clear();
        _chipShades.Clear();

        if (_datasets == null) return;

        IReadOnlyList<DatasetManager.Tab> tabs = _datasets.Tabs;
        for (int i = 0; i < tabs.Count; i++)
            _chips.Add(CreateChip(tabs[i].label, i));

        for (int pos = 0; pos < _chips.Count; pos++)
            _chips[_chips.Count - 1 - pos].Root.transform.SetSiblingIndex(pos);

        ApplyActive();
    }

    private UIButton.Handle CreateChip(string label, int index)
    {
        UIButton.Handle h = UIButton.Create(_root, $"Dataset_{index}", label,
            height: MetaTokens.ButtonHeight, fontSize: MetaTokens.Body1,
            alignment: TextAlignmentOptions.Center, padLeft: 12f, padRight: 12f);
        if (_bodyFont != null && h.Text != null) h.Text.font = _bodyFont;

        _chipShades.Add(LockShade.Attach(h.Root.transform, MetaTokens.RadiusButton));

        int captured = index;
        h.Button.onClick.AddListener(() => HandleClick(captured));

        if (_expansion != null)
        {
            RectTransform chipRt = h.Root.GetComponent<RectTransform>();
            string full = label;
            PointerEnterProxy proxy = h.Root.AddComponent<PointerEnterProxy>();
            proxy.OnEnter = () => _expansion.Show(chipRt, full, captured,
                _datasets != null && !_collapsed && _datasets.ActiveIndex == captured);
        }

        return h;
    }

    private void HandleClick(int index)
    {
        _hintLatched = true;
        _onClick?.Invoke(index);
    }

    public void SetCollapsed(bool collapsed)
    {
        _collapsed = collapsed;
        ApplyActive();
    }

    private void ApplyActive()
    {
        int active = (_datasets != null && !_collapsed) ? _datasets.ActiveIndex : -1;
        for (int i = 0; i < _chips.Count; i++)
            UIButton.SetSelected(_chips[i], i == active);
        RefreshLockState();
    }

    public void RefreshLockState()
    {
        for (int i = 0; i < _chipShades.Count; i++)
            if (_chipShades[i] != null)
                _chipShades[i].SetLocked(_datasets != null && _datasets.IsTabLocked(i));
    }
}
