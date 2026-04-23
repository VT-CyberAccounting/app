using System;
using System.Collections.Generic;
using UnityEngine;

public class SurfaceFilterController : MonoBehaviour
{
    public CSVDataSource dataSource;
    public DataSurfaceGenerator surfaceGenerator;

    public event Action<int, bool> OnColumnToggled;
    public event Action<string, bool> OnIndustryChanged;
    public event Action<string, bool> OnYearChanged;
    public event Action<string, bool> OnCountryChanged;
    public event Action OnSortChanged;

    private void Awake()
    {
        if (dataSource == null) dataSource = CSVDataSource.Instance;
    }

    private void Start()
    {
        if (dataSource == null) dataSource = CSVDataSource.Instance;
        if (dataSource == null)
            Debug.LogError($"[SurfaceFilterController:{name}] No CSVDataSource reference set and no singleton available.");
    }

    public void BeginBatch()
    {
        if (dataSource != null) dataSource.BeginBatchUpdate();
    }

    public void EndBatch()
    {
        if (dataSource != null) dataSource.EndBatchUpdate();
    }

    public void ToggleColumn(int colIndex)
    {
        SetColumnVisible(colIndex, !IsColumnVisible(colIndex));
    }

    public void SetColumnVisible(int colIndex, bool visible)
    {
        if (dataSource != null)
            dataSource.SetColumnActive(colIndex, visible);

        OnColumnToggled?.Invoke(colIndex, visible);
    }

    public bool IsColumnVisible(int colIndex)
    {
        if (dataSource == null) return true;
        return dataSource.IsColumnActive(colIndex);
    }

    public void ShowAllColumns()
    {
        if (dataSource == null) return;

        dataSource.BeginBatchUpdate();
        dataSource.SetAllColumnsActive(true);
        dataSource.EndBatchUpdate();

        for (int i = 0; i < dataSource.NumericColumnNames.Count; i++)
            OnColumnToggled?.Invoke(i, true);
    }

    public void HideAllColumns()
    {
        if (dataSource == null) return;

        dataSource.BeginBatchUpdate();
        dataSource.SetAllColumnsActive(false);
        dataSource.EndBatchUpdate();

        for (int i = 0; i < dataSource.NumericColumnNames.Count; i++)
            OnColumnToggled?.Invoke(i, false);
    }

    public void ResetColumns()
    {
        ShowAllColumns();
    }

    public void ToggleIndustry(string industry)
    {
        if (dataSource == null) return;
        bool active = dataSource.IsIndustryActive(industry);
        SetIndustryActive(industry, !active);
    }

    public void SetIndustryActive(string industry, bool active)
    {
        if (dataSource != null)
            dataSource.SetIndustryActive(industry, active);

        OnIndustryChanged?.Invoke(industry, active);
    }

    public bool IsIndustryActive(string industry)
    {
        if (dataSource == null) return true;
        return dataSource.IsIndustryActive(industry);
    }

    public void SetAllIndustriesActive(bool active)
    {
        if (dataSource == null) return;

        dataSource.BeginBatchUpdate();
        dataSource.SetAllIndustriesActive(active);
        dataSource.EndBatchUpdate();

        foreach (string industry in dataSource.AllIndustries)
            OnIndustryChanged?.Invoke(industry, active);
    }

    public void ResetIndustries()
    {
        SetAllIndustriesActive(true);
    }

    public void ToggleYear(string year)
    {
        if (dataSource == null) return;
        bool active = dataSource.IsYearActive(year);
        SetYearActive(year, !active);
    }

    public void SetYearActive(string year, bool active)
    {
        if (dataSource != null)
            dataSource.SetYearActive(year, active);

        OnYearChanged?.Invoke(year, active);
    }

    public bool IsYearActive(string year)
    {
        if (dataSource == null) return true;
        return dataSource.IsYearActive(year);
    }

    public void SetAllYearsActive(bool active)
    {
        if (dataSource == null) return;

        dataSource.BeginBatchUpdate();
        dataSource.SetAllYearsActive(active);
        dataSource.EndBatchUpdate();

        foreach (string year in dataSource.AllYears)
            OnYearChanged?.Invoke(year, active);
    }

    public void ResetYears()
    {
        SetAllYearsActive(true);
    }

    public void ToggleCountry(string countryCode)
    {
        if (dataSource == null) return;
        bool active = dataSource.IsCountryActive(countryCode);
        SetCountryActive(countryCode, !active);
    }

    public void SetCountryActive(string countryCode, bool active)
    {
        if (dataSource != null)
            dataSource.SetCountryActive(countryCode, active);

        OnCountryChanged?.Invoke(countryCode, active);
    }

    public bool IsCountryActive(string countryCode)
    {
        if (dataSource == null) return true;
        return dataSource.IsCountryActive(countryCode);
    }

    public void SetAllCountriesActive(bool active)
    {
        if (dataSource == null) return;

        dataSource.BeginBatchUpdate();
        dataSource.SetAllCountriesActive(active);
        dataSource.EndBatchUpdate();

        foreach (string country in dataSource.AllCountries)
            OnCountryChanged?.Invoke(country, active);
    }

    public void ResetCountries()
    {
        SetAllCountriesActive(true);
    }

    public void ResetAllFilters()
    {
        if (dataSource == null) return;

        dataSource.BeginBatchUpdate();

        dataSource.SetAllIndustriesActive(true);
        dataSource.SetAllYearsActive(true);
        dataSource.SetAllCountriesActive(true);
        dataSource.SetAllColumnsActive(true);
        dataSource.ClearAllSorts();

        dataSource.EndBatchUpdate();

        ResetColumns();

        foreach (string industry in dataSource.AllIndustries)
            OnIndustryChanged?.Invoke(industry, true);
        foreach (string year in dataSource.AllYears)
            OnYearChanged?.Invoke(year, true);
        foreach (string country in dataSource.AllCountries)
            OnCountryChanged?.Invoke(country, true);

        OnSortChanged?.Invoke();
    }

    public void ApplySort(string field, CSVDataSource.SortDirection direction)
    {
        if (dataSource == null) return;
        dataSource.ApplySort(field, direction);
        OnSortChanged?.Invoke();
    }

    public void ClearSort(string field)
    {
        if (dataSource == null) return;
        dataSource.ClearSort(field);
        OnSortChanged?.Invoke();
    }

    public void ClearAllSorts()
    {
        if (dataSource == null) return;
        dataSource.ClearAllSorts();
        OnSortChanged?.Invoke();
    }

    public bool TryGetSortDirection(string field, out CSVDataSource.SortDirection direction)
    {
        if (dataSource == null)
        {
            direction = CSVDataSource.SortDirection.Ascending;
            return false;
        }
        return dataSource.TryGetSortDirection(field, out direction);
    }
}
