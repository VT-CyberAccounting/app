using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class CSVDataSource : SurfaceDataSource
{
    public enum SortDirection { Ascending, Descending }

    public struct SortCriterion
    {
        public string Field;
        public SortDirection Direction;
    }

    public const string SortFieldIndustry = "Industry";
    public const string SortFieldYear = "Year";
    public const string SortFieldCountry = "Country";

    public static CSVDataSource Instance { get; private set; }

    public string csvFileName = "Case_Study_Data.csv";
    public bool loadOnStart = true;
    public bool claimSingleton = true;

    public List<string> AllIndustries => _allIndustries;
    public List<string> AllYears => _allYears;
    public List<string> AllCountries => _allCountries;
    public HashSet<string> ActiveIndustries => _activeIndustries;
    public HashSet<string> ActiveYears => _activeYears;
    public HashSet<string> ActiveCountries => _activeCountries;

    private Dictionary<string, int> _headerIndex = new Dictionary<string, int>();

    private HashSet<string> _activeIndustries = new HashSet<string>();
    private HashSet<string> _activeYears = new HashSet<string>();
    private HashSet<string> _activeCountries = new HashSet<string>();

    private List<string> _allIndustries = new List<string>();
    private List<string> _allYears = new List<string>();
    private List<string> _allCountries = new List<string>();

    private List<SortCriterion> _sortStack = new List<SortCriterion>(3);

    private static readonly string[] _industryFlags = {
        "Mining", "Construction", "Manufactuing",
        "Transportation Public Utilities", "Wholesale Trade",
        "Retail Trade", "Services"
    };

    private static readonly HashSet<string> _metadataColumnNames = new HashSet<string> {
        "Ticker", "YEAR", "GVKEY", "Address Line 1", "Postal Code",
        "City", "Mining", "Construction", "Manufactuing",
        "Transportation Public Utilities", "Wholesale Trade",
        "Retail Trade", "Services", "SIC code", "Company Name",
        "CIK Number", "Country Code"
    };

    private void Awake()
    {
        if (claimSingleton && Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        if (loadOnStart)
            StartCoroutine(LoadFromStreamingAssets(csvFileName));
    }

    public System.Collections.IEnumerator LoadFromStreamingAssets(string fileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        string url = path.Contains("://") ? path : "file://" + path;
        string csvText;

        using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[CSVDataSource:{name}] Failed to load CSV: {www.error}");
                _allRows.Clear();
                _filteredRows.Clear();
                _numericColumnNames.Clear();
                _activeColumns.Clear();
                RaiseDataLoaded();
                yield break;
            }

            csvText = www.downloadHandler.text;
        }

        LoadFromCsvText(csvText);
    }

    public void LoadFromPath(string absolutePath)
    {
        if (!File.Exists(absolutePath))
        {
            Debug.LogError($"[CSVDataSource] File not found: {absolutePath}");
            return;
        }
        LoadFromCsvText(File.ReadAllText(absolutePath));
    }

    public void LoadFromCsvText(string csvText)
    {
        _allRows.Clear();
        _filteredRows.Clear();
        _numericColumnNames.Clear();
        _headerIndex.Clear();
        _activeColumns.Clear();

        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length < 2) return;

        List<string> headers = ParseCSVLine(lines[0]);
        for (int i = 0; i < headers.Count; i++)
            _headerIndex[headers[i].Trim()] = i;

        IdentifyNumericColumns(headers);

        for (int i = 0; i < _numericColumnNames.Count; i++)
            _activeColumns.Add(i);

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            List<string> fields = ParseCSVLine(lines[i]);
            if (fields.Count < headers.Count) continue;

            DataRow row = new DataRow
            {
                Ticker = GetField(fields, "Ticker"),
                CompanyName = GetField(fields, "Company Name"),
                Year = GetField(fields, "YEAR"),
                CountryCode = GetField(fields, "Country Code"),
                City = GetField(fields, "City"),
                SICCode = ParseInt(GetField(fields, "SIC code")),
                Industry = ResolveIndustry(fields),
                NumericValues = new float[_numericColumnNames.Count],
                OriginalIndex = _allRows.Count
            };

            for (int j = 0; j < _numericColumnNames.Count; j++)
                row.NumericValues[j] = ParseFloat(GetField(fields, _numericColumnNames[j]));

            _allRows.Add(row);
        }

        CollectDistinctValues();
        RebuildFilter();

        Debug.Log($"[CSVDataSource:{name}] Loaded {_allRows.Count} rows, {_numericColumnNames.Count} numeric columns, {_allIndustries.Count} industries, {_allYears.Count} years, {_allCountries.Count} countries");
        RaiseDataLoaded();
    }

    private void CollectDistinctValues()
    {
        _activeIndustries.Clear();
        _activeYears.Clear();
        _activeCountries.Clear();

        for (int i = 0; i < _allRows.Count; i++)
        {
            if (!string.IsNullOrEmpty(_allRows[i].Industry))
                _activeIndustries.Add(_allRows[i].Industry);
            if (!string.IsNullOrEmpty(_allRows[i].Year))
                _activeYears.Add(_allRows[i].Year);
            if (!string.IsNullOrEmpty(_allRows[i].CountryCode))
                _activeCountries.Add(_allRows[i].CountryCode);
        }

        _allIndustries.Clear();
        _allIndustries.AddRange(_activeIndustries);
        _allIndustries.Sort();

        _allYears.Clear();
        _allYears.AddRange(_activeYears);
        _allYears.Sort();

        _allCountries.Clear();
        _allCountries.AddRange(_activeCountries);
        _allCountries.Sort();
    }

    private static List<string> ParseCSVLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        var sb = new System.Text.StringBuilder(64);

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }

    private void IdentifyNumericColumns(List<string> headers)
    {
        _numericColumnNames.Clear();
        foreach (string h in headers)
        {
            string trimmed = h.Trim();
            if (!_metadataColumnNames.Contains(trimmed))
                _numericColumnNames.Add(trimmed);
        }
    }

    private string GetField(List<string> fields, string columnName)
    {
        if (_headerIndex.TryGetValue(columnName, out int idx) && idx < fields.Count)
            return fields[idx].Trim();
        return "";
    }

    private string ResolveIndustry(List<string> fields)
    {
        foreach (string ind in _industryFlags)
        {
            if (GetField(fields, ind) == "1")
                return ind;
        }
        return "Unknown";
    }

    public void SetIndustryActive(string industry, bool active)
    {
        bool currentlyActive = _activeIndustries.Contains(industry);
        if (currentlyActive == active) return;

        if (active) _activeIndustries.Add(industry);
        else _activeIndustries.Remove(industry);

        NotifyChanged();
    }

    public void SetAllIndustriesActive(bool active)
    {
        int total = _allIndustries.Count;
        bool changed;
        if (active)
        {
            changed = _activeIndustries.Count != total;
            if (changed)
            {
                _activeIndustries.Clear();
                for (int i = 0; i < total; i++)
                    _activeIndustries.Add(_allIndustries[i]);
            }
        }
        else
        {
            changed = _activeIndustries.Count > 0;
            if (changed) _activeIndustries.Clear();
        }

        if (!changed) return;
        NotifyChanged();
    }

    public bool IsIndustryActive(string industry)
    {
        return _activeIndustries.Contains(industry);
    }

    public void SetYearActive(string year, bool active)
    {
        bool currentlyActive = _activeYears.Contains(year);
        if (currentlyActive == active) return;

        if (active) _activeYears.Add(year);
        else _activeYears.Remove(year);

        NotifyChanged();
    }

    public void SetAllYearsActive(bool active)
    {
        int total = _allYears.Count;
        bool changed;
        if (active)
        {
            changed = _activeYears.Count != total;
            if (changed)
            {
                _activeYears.Clear();
                for (int i = 0; i < total; i++)
                    _activeYears.Add(_allYears[i]);
            }
        }
        else
        {
            changed = _activeYears.Count > 0;
            if (changed) _activeYears.Clear();
        }

        if (!changed) return;
        NotifyChanged();
    }

    public bool IsYearActive(string year)
    {
        return _activeYears.Contains(year);
    }

    public void SetCountryActive(string countryCode, bool active)
    {
        bool currentlyActive = _activeCountries.Contains(countryCode);
        if (currentlyActive == active) return;

        if (active) _activeCountries.Add(countryCode);
        else _activeCountries.Remove(countryCode);

        NotifyChanged();
    }

    public void SetAllCountriesActive(bool active)
    {
        int total = _allCountries.Count;
        bool changed;
        if (active)
        {
            changed = _activeCountries.Count != total;
            if (changed)
            {
                _activeCountries.Clear();
                for (int i = 0; i < total; i++)
                    _activeCountries.Add(_allCountries[i]);
            }
        }
        else
        {
            changed = _activeCountries.Count > 0;
            if (changed) _activeCountries.Clear();
        }

        if (!changed) return;
        NotifyChanged();
    }

    public bool IsCountryActive(string countryCode)
    {
        return _activeCountries.Contains(countryCode);
    }

    public void ClearAllFilters()
    {
        _activeIndustries.Clear();
        for (int i = 0; i < _allIndustries.Count; i++)
            _activeIndustries.Add(_allIndustries[i]);

        _activeYears.Clear();
        for (int i = 0; i < _allYears.Count; i++)
            _activeYears.Add(_allYears[i]);

        _activeCountries.Clear();
        for (int i = 0; i < _allCountries.Count; i++)
            _activeCountries.Add(_allCountries[i]);

        _activeColumns.Clear();
        for (int i = 0; i < _numericColumnNames.Count; i++)
            _activeColumns.Add(i);
        RebuildFilter();
        RaiseFilterChanged();
    }

    public void ApplySort(string field, SortDirection direction)
    {
        if (_sortStack.Count > 0 && _sortStack[0].Field == field && _sortStack[0].Direction == direction)
            return;

        for (int i = 0; i < _sortStack.Count; i++)
        {
            if (_sortStack[i].Field == field)
            {
                _sortStack.RemoveAt(i);
                break;
            }
        }
        _sortStack.Insert(0, new SortCriterion { Field = field, Direction = direction });

        NotifyChanged();
    }

    public void ClearSort(string field)
    {
        bool changed = false;
        for (int i = 0; i < _sortStack.Count; i++)
        {
            if (_sortStack[i].Field == field)
            {
                _sortStack.RemoveAt(i);
                changed = true;
                break;
            }
        }
        if (!changed) return;
        NotifyChanged();
    }

    public void ClearAllSorts()
    {
        if (_sortStack.Count == 0) return;
        _sortStack.Clear();
        NotifyChanged();
    }

    public override int SortDepth => _sortStack.Count;

    public override string GetSortFieldAt(int stackIndex)
    {
        if (stackIndex < 0 || stackIndex >= _sortStack.Count) return null;
        return _sortStack[stackIndex].Field;
    }

    public override string GetRowSortKey(int filteredRowIndex, string sortField)
    {
        if (filteredRowIndex < 0 || filteredRowIndex >= _filteredRows.Count) return null;
        DataRow row = _filteredRows[filteredRowIndex];
        switch (sortField)
        {
            case SortFieldIndustry: return row.Industry ?? "";
            case SortFieldYear: return row.Year ?? "";
            case SortFieldCountry: return row.CountryCode ?? "";
            default: return null;
        }
    }

    public override string GetSortSectionDisplayValue(string sortField, string sectionKey)
    {
        if (string.IsNullOrEmpty(sectionKey)) return "";
        if (sortField == SortFieldCountry)
            return ResolveCountryName(sectionKey);
        return sectionKey;
    }

    public bool TryGetSortDirection(string field, out SortDirection direction)
    {
        for (int i = 0; i < _sortStack.Count; i++)
        {
            if (_sortStack[i].Field == field)
            {
                direction = _sortStack[i].Direction;
                return true;
            }
        }
        direction = SortDirection.Ascending;
        return false;
    }

    protected override void RebuildFilter()
    {
        _filteredRows.Clear();

        for (int i = 0; i < _allRows.Count; i++)
        {
            DataRow row = _allRows[i];

            if (!_activeCountries.Contains(row.CountryCode)) continue;
            if (!_activeYears.Contains(row.Year)) continue;
            if (!_activeIndustries.Contains(row.Industry)) continue;

            _filteredRows.Add(row);
        }

        ApplySortToFilteredRows();
        RecalculateColumnRanges();
    }

    private void ApplySortToFilteredRows()
    {
        if (_sortStack.Count == 0) return;

        _filteredRows.Sort((a, b) =>
        {
            for (int i = 0; i < _sortStack.Count; i++)
            {
                SortCriterion crit = _sortStack[i];
                int cmp = CompareByField(a, b, crit.Field);
                if (cmp != 0)
                    return crit.Direction == SortDirection.Ascending ? cmp : -cmp;
            }
            return a.OriginalIndex.CompareTo(b.OriginalIndex);
        });
    }

    private static int CompareByField(DataRow a, DataRow b, string field)
    {
        switch (field)
        {
            case SortFieldIndustry:
                return string.Compare(a.Industry ?? "", b.Industry ?? "", System.StringComparison.OrdinalIgnoreCase);
            case SortFieldYear:
                int yearA = ParseInt(a.Year);
                int yearB = ParseInt(b.Year);
                return yearA.CompareTo(yearB);
            case SortFieldCountry:
                return string.Compare(ResolveCountryName(a.CountryCode), ResolveCountryName(b.CountryCode), System.StringComparison.OrdinalIgnoreCase);
            default:
                return 0;
        }
    }

    private static string ResolveCountryName(string code)
    {
        if (string.IsNullOrEmpty(code)) return "";
        return CountryNames.GetFullName(code);
    }

    private static float ParseFloat(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0f;
        if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out float result))
            return result;
        return 0f;
    }

    private static int ParseInt(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        if (int.TryParse(value, out int result)) return result;
        return 0;
    }
}
