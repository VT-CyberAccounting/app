using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class SurfaceDataSource : MonoBehaviour
{
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
        public int OriginalIndex;
    }

    public List<DataRow> AllRows => _allRows;
    public List<DataRow> FilteredRows => _filteredRows;
    public List<string> NumericColumnNames => _numericColumnNames;
    public bool IsLoaded => _isLoaded;

    public event Action OnDataLoaded;
    public event Action OnFilterChanged;

    protected List<DataRow> _allRows = new List<DataRow>(2048);
    protected List<DataRow> _filteredRows = new List<DataRow>(2048);
    protected List<string> _numericColumnNames = new List<string>();
    protected HashSet<int> _activeColumns = new HashSet<int>();
    protected float[] _columnMins;
    protected float[] _columnMaxes;
    protected bool _isLoaded;
    protected bool _suppressFilterEvents;
    protected bool _batchDirty;

    private string[] _sectionFieldsBuffer;
    private string[] _sectionKeysBuffer;

    public void BeginBatchUpdate()
    {
        _suppressFilterEvents = true;
        _batchDirty = false;
    }

    public void EndBatchUpdate()
    {
        _suppressFilterEvents = false;
        if (!_batchDirty) return;
        _batchDirty = false;
        RebuildFilter();
        OnFilterChanged?.Invoke();
    }

    protected void NotifyChanged()
    {
        if (_suppressFilterEvents)
        {
            _batchDirty = true;
            return;
        }
        RebuildFilter();
        OnFilterChanged?.Invoke();
    }

    public bool IsColumnActive(int colIndex)
    {
        return _activeColumns.Contains(colIndex);
    }

    public void SetColumnActive(int colIndex, bool active)
    {
        bool currentlyActive = _activeColumns.Contains(colIndex);
        if (currentlyActive == active) return;

        if (active) _activeColumns.Add(colIndex);
        else _activeColumns.Remove(colIndex);

        NotifyChanged();
    }

    public void SetAllColumnsActive(bool active)
    {
        int total = _numericColumnNames.Count;
        bool changed;
        if (active)
        {
            changed = _activeColumns.Count != total;
            if (changed)
            {
                _activeColumns.Clear();
                for (int i = 0; i < total; i++)
                    _activeColumns.Add(i);
            }
        }
        else
        {
            changed = _activeColumns.Count > 0;
            if (changed) _activeColumns.Clear();
        }

        if (!changed) return;
        NotifyChanged();
    }

    protected virtual void RebuildFilter()
    {
        _filteredRows.Clear();
        for (int i = 0; i < _allRows.Count; i++)
            _filteredRows.Add(_allRows[i]);

        RecalculateColumnRanges();
    }

    protected void RecalculateColumnRanges()
    {
        int colCount = _numericColumnNames.Count;
        _columnMins = new float[colCount];
        _columnMaxes = new float[colCount];

        for (int c = 0; c < colCount; c++)
        {
            if (!_activeColumns.Contains(c))
            {
                _columnMins[c] = 0f;
                _columnMaxes[c] = 1f;
                continue;
            }

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

    public virtual int SortDepth => 0;

    public virtual string GetSortFieldAt(int stackIndex) => null;

    public virtual string GetRowSortKey(int filteredRowIndex, string sortField) => null;

    public virtual string GetSortSectionDisplayValue(string sortField, string sectionKey) => sectionKey;

    public virtual float GetColumnAverage(int dataColIndex)
    {
        if (dataColIndex < 0 || dataColIndex >= _numericColumnNames.Count) return 0f;
        if (_filteredRows.Count == 0) return 0f;

        double sum = 0.0;
        for (int r = 0; r < _filteredRows.Count; r++)
            sum += _filteredRows[r].NumericValues[dataColIndex];

        return (float)(sum / _filteredRows.Count);
    }

    public virtual int GetSectionRowCount(int startRow, int tier)
    {
        if (startRow < 0 || startRow >= _filteredRows.Count) return 0;
        int depth = SortDepth;
        if (tier < 0 || tier >= depth) return 0;

        int levels = tier + 1;
        if (_sectionFieldsBuffer == null || _sectionFieldsBuffer.Length < levels)
        {
            _sectionFieldsBuffer = new string[levels];
            _sectionKeysBuffer = new string[levels];
        }
        for (int t = 0; t < levels; t++)
        {
            _sectionFieldsBuffer[t] = GetSortFieldAt(t);
            _sectionKeysBuffer[t] = GetRowSortKey(startRow, _sectionFieldsBuffer[t]);
        }

        int count = 0;
        for (int r = startRow; r < _filteredRows.Count; r++)
        {
            for (int t = 0; t < levels; t++)
            {
                if (!string.Equals(GetRowSortKey(r, _sectionFieldsBuffer[t]), _sectionKeysBuffer[t]))
                    return count;
            }
            count++;
        }
        return count;
    }

    protected void RaiseDataLoaded()
    {
        _isLoaded = true;
        OnDataLoaded?.Invoke();
    }

    protected void RaiseFilterChanged()
    {
        OnFilterChanged?.Invoke();
    }
}
