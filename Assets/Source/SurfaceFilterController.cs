using System;
using System.Collections.Generic;
using UnityEngine;

public class SurfaceFilterController : MonoBehaviour
{
    public static SurfaceFilterController Instance { get; private set; }

    public DataSurfaceGenerator surfaceGenerator;

    public event Action<int, bool> OnColumnToggled;
    public event Action<string, bool> OnIndustryChanged;
    public event Action<string, bool> OnYearChanged;
    public event Action<string, bool> OnCountryChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ToggleColumn(int colIndex)
    {
        SetColumnVisible(colIndex, !IsColumnVisible(colIndex));
    }

    public void SetColumnVisible(int colIndex, bool visible)
    {
        if (CSVDataManager.Instance != null)
            CSVDataManager.Instance.SetColumnActive(colIndex, visible);

        OnColumnToggled?.Invoke(colIndex, visible);
    }

    public bool IsColumnVisible(int colIndex)
    {
        if (CSVDataManager.Instance == null) return true;
        return CSVDataManager.Instance.IsColumnActive(colIndex);
    }

    public void ShowAllColumns()
    {
        if (CSVDataManager.Instance == null) return;

        CSVDataManager.Instance.BeginBatchUpdate();
        CSVDataManager.Instance.SetAllColumnsActive(true);
        CSVDataManager.Instance.EndBatchUpdate();

        for (int i = 0; i < CSVDataManager.Instance.NumericColumnNames.Count; i++)
            OnColumnToggled?.Invoke(i, true);
    }

    public void HideAllColumns()
    {
        if (CSVDataManager.Instance == null) return;

        CSVDataManager.Instance.BeginBatchUpdate();
        CSVDataManager.Instance.SetAllColumnsActive(false);
        CSVDataManager.Instance.EndBatchUpdate();

        for (int i = 0; i < CSVDataManager.Instance.NumericColumnNames.Count; i++)
            OnColumnToggled?.Invoke(i, false);
    }

    public void ResetColumns()
    {
        ShowAllColumns();
    }

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

    public void ResetAllFilters()
    {
        CSVDataManager.Instance.BeginBatchUpdate();

        CSVDataManager.Instance.SetAllIndustriesActive(true);
        CSVDataManager.Instance.SetAllYearsActive(true);
        CSVDataManager.Instance.SetAllCountriesActive(true);
        CSVDataManager.Instance.SetAllColumnsActive(true);

        CSVDataManager.Instance.EndBatchUpdate();

        ResetColumns();

        foreach (string industry in CSVDataManager.Instance.AllIndustries)
            OnIndustryChanged?.Invoke(industry, true);
        foreach (string year in CSVDataManager.Instance.AllYears)
            OnYearChanged?.Invoke(year, true);
        foreach (string country in CSVDataManager.Instance.AllCountries)
            OnCountryChanged?.Invoke(country, true);
    }
}