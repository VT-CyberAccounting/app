using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace DataWorld
{
    public class FilterPanelUI : MonoBehaviour
    {
        [Header("Panel sections")]
        [SerializeField] private ScrollRect countryScroll;
        [SerializeField] private ScrollRect yearScroll;
        [SerializeField] private ScrollRect companyScroll;

        [Header("Search")]
        [SerializeField] private TMP_InputField companySearchField;

        [Header("Prefab")]
        [SerializeField] private GameObject filterItemPrefab;

        private readonly Dictionary<string, Toggle> _countryToggles = new();
        private readonly Dictionary<int, Toggle> _yearToggles = new();
        private readonly Dictionary<string, Toggle> _companyToggles = new();

        private List<string> _allCompanyNames = new List<string>();

        private void Start()
        {
            DataManager.Instance.OnDataReady.AddListener(BuildLists);
        }

        private void BuildLists()
        {
            FixScrollSetup(countryScroll);
            FixScrollSetup(yearScroll);
            FixScrollSetup(companyScroll);

            BuildCountryList();
            BuildYearList();
            BuildCompanyList();

            if (companySearchField != null)
                companySearchField.onValueChanged.AddListener(OnCompanySearch);

            FilterManager.Instance.OnFilterChanged.AddListener(RefreshVisuals);
        }

        // ── Mask → RectMask2D + ContentSizeFitter ────────────────────

        private void FixScrollSetup(ScrollRect scroll)
        {
            if (scroll == null || scroll.viewport == null) return;

            var mask = scroll.viewport.GetComponent<Mask>();
            if (mask != null)
            {
                Destroy(mask);
                var img = scroll.viewport.GetComponent<Image>();
                if (img != null) Destroy(img);
            }
            if (scroll.viewport.GetComponent<RectMask2D>() == null)
                scroll.viewport.gameObject.AddComponent<RectMask2D>();

            if (scroll.content != null)
            {
                var fitter = scroll.content.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                    fitter = scroll.content.gameObject.AddComponent<ContentSizeFitter>();

                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var rt = scroll.content as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                }
            }
        }

        // ── Liste builder'ları ────────────────────────────────────────

        private void BuildCountryList()
        {
            foreach (string code in DataManager.Instance.AllCountries)
            {
                var item = Instantiate(filterItemPrefab, countryScroll.content);
                item.transform.localScale = Vector3.one;

                var toggle = item.GetComponent<Toggle>();
                var label = item.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = code;
                    label.color = Color.black;
                    label.fontSize = 18f;
                }

                toggle.isOn = false;
                string capturedCode = code;
                toggle.onValueChanged.AddListener(_ =>
                    FilterManager.Instance.ToggleCountry(capturedCode));

                _countryToggles[code] = toggle;
            }

            RebuildLayout(countryScroll);
        }

        private void BuildYearList()
        {
            foreach (int year in DataManager.Instance.AllYears)
            {
                var item = Instantiate(filterItemPrefab, yearScroll.content);
                item.transform.localScale = Vector3.one;

                var toggle = item.GetComponent<Toggle>();
                var label = item.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = year.ToString();
                    label.color = Color.black;
                    label.fontSize = 18f;
                }

                toggle.isOn = false;
                int capturedYear = year;
                toggle.onValueChanged.AddListener(_ =>
                    FilterManager.Instance.ToggleYear(capturedYear));

                _yearToggles[year] = toggle;
            }

            RebuildLayout(yearScroll);
        }

        private void BuildCompanyList()
        {
            _allCompanyNames = DataManager.Instance.AllCompanies.Take(100).ToList();

            SpawnCompanyToggles(_allCompanyNames);
            RebuildLayout(companyScroll);
        }

        // ── Şirket arama ──────────────────────────────────────────────

        private void OnCompanySearch(string query)
        {
            foreach (Transform child in companyScroll.content)
                Destroy(child.gameObject);
            _companyToggles.Clear();

            var filtered = string.IsNullOrEmpty(query)
                ? _allCompanyNames
                : _allCompanyNames
                    .Where(c => c.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

            SpawnCompanyToggles(filtered);
            RebuildLayout(companyScroll);
        }

        private void SpawnCompanyToggles(List<string> companies)
        {
            foreach (string company in companies)
            {
                var item = Instantiate(filterItemPrefab, companyScroll.content);
                item.transform.localScale = Vector3.one;

                var toggle = item.GetComponent<Toggle>();
                var label = item.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = company;
                    label.color = Color.black;
                    label.fontSize = 18f;
                }

                toggle.isOn = IsExplicitlySelected_Company(company);

                string capturedName = company;
                toggle.onValueChanged.AddListener(_ =>
                    FilterManager.Instance.ToggleCompany(capturedName));

                _companyToggles[company] = toggle;
            }
        }

        // ── Layout rebuild ────────────────────────────────────────────

        private void RebuildLayout(ScrollRect scroll)
        {
            if (scroll == null || scroll.content == null) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                scroll.content as RectTransform);
            scroll.verticalNormalizedPosition = 1f;
        }

        // ── Görsel yenileme ───────────────────────────────────────────

        private void RefreshVisuals()
        {
            foreach (var (code, toggle) in _countryToggles)
                toggle.SetIsOnWithoutNotify(IsExplicitlySelected_Country(code));

            foreach (var (year, toggle) in _yearToggles)
                toggle.SetIsOnWithoutNotify(IsExplicitlySelected_Year(year));

            foreach (var (name, toggle) in _companyToggles)
                toggle.SetIsOnWithoutNotify(IsExplicitlySelected_Company(name));
        }

        private bool IsExplicitlySelected_Country(string code)
        {
            foreach (var c in FilterManager.Instance.ActiveCountries)
                if (c == code) return true;
            return false;
        }

        private bool IsExplicitlySelected_Year(int year)
        {
            foreach (var y in FilterManager.Instance.ActiveYears)
                if (y == year) return true;
            return false;
        }

        private bool IsExplicitlySelected_Company(string name)
        {
            foreach (var n in FilterManager.Instance.ActiveCompanies)
                if (n == name) return true;
            return false;
        }
    }
}