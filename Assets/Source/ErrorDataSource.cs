using System.Collections.Generic;
using UnityEngine;

public class ErrorDataSource : SurfaceDataSource
{
    public SurfaceDataSource solutionSource;
    public SurfaceDataSource answerSource;
    public bool autoRebuild = true;
    public bool useSignedDifference = false;

    public const float MissingRowError = 1f;

    private readonly List<string> _sharedColumns = new List<string>();
    private readonly Dictionary<string, int> _answerKeyToIndex = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _answerColIndex = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _solutionColIndex = new Dictionary<string, int>();
    private int[] _answerColMap;
    private int[] _solutionColMap;
    private float[] _jointMin;
    private float[] _jointMax;
    private int _cachedSolutionColsHash;
    private int _cachedAnswerColsHash;

    private void OnEnable()
    {
        if (solutionSource != null)
        {
            solutionSource.OnDataLoaded += HandleSourceChanged;
            solutionSource.OnFilterChanged += HandleSourceChanged;
        }
        if (answerSource != null)
        {
            answerSource.OnDataLoaded += HandleSourceChanged;
            answerSource.OnFilterChanged += HandleSourceChanged;
        }
        HandleSourceChanged();
    }

    private void OnDisable()
    {
        if (solutionSource != null)
        {
            solutionSource.OnDataLoaded -= HandleSourceChanged;
            solutionSource.OnFilterChanged -= HandleSourceChanged;
        }
        if (answerSource != null)
        {
            answerSource.OnDataLoaded -= HandleSourceChanged;
            answerSource.OnFilterChanged -= HandleSourceChanged;
        }
    }

    private void HandleSourceChanged()
    {
        if (autoRebuild) RebuildDiff();
    }

    public void RebuildDiff()
    {
        if (solutionSource == null || answerSource == null) return;
        if (!solutionSource.IsLoaded || !answerSource.IsLoaded) return;

        RefreshColumnSchemaIfNeeded();
        if (_sharedColumns.Count == 0)
        {
            Debug.LogWarning($"[ErrorDataSource:{name}] No formula columns shared between solution and answer.");
            ClearAndNotify();
            return;
        }

        BuildAnswerIndex();

        List<DataRow> solutionRows = solutionSource.FilteredRows;
        List<DataRow> answerRows = answerSource.FilteredRows;
        int sharedCount = _sharedColumns.Count;

        ComputeJointRanges(solutionRows, answerRows, sharedCount);

        _allRows.Clear();
        _activeColumns.Clear();
        for (int i = 0; i < _numericColumnNames.Count; i++)
            _activeColumns.Add(i);

        for (int s = 0; s < solutionRows.Count; s++)
        {
            DataRow sol = solutionRows[s];
            string key = MakeKey(sol.Ticker, sol.Year);
            float[] values = new float[sharedCount];

            if (_answerKeyToIndex.TryGetValue(key, out int aIdx))
            {
                DataRow ans = answerRows[aIdx];
                for (int c = 0; c < sharedCount; c++)
                {
                    float range = _jointMax[c] - _jointMin[c];
                    float solN = range > 0f ? (sol.NumericValues[_solutionColMap[c]] - _jointMin[c]) / range : 0f;
                    float ansN = range > 0f ? (ans.NumericValues[_answerColMap[c]] - _jointMin[c]) / range : 0f;
                    float diff = solN - ansN;
                    values[c] = useSignedDifference ? diff : Mathf.Abs(diff);
                }
            }
            else
            {
                for (int c = 0; c < sharedCount; c++)
                    values[c] = MissingRowError;
            }

            _allRows.Add(new DataRow
            {
                Ticker = sol.Ticker,
                CompanyName = sol.CompanyName,
                Year = sol.Year,
                CountryCode = sol.CountryCode,
                City = sol.City,
                SICCode = sol.SICCode,
                Industry = sol.Industry,
                NumericValues = values
            });
        }

        RebuildFilter();
        Debug.Log($"[ErrorDataSource:{name}] Built diff: {_allRows.Count} rows, {_numericColumnNames.Count} formula columns.");
        if (!_isLoaded) RaiseDataLoaded();
        else RaiseFilterChanged();
    }

    private void ComputeJointRanges(List<DataRow> solutionRows, List<DataRow> answerRows, int sharedCount)
    {
        if (_jointMin == null || _jointMin.Length != sharedCount) _jointMin = new float[sharedCount];
        if (_jointMax == null || _jointMax.Length != sharedCount) _jointMax = new float[sharedCount];

        for (int c = 0; c < sharedCount; c++)
        {
            _jointMin[c] = float.MaxValue;
            _jointMax[c] = float.MinValue;
        }

        for (int r = 0; r < solutionRows.Count; r++)
        {
            float[] v = solutionRows[r].NumericValues;
            for (int c = 0; c < sharedCount; c++)
            {
                float x = v[_solutionColMap[c]];
                if (!float.IsFinite(x)) continue;
                if (x < _jointMin[c]) _jointMin[c] = x;
                if (x > _jointMax[c]) _jointMax[c] = x;
            }
        }
        for (int r = 0; r < answerRows.Count; r++)
        {
            float[] v = answerRows[r].NumericValues;
            for (int c = 0; c < sharedCount; c++)
            {
                float x = v[_answerColMap[c]];
                if (!float.IsFinite(x)) continue;
                if (x < _jointMin[c]) _jointMin[c] = x;
                if (x > _jointMax[c]) _jointMax[c] = x;
            }
        }

        for (int c = 0; c < sharedCount; c++)
        {
            if (_jointMin[c] == float.MaxValue) { _jointMin[c] = 0f; _jointMax[c] = 1f; }
            else if (_jointMax[c] <= _jointMin[c]) _jointMax[c] = _jointMin[c] + 1f;
        }
    }

    private void RefreshColumnSchemaIfNeeded()
    {
        int solHash = ColumnListHash(solutionSource.NumericColumnNames);
        int ansHash = ColumnListHash(answerSource.NumericColumnNames);
        if (solHash == _cachedSolutionColsHash && ansHash == _cachedAnswerColsHash && _sharedColumns.Count > 0)
            return;

        _cachedSolutionColsHash = solHash;
        _cachedAnswerColsHash = ansHash;

        _answerColIndex.Clear();
        List<string> ansCols = answerSource.NumericColumnNames;
        for (int i = 0; i < ansCols.Count; i++)
            _answerColIndex[ansCols[i]] = i;

        _solutionColIndex.Clear();
        List<string> solCols = solutionSource.NumericColumnNames;
        for (int i = 0; i < solCols.Count; i++)
            _solutionColIndex[solCols[i]] = i;

        _sharedColumns.Clear();
        string[] formulaCols = FormulaCalculator.FormulaColumns;
        for (int i = 0; i < formulaCols.Length; i++)
        {
            string col = formulaCols[i];
            if (_solutionColIndex.ContainsKey(col) && _answerColIndex.ContainsKey(col))
                _sharedColumns.Add(col);
        }

        _numericColumnNames.Clear();
        _numericColumnNames.AddRange(_sharedColumns);

        if (_answerColMap == null || _answerColMap.Length != _sharedColumns.Count)
            _answerColMap = new int[_sharedColumns.Count];
        if (_solutionColMap == null || _solutionColMap.Length != _sharedColumns.Count)
            _solutionColMap = new int[_sharedColumns.Count];

        for (int i = 0; i < _sharedColumns.Count; i++)
        {
            _answerColMap[i] = _answerColIndex[_sharedColumns[i]];
            _solutionColMap[i] = _solutionColIndex[_sharedColumns[i]];
        }
    }

    private void BuildAnswerIndex()
    {
        _answerKeyToIndex.Clear();
        List<DataRow> rows = answerSource.FilteredRows;
        for (int i = 0; i < rows.Count; i++)
        {
            string key = MakeKey(rows[i].Ticker, rows[i].Year);
            if (!_answerKeyToIndex.ContainsKey(key))
                _answerKeyToIndex[key] = i;
        }
    }

    private static int ColumnListHash(List<string> cols)
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < cols.Count; i++)
                hash = hash * 31 + (cols[i]?.GetHashCode() ?? 0);
            return hash;
        }
    }

    private static string MakeKey(string ticker, string year) => ticker + "|" + year;

    private void ClearAndNotify()
    {
        _allRows.Clear();
        _filteredRows.Clear();
        _numericColumnNames.Clear();
        _activeColumns.Clear();
        RebuildFilter();
        if (!_isLoaded) RaiseDataLoaded();
        else RaiseFilterChanged();
    }
}
