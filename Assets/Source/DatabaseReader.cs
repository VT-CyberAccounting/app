using System.Collections.Generic;
using UnityEngine;

public class DatabaseReader : DataSource
{
    public string Variable { get; private set; }

    private readonly Dictionary<long, float> _points = new Dictionary<long, float>();
    private readonly List<string> _companyOrder = new List<string>();
    private readonly Dictionary<string, int> _companyIndex = new Dictionary<string, int>();
    private readonly SortedSet<int> _years = new SortedSet<int>();

    public void Configure(string variable)
    {
        Variable = variable;
    }

    public void AddPoint(string company, int year, float value)
    {
        if (string.IsNullOrEmpty(company)) return;
        if (!_companyIndex.ContainsKey(company))
        {
            _companyIndex[company] = _companyOrder.Count;
            _companyOrder.Add(company);
        }
        _years.Add(year);
        _points[Key(_companyIndex[company], year)] = value;
    }

    public void Rebuild()
    {
        int rowCount = _companyOrder.Count;
        int colCount = _years.Count;

        _columnTitles.Clear();
        _rowTitles.Clear();
        _visibleColumns.Clear();
        _visibleRows.Clear();

        List<int> yearList = new List<int>(_years);
        for (int c = 0; c < colCount; c++) _columnTitles.Add(yearList[c].ToString());
        for (int r = 0; r < rowCount; r++) _rowTitles.Add(_companyOrder[r]);

        _values = new float[rowCount, colCount];

        _globalMin = float.MaxValue;
        _globalMax = float.MinValue;
        bool anyValue = false;

        for (int r = 0; r < rowCount; r++)
        {
            for (int c = 0; c < colCount; c++)
            {
                if (_points.TryGetValue(Key(r, yearList[c]), out float v))
                {
                    _values[r, c] = v;
                    if (v < _globalMin) _globalMin = v;
                    if (v > _globalMax) _globalMax = v;
                    anyValue = true;
                }
            }
        }

        if (!anyValue)
        {
            _globalMin = 0f;
            _globalMax = 1f;
        }

        for (int r = 0; r < rowCount; r++)
        {
            for (int c = 0; c < colCount; c++)
            {
                if (!_points.ContainsKey(Key(r, yearList[c])))
                    _values[r, c] = _globalMin;
            }
        }

        for (int c = 0; c < colCount; c++) _visibleColumns.Add(c);
        for (int r = 0; r < rowCount; r++) _visibleRows.Add(r);

        RebuildFilter();

        Debug.Log($"[DatabaseReader:{Variable}] {rowCount} companies x {colCount} years " +
                  $"(range [{_globalMin}, {_globalMax}])");
        RaiseDataLoaded();
    }

    private static long Key(int companyRow, int year)
    {
        return ((long)companyRow << 32) ^ (uint)year;
    }
}
