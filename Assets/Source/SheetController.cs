using System;
using UnityEngine;

public class SheetController : MonoBehaviour
{
    public DataSource dataSource;
    [UnityEngine.Serialization.FormerlySerializedAs("surfaceGenerator")]
    [UnityEngine.Serialization.FormerlySerializedAs("regionGenerator")]
    public SheetGenerator sheetGenerator;

    public event Action<int, bool> OnColumnToggled;
    public event Action<int, bool> OnRowToggled;

    public DataSource DataSource => dataSource;

    public void SetDataSource(DataSource source)
    {
        dataSource = source;
    }

    public void SetSheetPresented(bool presented)
    {
        if (sheetGenerator != null) sheetGenerator.SetPresented(presented);
    }

    private void Awake()
    {
        if (dataSource == null) dataSource = FileReader.Instance;
    }

    private void Start()
    {
        if (dataSource == null) dataSource = FileReader.Instance;
        if (dataSource == null)
            Debug.LogError($"[SheetController:{name}] No data source set and no singleton available.");
    }

    public void BeginBatch()
    {
        if (dataSource != null) dataSource.BeginBatchUpdate();
    }

    public void EndBatch()
    {
        if (dataSource != null) dataSource.EndBatchUpdate();
    }

    public void SetColumnVisible(int colIndex, bool visible)
    {
        if (dataSource != null) dataSource.SetColumnVisible(colIndex, visible);
        OnColumnToggled?.Invoke(colIndex, visible);
    }

    public bool IsColumnVisible(int colIndex)
    {
        if (dataSource == null) return true;
        return dataSource.IsColumnVisible(colIndex);
    }

    public void SetAllColumnsVisible(bool visible)
    {
        if (dataSource == null) return;

        dataSource.BeginBatchUpdate();
        dataSource.SetAllColumnsVisible(visible);
        dataSource.EndBatchUpdate();

        for (int i = 0; i < dataSource.ColumnCount; i++)
            OnColumnToggled?.Invoke(i, visible);
    }

    public void SetRowVisible(int rowIndex, bool visible)
    {
        if (dataSource != null) dataSource.SetRowVisible(rowIndex, visible);
        OnRowToggled?.Invoke(rowIndex, visible);
    }

    public bool IsRowVisible(int rowIndex)
    {
        if (dataSource == null) return true;
        return dataSource.IsRowVisible(rowIndex);
    }

    public void SetAllRowsVisible(bool visible)
    {
        if (dataSource == null) return;

        dataSource.BeginBatchUpdate();
        dataSource.SetAllRowsVisible(visible);
        dataSource.EndBatchUpdate();

        for (int i = 0; i < dataSource.RowCount; i++)
            OnRowToggled?.Invoke(i, visible);
    }

    public void SortColumns(DataSource.SortMode mode)
    {
        if (dataSource != null) dataSource.SortColumns(mode);
    }

    public void SortRows(DataSource.SortMode mode)
    {
        if (dataSource != null) dataSource.SortRows(mode);
    }

    public void ResetAllFilters()
    {
        if (dataSource == null) return;

        dataSource.BeginBatchUpdate();
        dataSource.SetAllColumnsVisible(true);
        dataSource.SetAllRowsVisible(true);
        dataSource.ResetOrder();
        dataSource.EndBatchUpdate();

        for (int i = 0; i < dataSource.ColumnCount; i++)
            OnColumnToggled?.Invoke(i, true);
        for (int i = 0; i < dataSource.RowCount; i++)
            OnRowToggled?.Invoke(i, true);
    }
}
