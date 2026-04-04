using System;
using System.Collections.Generic;
using UnityEngine;

public class SurfaceFilterController : MonoBehaviour
{
    #region Singleton

    public static SurfaceFilterController Instance { get; private set; }

    #endregion

    #region Inspector Fields

    [Header("References")]
    public DataSurfaceGenerator surfaceGenerator;

    #endregion

    #region Events

    public event Action<int, bool> OnColumnToggled;
    public event Action<string, bool> OnIndustryChanged;
    public event Action<string, bool> OnYearChanged;
    public event Action<string, bool> OnCountryChanged;

    #endregion

    #region Private Fields

    private Dictionary<int, bool> _columnVisibility = new Dictionary<int, bool>();

    #endregion

    #region Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(WaitForDataManager());
    }

    private System.Collections.IEnumerator WaitForDataManager()
    {
        while (CSVDataManager.Instance == null || !CSVDataManager.Instance.IsLoaded)
            yield return null;

        int colCount = CSVDataManager.Instance.NumericColumnNames.Count;
        for (int i = 0; i < colCount; i++)
            _columnVisibility[i] = true;
    }

    #endregion

    #region Column Visibility

    public void ToggleColumn(int colIndex)
    {
        SetColumnVisible(colIndex, !IsColumnVisible(colIndex));
    }

    public void SetColumnVisible(int colIndex, bool visible)
    {
        _columnVisibility[colIndex] = visible;
        OnColumnToggled?.Invoke(colIndex, visible);

        if (surfaceGenerator != null)
            surfaceGenerator.OnColumnVisibilityChanged();
    }

    public bool IsColumnVisible(int colIndex)
    {
        if (_columnVisibility.TryGetValue(colIndex, out bool visible))
            return visible;
        return true;
    }

    public void ShowAllColumns()
    {
        bool anyChanged = false;
        foreach (int key in new List<int>(_columnVisibility.Keys))
        {
            if (!_columnVisibility[key])
            {
                _columnVisibility[key] = true;
                OnColumnToggled?.Invoke(key, true);
                anyChanged = true;
            }
        }

        if (anyChanged && surfaceGenerator != null)
            surfaceGenerator.OnColumnVisibilityChanged();
    }

    public void HideAllColumns()
    {
        bool anyChanged = false;
        foreach (int key in new List<int>(_columnVisibility.Keys))
        {
            if (_columnVisibility[key])
            {
                _columnVisibility[key] = false;
                OnColumnToggled?.Invoke(key, false);
                anyChanged = true;
            }
        }

        if (anyChanged && surfaceGenerator != null)
            surfaceGenerator.OnColumnVisibilityChanged();
    }

    public void ResetColumns()
    {
        ShowAllColumns();
    }

    #endregion

    #region Industry Filtering

    public void ToggleIndustry(string industry)
    {
        if (CSVDataManager.Instance == null) return;
        bool active = CSVDataManager.Instance.IsIndustryActive(industry);
        SetIndustryActive(industry, !active);
    }

    public void SetIndustryActive(string industry, bool active)
    {
        if (CSVDataManager.Instance != null)
            CSVDataManager.Instance.SetIndustryActive(industry, active);

        OnIndustryChanged?.Invoke(industry, active);
    }

    public bool IsIndustryActive(string industry)
    {
        if (CSVDataManager.Instance == null) return true;
        return CSVDataManager.Instance.IsIndustryActive(industry);
    }

    public void SetAllIndustriesActive(bool active)
    {
        if (CSVDataManager.Instance == null) return;

        CSVDataManager.Instance.BeginBatchUpdate();
        CSVDataManager.Instance.SetAllIndustriesActive(active);
        CSVDataManager.Instance.EndBatchUpdate();

        foreach (string industry in CSVDataManager.Instance.AllIndustries)
            OnIndustryChanged?.Invoke(industry, active);
    }

    public void ResetIndustries()
    {
        SetAllIndustriesActive(true);
    }

    #endregion

    #region Year Filtering

    public void ToggleYear(string year)
    {
        if (CSVDataManager.Instance == null) return;
        bool active = CSVDataManager.Instance.IsYearActive(year);
        SetYearActive(year, !active);
    }

    public void SetYearActive(string year, bool active)
    {
        if (CSVDataManager.Instance != null)
            CSVDataManager.Instance.SetYearActive(year, active);

        OnYearChanged?.Invoke(year, active);
    }

    public bool IsYearActive(string year)
    {
        if (CSVDataManager.Instance == null) return true;
        return CSVDataManager.Instance.IsYearActive(year);
    }

    public void SetAllYearsActive(bool active)
    {
        if (CSVDataManager.Instance == null) return;

        CSVDataManager.Instance.BeginBatchUpdate();
        CSVDataManager.Instance.SetAllYearsActive(active);
        CSVDataManager.Instance.EndBatchUpdate();

        foreach (string year in CSVDataManager.Instance.AllYears)
            OnYearChanged?.Invoke(year, active);
    }

    public void ResetYears()
    {
        SetAllYearsActive(true);
    }

    #endregion

    #region Country Filtering

    public void ToggleCountry(string countryCode)
    {
        if (CSVDataManager.Instance == null) return;
        bool active = CSVDataManager.Instance.IsCountryActive(countryCode);
        SetCountryActive(countryCode, !active);
    }

    public void SetCountryActive(string countryCode, bool active)
    {
        if (CSVDataManager.Instance != null)
            CSVDataManager.Instance.SetCountryActive(countryCode, active);

        OnCountryChanged?.Invoke(countryCode, active);
    }

    public bool IsCountryActive(string countryCode)
    {
        if (CSVDataManager.Instance == null) return true;
        return CSVDataManager.Instance.IsCountryActive(countryCode);
    }

    public void SetAllCountriesActive(bool active)
    {
        if (CSVDataManager.Instance == null) return;

        CSVDataManager.Instance.BeginBatchUpdate();
        CSVDataManager.Instance.SetAllCountriesActive(active);
        CSVDataManager.Instance.EndBatchUpdate();

        foreach (string country in CSVDataManager.Instance.AllCountries)
            OnCountryChanged?.Invoke(country, active);
    }

    public void ResetCountries()
    {
        SetAllCountriesActive(true);
    }

    #endregion

    #region Global Reset

    public void ResetAllFilters()
    {
        CSVDataManager.Instance.BeginBatchUpdate();

        CSVDataManager.Instance.SetAllIndustriesActive(true);
        CSVDataManager.Instance.SetAllYearsActive(true);
        CSVDataManager.Instance.SetAllCountriesActive(true);

        CSVDataManager.Instance.EndBatchUpdate();

        ResetColumns();

        foreach (string industry in CSVDataManager.Instance.AllIndustries)
            OnIndustryChanged?.Invoke(industry, true);
        foreach (string year in CSVDataManager.Instance.AllYears)
            OnYearChanged?.Invoke(year, true);
        foreach (string country in CSVDataManager.Instance.AllCountries)
            OnCountryChanged?.Invoke(country, true);
    }

    #endregion
}