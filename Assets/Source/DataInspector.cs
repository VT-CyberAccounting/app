using UnityEngine;
using Oculus.Interaction;

public class DataInspector : MonoBehaviour
{
    public DataSurfaceGenerator surfaceGenerator;
    public DataTooltipUI tooltip;

    private RayInteractable _rayInteractable;
    private bool _isHovering;
    private int _lastRow = -1;
    private int _lastCol = -1;

    private int _uiPanelLayer;

    private void Start()
    {
        _uiPanelLayer = LayerMask.GetMask("UIPanel");
    }

    private void Awake()
    {
        _rayInteractable = GetComponentInChildren<RayInteractable>(true);
    }

    private void OnEnable()
    {
        _rayInteractable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDisable()
    {
        _rayInteractable.WhenPointerEventRaised -= OnPointerEvent;
        HideTooltip();
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Hover:
                _isHovering = true;
                UpdateTooltip(evt.Pose.position);
                break;

            case PointerEventType.Move:
                if (_isHovering)
                    UpdateTooltip(evt.Pose.position);
                break;

            case PointerEventType.Unhover:
                _isHovering = false;
                HideTooltip();
                break;
        }
    }

    private void UpdateTooltip(Vector3 worldPoint)
    {
        if (surfaceGenerator == null || tooltip == null) return;

        Transform cam = Camera.main?.transform;
        if (cam != null)
        {
            Vector3 direction = (worldPoint - cam.position).normalized;
            float distance = Vector3.Distance(cam.position, worldPoint);

            if (Physics.Raycast(cam.position, direction, distance, _uiPanelLayer))
            {
                HideTooltip();
                return;
            }
        }

        CSVDataManager data = CSVDataManager.Instance;
        if (data == null || !data.IsLoaded || data.FilteredRows.Count == 0) return;

        (int row, int col) = surfaceGenerator.GetNearestCell(worldPoint);

        if (row == _lastRow && col == _lastCol)
        {
            tooltip.UpdatePosition(worldPoint);
            return;
        }

        _lastRow = row;
        _lastCol = col;

        if (row >= data.FilteredRows.Count || col >= data.NumericColumnNames.Count)
            return;

        CSVDataManager.DataRow dataRow = data.FilteredRows[row];
        float rawValue = data.GetRawValue(row, col);
        float normalizedValue = data.GetNormalizedValue(row, col);
        string columnName = data.NumericColumnNames[col];

        tooltip.Show(
            worldPoint,
            dataRow.Ticker,
            dataRow.CompanyName,
            dataRow.Industry,
            dataRow.CountryCode,
            dataRow.Year,
            columnName,
            rawValue,
            normalizedValue
        );
    }

    private void HideTooltip()
    {
        _lastRow = -1;
        _lastCol = -1;
        if (tooltip != null)
            tooltip.Hide();
    }
}