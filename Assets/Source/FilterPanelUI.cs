using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FilterPanelUI : MonoBehaviour
{
    public SurfaceFilterController filterController;
    public float distanceFromCamera = 1.5f;
    public float verticalOffset = 0.1f;

    private Canvas _canvas;
    private List<GameObject> _tabButtons = new List<GameObject>();
    private List<GameObject> _tabContents = new List<GameObject>();
    private int _activeTabIndex;

    private Dictionary<string, ToggleState> _columnToggles = new Dictionary<string, ToggleState>();
    private Dictionary<string, ToggleState> _industryToggles = new Dictionary<string, ToggleState>();
    private Dictionary<string, ToggleState> _yearToggles = new Dictionary<string, ToggleState>();
    private Dictionary<string, ToggleState> _countryToggles = new Dictionary<string, ToggleState>();

    private static readonly Color ActiveRowBg = new Color(0f, 0.82f, 0.9f, 0.092f);
    private static readonly Color ActiveTrack = new Color(0f, 0.82f, 0.9f, 1f);
    private static readonly Color ActiveTextColor = new Color(0.91f, 0.92f, 0.94f, 1f);
    private static readonly Color ActiveKnob = Color.white;

    private static readonly Color InactiveRowBg = new Color(1f, 1f, 1f, 0.028f);
    private static readonly Color InactiveTrack = new Color(0.16f, 0.18f, 0.23f, 0.92f);
    private static readonly Color InactiveText = new Color(0.31f, 0.33f, 0.41f, 1f);
    private static readonly Color InactiveKnob = new Color(0.31f, 0.33f, 0.41f, 1f);

    private static readonly Color TabActiveText = new Color(0f, 0.82f, 0.9f, 1f);
    private static readonly Color TabActiveBg = new Color(0f, 0.82f, 0.9f, 0.046f);
    private static readonly Color TabActiveBar = new Color(0f, 0.82f, 0.9f, 1f);
    private static readonly Color TabInactiveText = new Color(0.42f, 0.44f, 0.52f, 1f);

    private class ToggleState
    {
        public bool IsActive;
        public Image RowBackground;
        public TextMeshProUGUI Label;
        public Image TrackImage;
        public RectTransform Knob;
        public Image KnobImage;
    }

    private void Start()
    {
        _canvas = GetComponentInChildren<Canvas>(true);
        BindTabs();
        BindTabContents();
        BindTitleBarButtons();
        SwitchToTab(0);

        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    private void PlaceInFrontOfCamera()
    {
        Transform cam = Camera.main?.transform;
        if (cam == null) return;

        Vector3 forward = cam.forward;
        forward.y = 0f;
        forward.Normalize();

        transform.position = cam.position + forward * distanceFromCamera + Vector3.up * verticalOffset;
        transform.rotation = Quaternion.LookRotation(forward);
    }

    private void BindTabs()
    {
        Transform tabBar = FindDeep(transform, "TabBar");
        if (tabBar == null) return;

        string[] tabNames = { "Tab_Columns", "Tab_Industries", "Tab_Years", "Tab_Countries" };
        for (int i = 0; i < tabNames.Length; i++)
        {
            Transform tab = tabBar.Find(tabNames[i]);
            if (tab == null) continue;

            _tabButtons.Add(tab.gameObject);

            int index = i;
            Button btn = tab.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => SwitchToTab(index));
        }
    }

    private void BindTabContents()
    {
        Transform panelRoot = FindDeep(transform, "PanelRoot");
        if (panelRoot == null) return;

        string[] contentNames = { "ColumnsContent", "IndustriesContent", "YearsContent", "CountriesContent" };
        for (int i = 0; i < contentNames.Length; i++)
        {
            Transform content = panelRoot.Find(contentNames[i]);
            if (content == null) continue;

            _tabContents.Add(content.gameObject);

            Transform scrollContent = FindDeep(content, "Content");
            if (scrollContent == null) continue;

            switch (i)
            {
                case 0: BindToggles(scrollContent, _columnToggles, OnColumnToggle); BindBulkButtons(scrollContent, _columnToggles, OnColumnToggle); break;
                case 1: BindToggles(scrollContent, _industryToggles, OnIndustryToggle); BindBulkButtons(scrollContent, _industryToggles, OnIndustryToggle); break;
                case 2: BindToggles(scrollContent, _yearToggles, OnYearToggle); BindBulkButtons(scrollContent, _yearToggles, OnYearToggle); break;
                case 3: BindToggles(scrollContent, _countryToggles, OnCountryToggle); BindBulkButtons(scrollContent, _countryToggles, OnCountryToggle); break;
            }
        }
    }

    private void BindToggles(Transform scrollContent, Dictionary<string, ToggleState> states, System.Action<string, bool> callback)
    {
        for (int i = 0; i < scrollContent.childCount; i++)
        {
            Transform child = scrollContent.GetChild(i);
            if (!child.name.StartsWith("Toggle_")) continue;

            string key = child.name.Substring(7);

            Transform trackTransform = child.Find("Track");
            Transform knobTransform = trackTransform?.Find("Knob");
            Transform labelTransform = child.Find("Label");

            if (trackTransform == null || knobTransform == null || labelTransform == null) continue;

            ToggleState state = new ToggleState
            {
                IsActive = true,
                RowBackground = child.GetComponent<Image>(),
                Label = labelTransform.GetComponent<TextMeshProUGUI>(),
                TrackImage = trackTransform.GetComponent<Image>(),
                Knob = knobTransform.GetComponent<RectTransform>(),
                KnobImage = knobTransform.GetComponent<Image>()
            };

            states[key] = state;

            string capturedKey = key;
            Button btn = child.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() =>
                {
                    state.IsActive = !state.IsActive;
                    ApplyToggleVisuals(state);
                    callback(capturedKey, state.IsActive);
                });
            }
        }
    }

    private void BindBulkButtons(Transform scrollContent, Dictionary<string, ToggleState> states, System.Action<string, bool> callback)
    {
        Transform bulkButtons = scrollContent.Find("BulkButtons");
        if (bulkButtons == null) return;

        Transform selectAll = bulkButtons.Find("Select all");
        Transform deselectAll = bulkButtons.Find("Deselect all");

        if (selectAll != null)
        {
            Button btn = selectAll.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => SetAllToggles(states, true, callback));
        }

        if (deselectAll != null)
        {
            Button btn = deselectAll.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => SetAllToggles(states, false, callback));
        }
    }

    private void BindTitleBarButtons()
    {
        Transform resetBtn = FindDeep(transform, "Reset all_Btn");
        if (resetBtn != null)
        {
            Button btn = resetBtn.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OnResetAll);
        }

        Transform closeBtn = FindDeep(transform, "X_Btn");
        if (closeBtn != null)
        {
            Button btn = closeBtn.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(HidePanel);
        }
    }

    private void SwitchToTab(int index)
    {
        _activeTabIndex = index;

        for (int i = 0; i < _tabButtons.Count; i++)
        {
            bool active = (i == index);
            GameObject tabBtn = _tabButtons[i];

            Image bg = tabBtn.GetComponent<Image>();
            if (bg != null)
                bg.color = active ? TabActiveBg : Color.clear;

            TextMeshProUGUI text = tabBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.color = active ? TabActiveText : TabInactiveText;
                text.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            }

            Transform underline = tabBtn.transform.Find("Underline");
            if (underline != null)
            {
                Image underImg = underline.GetComponent<Image>();
                if (underImg != null)
                    underImg.color = active ? TabActiveBar : Color.clear;
            }
        }

        for (int i = 0; i < _tabContents.Count; i++)
            _tabContents[i].SetActive(i == index);
    }

    private void ApplyToggleVisuals(ToggleState state)
    {
        if (state.IsActive)
        {
            state.RowBackground.color = ActiveRowBg;
            state.Label.color = ActiveTextColor;
            state.TrackImage.color = ActiveTrack;
            state.KnobImage.color = ActiveKnob;
            state.Knob.anchoredPosition = new Vector2(21f, 0f);
        }
        else
        {
            state.RowBackground.color = InactiveRowBg;
            state.Label.color = InactiveText;
            state.TrackImage.color = InactiveTrack;
            state.KnobImage.color = InactiveKnob;
            state.Knob.anchoredPosition = new Vector2(3f, 0f);
        }
    }

    private void SetAllToggles(Dictionary<string, ToggleState> states, bool active, System.Action<string, bool> callback)
    {
        if (CSVDataManager.Instance != null)
            CSVDataManager.Instance.BeginBatchUpdate();

        foreach (var kvp in states)
        {
            if (kvp.Value.IsActive == active) continue;
            kvp.Value.IsActive = active;
            ApplyToggleVisuals(kvp.Value);
            callback(kvp.Key, active);
        }

        if (CSVDataManager.Instance != null)
            CSVDataManager.Instance.EndBatchUpdate();
    }

    private void OnColumnToggle(string key, bool active)
    {
        if (filterController == null) return;
        if (int.TryParse(key, out int colIndex))
            filterController.SetColumnVisible(colIndex, active);
    }

    private void OnIndustryToggle(string key, bool active)
    {
        if (filterController != null)
            filterController.SetIndustryActive(key, active);
    }

    private void OnYearToggle(string key, bool active)
    {
        if (filterController != null)
            filterController.SetYearActive(key, active);
    }

    private void OnCountryToggle(string key, bool active)
    {
        if (filterController != null)
            filterController.SetCountryActive(key, active);
    }

    private void OnResetAll()
    {
        if (filterController != null)
            filterController.ResetAllFilters();

        ResetAllToggleVisuals(_columnToggles);
        ResetAllToggleVisuals(_industryToggles);
        ResetAllToggleVisuals(_yearToggles);
        ResetAllToggleVisuals(_countryToggles);
        SwitchToTab(0);
    }

    private void ResetAllToggleVisuals(Dictionary<string, ToggleState> states)
    {
        foreach (var kvp in states)
        {
            kvp.Value.IsActive = true;
            ApplyToggleVisuals(kvp.Value);
        }
    }

    public void ShowPanel()
    {
        if (_canvas == null) return;
        PlaceInFrontOfCamera();
        _canvas.gameObject.SetActive(true);
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

    private Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindDeep(parent.GetChild(i), name);
            if (result != null) return result;
        }

        return null;
    }
}