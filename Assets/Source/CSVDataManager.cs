using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class CSVDataManager : MonoBehaviour
{
    #region Singleton

    public static CSVDataManager Instance { get; private set; }

    #endregion

    #region Data Structures

    public struct DataRow
    {
        public string Ticker;
        public string CompanyName;
        public string Year;
        public string CountryCode;
        public string City;
        public int SICCode;
        public string Industry;
        public float[] NumericValues;
    }

    #endregion

    #region Inspector Fields

    [Header("CSV Settings")]
    public string csvFileName = "Case_Study_Data.csv";

    #endregion

    #region Public Properties

    public List<DataRow> AllRows => _allRows;
    public List<DataRow> FilteredRows => _filteredRows;
    public List<string> NumericColumnNames => _numericColumnNames;
    public List<string> AllIndustries => _allIndustries;
    public List<string> AllYears => _allYears;
    public List<string> AllCountries => _allCountries;
    public HashSet<string> ActiveIndustries => _activeIndustries;
    public HashSet<string> ActiveYears => _activeYears;
    public HashSet<string> ActiveCountries => _activeCountries;
    public bool IsLoaded => _isLoaded;

    #endregion

    #region Events

    public event Action OnDataLoaded;
    public event Action OnFilterChanged;

    #endregion

    #region Private Fields

    private List<DataRow> _allRows = new List<DataRow>(2048);
    private List<DataRow> _filteredRows = new List<DataRow>(2048);
    private List<string> _numericColumnNames = new List<string>();
    private Dictionary<string, int> _headerIndex = new Dictionary<string, int>();
    private bool _isLoaded;
    private bool _suppressFilterEvents;

    private HashSet<string> _activeIndustries = new HashSet<string>();
    private HashSet<string> _activeYears = new HashSet<string>();
    private HashSet<string> _activeCountries = new HashSet<string>();

    private List<string> _allIndustries = new List<string>();
    private List<string> _allYears = new List<string>();
    private List<string> _allCountries = new List<string>();

    private float[] _columnMins;
    private float[] _columnMaxes;

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
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StartCoroutine(LoadCSVCoroutine());
    }

    #endregion

    #region CSV Parsing

    private System.Collections.IEnumerator LoadCSVCoroutine()
    {
        string path = Path.Combine(Application.streamingAssetsPath, csvFileName);
        string csvText = null;

#if UNITY_ANDROID && !UNITY_EDITOR
        using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(path))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[CSVDataManager] Failed to load CSV: {www.error}");
                yield break;
            }

            csvText = www.downloadHandler.text;
        }
#else
        if (!File.Exists(path))
        {
            Debug.LogError($"[CSVDataManager] File not found: {path}");
            yield break;
        }

        csvText = File.ReadAllText(path);
#endif

        ProcessCSV(csvText);
    }

    private void ProcessCSV(string csvText)
    {
        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length < 2) return;

        List<string> headers = ParseCSVLine(lines[0]);
        for (int i = 0; i < headers.Count; i++)
            _headerIndex[headers[i].Trim()] = i;

        IdentifyNumericColumns(headers);

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
                NumericValues = new float[_numericColumnNames.Count]
            };

            for (int j = 0; j < _numericColumnNames.Count; j++)
                row.NumericValues[j] = ParseFloat(GetField(fields, _numericColumnNames[j]));

            _allRows.Add(row);
        }

        _isLoaded = true;
        CollectDistinctValues();
        RebuildFilter();

        Debug.Log($"[CSVDataManager] Loaded {_allRows.Count} rows, {_numericColumnNames.Count} numeric columns, {_allIndustries.Count} industries, {_allYears.Count} years, {_allCountries.Count} countries");
        OnDataLoaded?.Invoke();
    }

    private void CollectDistinctValues()
    {
        HashSet<string> industries = new HashSet<string>();
        HashSet<string> years = new HashSet<string>();
        HashSet<string> countries = new HashSet<string>();

        for (int i = 0; i < _allRows.Count; i++)
        {
            if (!string.IsNullOrEmpty(_allRows[i].Industry))
                industries.Add(_allRows[i].Industry);
            if (!string.IsNullOrEmpty(_allRows[i].Year))
                years.Add(_allRows[i].Year);
            if (!string.IsNullOrEmpty(_allRows[i].CountryCode))
                countries.Add(_allRows[i].CountryCode);
        }

        _allIndustries = new List<string>(industries);
        _allIndustries.Sort();
        _allYears = new List<string>(years);
        _allYears.Sort();
        _allCountries = new List<string>(countries);
        _allCountries.Sort();

        _activeIndustries = new HashSet<string>(industries);
        _activeYears = new HashSet<string>(years);
        _activeCountries = new HashSet<string>(countries);
    }

    private List<string> ParseCSVLine(string line)
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

    #endregion

    #region Batch Updates

    public void BeginBatchUpdate()
    {
        _suppressFilterEvents = true;
    }

    public void EndBatchUpdate()
    {
        _suppressFilterEvents = false;
        RebuildFilter();
        OnFilterChanged?.Invoke();
    }

    #endregion

    #region Industry Filtering

    public void SetIndustryActive(string industry, bool active)
    {
        if (active) _activeIndustries.Add(industry);
        else _activeIndustries.Remove(industry);

        if (_suppressFilterEvents) return;
        RebuildFilter();
        OnFilterChanged?.Invoke();
    }

    public void SetAllIndustriesActive(bool active)
    {
        _activeIndustries.Clear();
        if (active)
        {
            for (int i = 0; i < _allIndustries.Count; i++)
                _activeIndustries.Add(_allIndustries[i]);
        }

        if (_suppressFilterEvents) return;
        RebuildFilter();
        OnFilterChanged?.Invoke();
    }

    public bool IsIndustryActive(string industry)
    {
        return _activeIndustries.Contains(industry);
    }

    #endregion

    #region Year Filtering

    public void SetYearActive(string year, bool active)
    {
        if (active) _activeYears.Add(year);
        else _activeYears.Remove(year);

        if (_suppressFilterEvents) return;
        RebuildFilter();
        OnFilterChanged?.Invoke();
    }

    public void SetAllYearsActive(bool active)
    {
        _activeYears.Clear();
        if (active)
        {
            for (int i = 0; i < _allYears.Count; i++)
                _activeYears.Add(_allYears[i]);
        }

        if (_suppressFilterEvents) return;
        RebuildFilter();
        OnFilterChanged?.Invoke();
    }

    public bool IsYearActive(string year)
    {
        return _activeYears.Contains(year);
    }

    #endregion

    #region Country Filtering

    public void SetCountryActive(string countryCode, bool active)
    {
        if (active) _activeCountries.Add(countryCode);
        else _activeCountries.Remove(countryCode);

        if (_suppressFilterEvents) return;
        RebuildFilter();
        OnFilterChanged?.Invoke();
    }

    public void SetAllCountriesActive(bool active)
    {
        _activeCountries.Clear();
        if (active)
        {
            for (int i = 0; i < _allCountries.Count; i++)
                _activeCountries.Add(_allCountries[i]);
        }

        if (_suppressFilterEvents) return;
        RebuildFilter();
        OnFilterChanged?.Invoke();
    }

    public bool IsCountryActive(string countryCode)
    {
        return _activeCountries.Contains(countryCode);
    }

    #endregion

    #region Filter Rebuild

    public void ClearAllFilters()
    {
        _activeIndustries = new HashSet<string>(_allIndustries);
        _activeYears = new HashSet<string>(_allYears);
        _activeCountries = new HashSet<string>(_allCountries);
        RebuildFilter();
        OnFilterChanged?.Invoke();
    }

    private void RebuildFilter()
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

        RecalculateColumnRanges();
    }

    #endregion

    #region Normalization

    private void RecalculateColumnRanges()
    {
        int colCount = _numericColumnNames.Count;
        _columnMins = new float[colCount];
        _columnMaxes = new float[colCount];

        for (int c = 0; c < colCount; c++)
        {
            float min = float.MaxValue;
            float max = float.MinValue;

            for (int r = 0; r < _filteredRows.Count; r++)
            {
                float val = _filteredRows[r].NumericValues[c];
                if (val < min) min = val;
                if (val > max) max = val;
            }

            if (_filteredRows.Count == 0) { min = 0f; max = 1f; }
            else if (Mathf.Approximately(min, max)) { max = min + 1f; }

            _columnMins[c] = min;
            _columnMaxes[c] = max;
        }
    }

    public float GetNormalizedValue(int rowIndex, int colIndex)
    {
        if (rowIndex >= _filteredRows.Count || colIndex >= _numericColumnNames.Count)
            return 0f;

        float raw = _filteredRows[rowIndex].NumericValues[colIndex];
        float min = _columnMins[colIndex];
        float max = _columnMaxes[colIndex];

        return (raw - min) / (max - min);
    }

    public float GetRawValue(int rowIndex, int colIndex)
    {
        if (rowIndex >= _filteredRows.Count || colIndex >= _numericColumnNames.Count)
            return 0f;
        return _filteredRows[rowIndex].NumericValues[colIndex];
    }

    public (float min, float max) GetColumnRange(int colIndex)
    {
        return (_columnMins[colIndex], _columnMaxes[colIndex]);
    }

    #endregion

    #region Utility

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

    #endregion
}