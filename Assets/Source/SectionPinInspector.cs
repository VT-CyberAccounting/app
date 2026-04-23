using UnityEngine;
using Oculus.Interaction;

[RequireComponent(typeof(SurfacePinRef))]
public class SectionPinInspector : MonoBehaviour
{
    private DataTooltipUI _tooltip;
    private SurfaceDataSource _dataSource;
    private SurfacePinRef _pinRef;
    private RayInteractable _rayInteractable;

    private int _uiPanelLayer;
    private bool _uiPanelLayerInitialized;
    private bool _isHovering;
    private bool _subscribed;

    public void Configure(DataTooltipUI tooltip, SurfaceDataSource dataSource)
    {
        _tooltip = tooltip;
        _dataSource = dataSource;
    }

    private void Awake()
    {
        _pinRef = GetComponent<SurfacePinRef>();
        _rayInteractable = GetComponentInChildren<RayInteractable>(true);
    }

    private void OnEnable()
    {
        if (_rayInteractable == null) return;
        _rayInteractable.WhenPointerEventRaised += OnPointerEvent;
        _subscribed = true;
    }

    private void OnDisable()
    {
        if (_subscribed && _rayInteractable != null)
            _rayInteractable.WhenPointerEventRaised -= OnPointerEvent;
        _subscribed = false;

        if (_isHovering) HideTooltip();
        _isHovering = false;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Hover:
                _isHovering = true;
                ShowTooltip(evt.Pose.position);
                break;

            case PointerEventType.Move:
                if (_isHovering)
                    UpdateTooltipPosition(evt.Pose.position);
                break;

            case PointerEventType.Unhover:
                _isHovering = false;
                HideTooltip();
                break;
        }
    }

    private void ShowTooltip(Vector3 worldPoint)
    {
        if (_tooltip == null || _dataSource == null || _pinRef == null) return;
        if (IsOccludedByUIPanel(worldPoint))
        {
            HideTooltip();
            return;
        }

        if (_pinRef.kind == SurfacePinManager.PinKind.Column)
            ShowColumnTooltip(worldPoint);
        else
            ShowRowSortTooltip(worldPoint);
    }

    private void ShowColumnTooltip(Vector3 worldPoint)
    {
        int dataCol = _pinRef.dataColIndex;
        if (dataCol < 0 || dataCol >= _dataSource.NumericColumnNames.Count) return;

        string csvHeader = _pinRef.sectionKey;
        string displayName = string.IsNullOrEmpty(_pinRef.sectionDisplayValue)
            ? ColumnDisplayNames.GetDisplayName(csvHeader)
            : _pinRef.sectionDisplayValue;

        (float min, float max) = _dataSource.GetColumnRange(dataCol);
        float avg = _dataSource.GetColumnAverage(dataCol);
        bool isCurrency = ColumnDisplayNames.IsCurrencyColumn(csvHeader);

        _tooltip.ShowColumnSection(worldPoint, displayName, min, max, avg, isCurrency, _pinRef.color);
    }

    private void ShowRowSortTooltip(Vector3 worldPoint)
    {
        string fieldLabel = _pinRef.sortField;
        string sectionValue = string.IsNullOrEmpty(_pinRef.sectionDisplayValue)
            ? _pinRef.sectionKey
            : _pinRef.sectionDisplayValue;

        string breadcrumb = BuildBreadcrumb();
        int rowCount = _dataSource.GetSectionRowCount(_pinRef.rowIndex, _pinRef.tier);

        _tooltip.ShowRowSortSection(worldPoint, fieldLabel, sectionValue, breadcrumb, rowCount, _pinRef.color);
    }

    private string BuildBreadcrumb()
    {
        if (_pinRef.tier <= 0) return null;

        var sb = new System.Text.StringBuilder();
        for (int t = 0; t < _pinRef.tier; t++)
        {
            string parentField = _dataSource.GetSortFieldAt(t);
            if (string.IsNullOrEmpty(parentField)) continue;

            string parentKey = _dataSource.GetRowSortKey(_pinRef.rowIndex, parentField);
            string parentDisplay = _dataSource.GetSortSectionDisplayValue(parentField, parentKey);

            if (sb.Length > 0) sb.Append(" › ");
            sb.Append(parentField);
            sb.Append(": ");
            sb.Append(parentDisplay);
        }
        return sb.ToString();
    }

    private void UpdateTooltipPosition(Vector3 worldPoint)
    {
        if (_tooltip == null) return;
        if (IsOccludedByUIPanel(worldPoint))
        {
            HideTooltip();
            return;
        }
        _tooltip.UpdatePosition(worldPoint);
    }

    private void HideTooltip()
    {
        if (_tooltip != null) _tooltip.Hide();
    }

    private bool IsOccludedByUIPanel(Vector3 worldPoint)
    {
        if (!_uiPanelLayerInitialized)
        {
            _uiPanelLayer = LayerMask.GetMask("UIPanel");
            _uiPanelLayerInitialized = true;
        }

        Transform camTransform = CameraRig.MainTransform;
        if (camTransform == null) return false;

        Vector3 camPos = camTransform.position;
        Vector3 delta = worldPoint - camPos;
        float distance = delta.magnitude;
        if (distance <= 0f) return false;

        Vector3 direction = delta / distance;
        return Physics.Raycast(camPos, direction, distance, _uiPanelLayer);
    }
}
