using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FilterPanelUI : MonoBehaviour
{
    public SurfaceFilterController filterController;
    public float distanceFromCamera = 1.5f;
    public float verticalOffset = 0.1f;

    public GameObject studentSurface;
    public GameObject solutionSurface;
    public GameObject errorSurface;

    private Canvas _canvas;
    private List<GameObject> _tabButtons = new List<GameObject>();
    private List<SortButtonVisual> _tabVisuals = new List<SortButtonVisual>();
    private List<GameObject> _tabContents = new List<GameObject>();
    private int _activeTabIndex;

    private Dictionary<string, ToggleState> _columnToggles = new Dictionary<string, ToggleState>();
    private Dictionary<string, ToggleState> _industryToggles = new Dictionary<string, ToggleState>();
    private Dictionary<string, ToggleState> _yearToggles = new Dictionary<string, ToggleState>();
    private Dictionary<string, ToggleState> _countryToggles = new Dictionary<string, ToggleState>();
    private Dictionary<string, ToggleState> _surfaceToggles = new Dictionary<string, ToggleState>();

    private Dictionary<string, SortButtonPair> _sortButtons = new Dictionary<string, SortButtonPair>();

    private static readonly Color ActiveRowBg = new Color(0f, 0.82f, 0.9f, 0.092f);
    private static readonly Color ActiveTrack = new Color(0f, 0.82f, 0.9f, 1f);
    private static readonly Color ActiveTextColor = new Color(0.91f, 0.92f, 0.94f, 1f);
    private static readonly Color ActiveKnob = Color.white;

    private static readonly Color InactiveRowBg = new Color(1f, 1f, 1f, 0.028f);
    private static readonly Color InactiveTrack = new Color(0.16f, 0.18f, 0.23f, 0.92f);
    private static readonly Color InactiveText = new Color(0.31f, 0.33f, 0.41f, 1f);
    private static readonly Color InactiveKnob = new Color(0.31f, 0.33f, 0.41f, 1f);

    private static readonly Color BulkBtnBg = new Color(0f, 0f, 0f, 0.01f);
    private static readonly Color BulkBtnText = new Color(0.61f, 0.63f, 0.71f, 1f);

    private static readonly Color SortActiveBg = new Color(0f, 0.82f, 0.9f, 0.18f);
    private static readonly Color SortActiveText = new Color(0f, 0.82f, 0.9f, 1f);

    private class ToggleState
    {
        public bool IsActive;
        public Image RowBackground;
        public TextMeshProUGUI Label;
        public Image TrackImage;
        public RectTransform Knob;
        public Image KnobImage;
    }

    private class SortButtonVisual
    {
        public Image Inner;
        public TextMeshProUGUI Text;
    }

    private class SortButtonPair
    {
        public SortButtonVisual Ascending;
        public SortButtonVisual Descending;
    }

    private void Start()
    {
        _canvas = GetComponentInChildren<Canvas>(true);
        BindTabs();
        BindTabContents();
        BindTitleBarButtons();
        BindSurfaceToggles();
        BindSurfaceResetButton();
        ApplyInitialSurfaceState();
        SwitchToTab(0);

        if (filterController != null)
            filterController.OnSortChanged += RefreshAllSortVisuals;
        RefreshAllSortVisuals();

        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (filterController != null)
            filterController.OnSortChanged -= RefreshAllSortVisuals;
    }

    private void PlaceInFrontOfCamera()
    {
        Transform cam = CameraRig.MainTransform;
        if (cam == null) return;

        Vector3 forward = cam.forward;
        forward.y = 0f;
        forward.Normalize();

        transform.position = cam.position + forward * distanceFromCamera + Vector3.up * verticalOffset;
        transform.rotation = Quaternion.LookRotation(forward);
    }

    private void BindTabs()
    {
        Transform tabBar = UITransformSearch.FindDeep(transform,"TabBar");
        if (tabBar == null) return;

        for (int i = 0; i < FilterPanelConstants.TabLabels.Length; i++)
        {
            Transform tab = tabBar.Find($"Tab_{FilterPanelConstants.TabLabels[i]}");
            if (tab == null) continue;

            _tabButtons.Add(tab.gameObject);

            Transform innerT = tab.Find("Inner");
            Transform textT = tab.Find("Text");
            Image innerImg = innerT != null ? innerT.GetComponent<Image>() : null;
            Image outerImg = tab.GetComponent<Image>();
            if (outerImg != null && outerImg.sprite == null && innerImg != null && innerImg.sprite != null)
            {
                outerImg.sprite = innerImg.sprite;
                outerImg.type = Image.Type.Sliced;
            }

            _tabVisuals.Add(new SortButtonVisual
            {
                Inner = innerImg != null ? innerImg : outerImg,
                Text = textT != null ? textT.GetComponent<TextMeshProUGUI>() : null
            });

            int index = _tabButtons.Count - 1;
            Button btn = tab.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => SwitchToTab(index));
        }
    }

    private void BindTabContents()
    {
        Transform panelRoot = UITransformSearch.FindDeep(transform,"PanelRoot");
        if (panelRoot == null) return;

        string[] contentNames = { "ColumnsContent", "IndustriesContent", "YearsContent", "CountriesContent" };
        for (int i = 0; i < contentNames.Length; i++)
        {
            Transform content = panelRoot.Find(contentNames[i]);
            if (content == null) continue;

            _tabContents.Add(content.gameObject);

            Transform scrollContent = UITransformSearch.FindDeep(content, "Content");
            if (scrollContent == null) continue;

            switch (i)
            {
                case 0:
                    BindToggles(scrollContent, _columnToggles, OnColumnToggle);
                    BindBulkButtons(content, _columnToggles, OnColumnToggle);
                    break;
                case 1:
                    BindToggles(scrollContent, _industryToggles, OnIndustryToggle);
                    BindBulkButtons(content, _industryToggles, OnIndustryToggle);
                    BindSortButtons(content, CSVDataSource.SortFieldIndustry);
                    break;
                case 2:
                    BindToggles(scrollContent, _yearToggles, OnYearToggle);
                    BindBulkButtons(content, _yearToggles, OnYearToggle);
                    BindSortButtons(content, CSVDataSource.SortFieldYear);
                    break;
                case 3:
                    BindToggles(scrollContent, _countryToggles, OnCountryToggle);
                    BindBulkButtons(content, _countryToggles, OnCountryToggle);
                    BindSortButtons(content, CSVDataSource.SortFieldCountry);
                    break;
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

    private void BindBulkButtons(Transform tabContainer, Dictionary<string, ToggleState> states, System.Action<string, bool> callback)
    {
        Transform bulkButtons = UITransformSearch.FindDeep(tabContainer, "BulkButtons");
        if (bulkButtons == null) return;

        Transform selectAll = bulkButtons.Find("Select All");
        Transform deselectAll = bulkButtons.Find("Deselect All");

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

    private void BindSortButtons(Transform tabContainer, string field)
    {
        Transform bulkButtons = UITransformSearch.FindDeep(tabContainer, "BulkButtons");
        if (bulkButtons == null) return;

        SortButtonVisual asc = ResolveSortButton(bulkButtons.Find("Sort Ascending"), field, CSVDataSource.SortDirection.Ascending);
        SortButtonVisual desc = ResolveSortButton(bulkButtons.Find("Sort Descending"), field, CSVDataSource.SortDirection.Descending);

        if (asc == null && desc == null) return;
        _sortButtons[field] = new SortButtonPair { Ascending = asc, Descending = desc };
    }

    private SortButtonVisual ResolveSortButton(Transform btnTransform, string field, CSVDataSource.SortDirection direction)
    {
        if (btnTransform == null) return null;

        Transform inner = btnTransform.Find("Inner");
        Transform textT = btnTransform.Find("Text");
        if (inner == null || textT == null) return null;

        SortButtonVisual visual = new SortButtonVisual
        {
            Inner = inner.GetComponent<Image>(),
            Text = textT.GetComponent<TextMeshProUGUI>()
        };

        Button btn = btnTransform.GetComponent<Button>();
        if (btn != null)
        {
            string capturedField = field;
            CSVDataSource.SortDirection capturedDir = direction;
            btn.onClick.AddListener(() => OnSortButtonClicked(capturedField, capturedDir));
        }

        return visual;
    }

    private void OnSortButtonClicked(string field, CSVDataSource.SortDirection direction)
    {
        if (filterController == null) return;

        if (filterController.TryGetSortDirection(field, out CSVDataSource.SortDirection current) && current == direction)
            filterController.ClearSort(field);
        else
            filterController.ApplySort(field, direction);
    }

    private void RefreshAllSortVisuals()
    {
        foreach (var kvp in _sortButtons)
            RefreshSortVisuals(kvp.Key, kvp.Value);
    }

    private void RefreshSortVisuals(string field, SortButtonPair pair)
    {
        CSVDataSource.SortDirection direction = CSVDataSource.SortDirection.Ascending;
        bool hasSort = filterController != null && filterController.TryGetSortDirection(field, out direction);
        bool ascActive = hasSort && direction == CSVDataSource.SortDirection.Ascending;
        bool descActive = hasSort && direction == CSVDataSource.SortDirection.Descending;

        ApplySortButtonVisual(pair.Ascending, ascActive);
        ApplySortButtonVisual(pair.Descending, descActive);
    }

    private void ApplySortButtonVisual(SortButtonVisual visual, bool active)
    {
        if (visual == null) return;
        if (visual.Inner != null) visual.Inner.color = active ? SortActiveBg : BulkBtnBg;
        if (visual.Text != null) visual.Text.color = active ? SortActiveText : BulkBtnText;
    }

    private void BindTitleBarButtons()
    {
        Transform titleBar = UITransformSearch.FindDeep(transform,"TitleBar");
        if (titleBar == null) return;

        Transform resetBtn = UITransformSearch.FindDeep(titleBar, "Reset All_Btn");
        if (resetBtn != null)
        {
            Button btn = resetBtn.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OnResetAll);
        }

        Transform closeBtn = UITransformSearch.FindDeep(titleBar, "X_Btn");
        if (closeBtn != null)
        {
            Button btn = closeBtn.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(HidePanel);
        }
    }

    private void BindSurfaceToggles()
    {
        Transform section = UITransformSearch.FindDeep(transform,"SurfaceToggles");
        if (section == null) return;

        for (int i = 0; i < section.childCount; i++)
        {
            Transform child = section.GetChild(i);
            if (!child.name.StartsWith("Toggle_")) continue;

            string key = child.name.Substring(7);

            Transform trackTransform = child.Find("Track");
            Transform knobTransform = trackTransform != null ? trackTransform.Find("Knob") : null;
            Transform labelTransform = child.Find("Label");

            if (trackTransform == null || knobTransform == null || labelTransform == null) continue;

            ToggleState state = new ToggleState
            {
                IsActive = false,
                RowBackground = child.GetComponent<Image>(),
                Label = labelTransform.GetComponent<TextMeshProUGUI>(),
                TrackImage = trackTransform.GetComponent<Image>(),
                Knob = knobTransform.GetComponent<RectTransform>(),
                KnobImage = knobTransform.GetComponent<Image>()
            };

            _surfaceToggles[key] = state;

            string capturedKey = key;
            Button btn = child.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() =>
                {
                    state.IsActive = !state.IsActive;
                    ApplyToggleVisuals(state);
                    OnSurfaceToggle(capturedKey, state.IsActive);
                });
            }
        }
    }

    private void BindSurfaceResetButton()
    {
        Transform titleBar = UITransformSearch.FindDeep(transform,"SurfaceTitleBar");
        if (titleBar == null) return;

        Transform resetBtn = UITransformSearch.FindDeep(titleBar, "Reset All_Btn");
        if (resetBtn == null) return;

        Button btn = resetBtn.GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(OnSurfaceReset);
    }

    private void ApplyInitialSurfaceState()
    {
        if (studentSurface != null) studentSurface.SetActive(false);
        if (solutionSurface != null) solutionSurface.SetActive(false);
        if (errorSurface != null) errorSurface.SetActive(false);
    }

    private void OnSurfaceToggle(string key, bool active)
    {
        SetSurfaceActive(ResolveSurface(key), active);
    }

    private void SetSurfaceActive(GameObject target, bool active)
    {
        if (target == null) return;

        if (active)
        {
            target.SetActive(true);
            return;
        }

        DataSurfaceGenerator generator = target.GetComponent<DataSurfaceGenerator>();
        if (generator != null)
            generator.CollapseAndDeactivate();
        else
            target.SetActive(false);
    }

    private void OnSurfaceReset()
    {
        foreach (var kvp in _surfaceToggles)
        {
            ToggleState state = kvp.Value;
            if (state.IsActive)
            {
                state.IsActive = false;
                ApplyToggleVisuals(state);
            }
            SetSurfaceActive(ResolveSurface(kvp.Key), false);
        }
    }

    private GameObject ResolveSurface(string key)
    {
        switch (key)
        {
            case "Student": return studentSurface;
            case "Solution": return solutionSurface;
            case "Error": return errorSurface;
            default: return null;
        }
    }

    private void SwitchToTab(int index)
    {
        _activeTabIndex = index;

        for (int i = 0; i < _tabVisuals.Count; i++)
            ApplySortButtonVisual(_tabVisuals[i], i == index);

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
        if (filterController != null)
            filterController.BeginBatch();

        foreach (var kvp in states)
        {
            if (kvp.Value.IsActive == active) continue;
            kvp.Value.IsActive = active;
            ApplyToggleVisuals(kvp.Value);
            callback(kvp.Key, active);
        }

        if (filterController != null)
            filterController.EndBatch();
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

}