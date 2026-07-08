using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public abstract class DataSource : MonoBehaviour
{
    public enum SortMode { Original, Ascending, Descending }

    public IReadOnlyList<string> ColumnTitles => _columnTitles;
    public IReadOnlyList<string> RowTitles => _rowTitles;
    public int ColumnCount => _columnTitles.Count;
    public int RowCount => _rowTitles.Count;

    public string ColumnAxisTitle => _columnAxisTitle;
    public string RowAxisTitle => _rowAxisTitle;

    public IReadOnlyList<int> VisibleColumnIndices => _visibleColumnIndices;
    public IReadOnlyList<int> VisibleRowIndices => _visibleRowIndices;

    public IReadOnlyList<int> ColumnOrder => _columnOrder;
    public IReadOnlyList<int> RowOrder => _rowOrder;
    public bool ColumnTitlesAreNumeric => _columnsAreNumeric;
    public bool RowTitlesAreNumeric => _rowsAreNumeric;
    public SortMode ColumnSortMode => _columnSortMode;
    public SortMode RowSortMode => _rowSortMode;

    public float GlobalMin => _globalMin;
    public float GlobalMax => _globalMax;
    public bool IsLoaded => _isLoaded;

    public event Action OnDataLoaded;
    public event Action OnFilterChanged;
    public event Action OnOrderChanged;

    protected List<string> _columnTitles = new List<string>();
    protected List<string> _rowTitles = new List<string>();
    protected string _columnAxisTitle;
    protected string _rowAxisTitle;
    protected float[,] _values = new float[0, 0];
    protected float _globalMin;
    protected float _globalMax = 1f;

    protected HashSet<int> _visibleColumns = new HashSet<int>();
    protected HashSet<int> _visibleRows = new HashSet<int>();
    protected List<int> _visibleColumnIndices = new List<int>();
    protected List<int> _visibleRowIndices = new List<int>();
    protected List<int> _columnOrder = new List<int>();
    protected List<int> _rowOrder = new List<int>();
    protected bool _columnsAreNumeric;
    protected bool _rowsAreNumeric;
    protected SortMode _columnSortMode = SortMode.Original;
    protected SortMode _rowSortMode = SortMode.Original;

    protected bool _isLoaded;
    protected bool _suppressFilterEvents;
    protected bool _batchDirty;

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

    public bool IsColumnVisible(int colIndex) => _visibleColumns.Contains(colIndex);

    public void SetColumnVisible(int colIndex, bool visible)
    {
        if (colIndex < 0 || colIndex >= ColumnCount) return;
        if (_visibleColumns.Contains(colIndex) == visible) return;
        if (visible) _visibleColumns.Add(colIndex);
        else _visibleColumns.Remove(colIndex);
        NotifyChanged();
    }

    public void SetAllColumnsVisible(bool visible) => SetAllVisible(_visibleColumns, ColumnCount, visible);

    public bool IsRowVisible(int rowIndex) => _visibleRows.Contains(rowIndex);

    public void SetRowVisible(int rowIndex, bool visible)
    {
        if (rowIndex < 0 || rowIndex >= RowCount) return;
        if (_visibleRows.Contains(rowIndex) == visible) return;
        if (visible) _visibleRows.Add(rowIndex);
        else _visibleRows.Remove(rowIndex);
        NotifyChanged();
    }

    public void SetAllRowsVisible(bool visible) => SetAllVisible(_visibleRows, RowCount, visible);

    private void SetAllVisible(HashSet<int> set, int total, bool visible)
    {
        bool changed;
        if (visible)
        {
            changed = set.Count != total;
            if (changed)
            {
                set.Clear();
                for (int i = 0; i < total; i++) set.Add(i);
            }
        }
        else
        {
            changed = set.Count > 0;
            if (changed) set.Clear();
        }

        if (!changed) return;
        NotifyChanged();
    }

    protected virtual void RebuildFilter()
    {
        EnsureOrder(_columnOrder, ColumnCount);
        EnsureOrder(_rowOrder, RowCount);

        _visibleColumnIndices.Clear();
        for (int i = 0; i < _columnOrder.Count; i++)
        {
            int c = _columnOrder[i];
            if (_visibleColumns.Contains(c)) _visibleColumnIndices.Add(c);
        }

        _visibleRowIndices.Clear();
        for (int i = 0; i < _rowOrder.Count; i++)
        {
            int r = _rowOrder[i];
            if (_visibleRows.Contains(r)) _visibleRowIndices.Add(r);
        }
    }

    public void SortColumns(SortMode mode)
    {
        _columnSortMode = mode;
        BuildOrder(_columnOrder, _columnTitles, _columnsAreNumeric, mode);
        NotifyChanged();
        OnOrderChanged?.Invoke();
    }

    public void SortRows(SortMode mode)
    {
        _rowSortMode = mode;
        BuildOrder(_rowOrder, _rowTitles, _rowsAreNumeric, mode);
        NotifyChanged();
        OnOrderChanged?.Invoke();
    }

    public void ResetOrder()
    {
        _columnSortMode = SortMode.Original;
        _rowSortMode = SortMode.Original;
        InitIdentity(_columnOrder, ColumnCount);
        InitIdentity(_rowOrder, RowCount);
        NotifyChanged();
        OnOrderChanged?.Invoke();
    }

    private static void BuildOrder(List<int> order, List<string> titles, bool numeric, SortMode mode)
    {
        InitIdentity(order, titles.Count);
        if (mode == SortMode.Original) return;

        bool ascending = mode == SortMode.Ascending;
        order.Sort((a, b) =>
        {
            int cmp = numeric
                ? ParseNumber(titles[a]).CompareTo(ParseNumber(titles[b]))
                : string.Compare(titles[a], titles[b], StringComparison.OrdinalIgnoreCase);
            if (!ascending) cmp = -cmp;
            return cmp != 0 ? cmp : a.CompareTo(b);
        });
    }

    private static void InitIdentity(List<int> order, int count)
    {
        order.Clear();
        for (int i = 0; i < count; i++) order.Add(i);
    }

    private static void EnsureOrder(List<int> order, int count)
    {
        if (order.Count == count) return;
        InitIdentity(order, count);
    }

    private static bool DetectNumeric(List<string> titles)
    {
        bool anyValue = false;
        for (int i = 0; i < titles.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(titles[i])) continue;
            anyValue = true;
            if (!double.TryParse(titles[i], NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                return false;
        }
        return anyValue;
    }

    private static double ParseNumber(string value)
    {
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
            return result;
        return 0d;
    }

    public float GetValue(int rowIndex, int colIndex)
    {
        if (rowIndex < 0 || rowIndex >= RowCount || colIndex < 0 || colIndex >= ColumnCount)
            return 0f;
        return _values[rowIndex, colIndex];
    }

    public float GetNormalizedValue(int rowIndex, int colIndex)
    {
        float range = _globalMax - _globalMin;
        if (range <= 0f) return 0f;
        return (GetValue(rowIndex, colIndex) - _globalMin) / range;
    }

    protected void RaiseDataLoaded()
    {
        _isLoaded = true;
        _columnsAreNumeric = DetectNumeric(_columnTitles);
        _rowsAreNumeric = DetectNumeric(_rowTitles);
        _columnSortMode = SortMode.Original;
        _rowSortMode = SortMode.Original;
        InitIdentity(_columnOrder, ColumnCount);
        InitIdentity(_rowOrder, RowCount);
        OnDataLoaded?.Invoke();
    }
}
