using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DataTooltipUI : MonoBehaviour
{
    public float fixedDistance = 0.7f;
    public float horizontalOffset = 0.1f;
    public float verticalOffset = 0.1f;

    private const float TopPad = 12f;
    private const float BottomPad = 12f;
    private const float LabeledRowHeight = 22f;
    private const float LabeledRowGap = 4f;
    private const float DividerGap = 10f;
    private const float WrapGap = 4f;
    private const float CellModeHeight = 224f;

    private Canvas _canvas;
    private RectTransform _canvasRect;
    private Image _colorSwatch;
    private GameObject _cellGroup;
    private GameObject _sectionGroup;

    private TextMeshProUGUI _tickerLabel;
    private TextMeshProUGUI _companyLabel;
    private TextMeshProUGUI _industryLabel;
    private TextMeshProUGUI _countryLabel;
    private TextMeshProUGUI _yearLabel;
    private TextMeshProUGUI _columnLabel;
    private TextMeshProUGUI _valueLabel;

    private TextMeshProUGUI _sectionTitleLabel;
    private TextMeshProUGUI _sectionValueLabel;
    private TextMeshProUGUI _sectionBreadcrumbLabel;
    private TextMeshProUGUI _sectionRowCountLabel;
    private TextMeshProUGUI _sectionMinLabel;
    private TextMeshProUGUI _sectionMaxLabel;
    private TextMeshProUGUI _sectionAverageLabel;

    private RectTransform _sectionTitleRT;
    private RectTransform _sectionValueRT;
    private RectTransform _sectionBreadcrumbRT;
    private RectTransform _sectionDividerRT;
    private RectTransform _rowsLabelRT;
    private RectTransform _sectionRowCountRT;
    private RectTransform _minLabelRT;
    private RectTransform _sectionMinRT;
    private RectTransform _maxLabelRT;
    private RectTransform _sectionMaxRT;
    private RectTransform _avgLabelRT;
    private RectTransform _sectionAverageRT;

    private Color _defaultSwatchColor;
    private bool _hasDefaultSwatchColor;

    private RectTransform _columnLabelRT;
    private RectTransform _valueLabelRT;
    private float _columnLabelBaseHeight;
    private float _valueLabelBaseHeight;
    private float _valueLabelBaseY;
    private bool _cellLayoutCached;

    private void Awake()
    {
        _canvas = GetComponentInChildren<Canvas>(true);
        if (_canvas != null) _canvasRect = _canvas.GetComponent<RectTransform>();

        _colorSwatch = FindComponent<Image>("ColorSwatch");
        if (_colorSwatch != null)
        {
            _defaultSwatchColor = _colorSwatch.color;
            _hasDefaultSwatchColor = true;
        }

        _cellGroup = FindChild("CellGroup");
        _sectionGroup = FindChild("SectionGroup");

        _tickerLabel = FindComponent<TextMeshProUGUI>("Ticker");
        _companyLabel = FindComponent<TextMeshProUGUI>("Company");
        _industryLabel = FindComponent<TextMeshProUGUI>("IndustryValue");
        _countryLabel = FindComponent<TextMeshProUGUI>("CountryValue");
        _yearLabel = FindComponent<TextMeshProUGUI>("YearValue");
        _columnLabel = FindComponent<TextMeshProUGUI>("ColumnValue");
        _valueLabel = FindComponent<TextMeshProUGUI>("ValueValue");

        _sectionTitleLabel = FindComponent<TextMeshProUGUI>("SectionTitle");
        _sectionValueLabel = FindComponent<TextMeshProUGUI>("SectionValue");
        _sectionBreadcrumbLabel = FindComponent<TextMeshProUGUI>("SectionBreadcrumb");
        _sectionRowCountLabel = FindComponent<TextMeshProUGUI>("SectionRowCount");
        _sectionMinLabel = FindComponent<TextMeshProUGUI>("SectionMin");
        _sectionMaxLabel = FindComponent<TextMeshProUGUI>("SectionMax");
        _sectionAverageLabel = FindComponent<TextMeshProUGUI>("SectionAverage");

        _sectionTitleRT = RectOf(_sectionTitleLabel);
        _sectionValueRT = RectOf(_sectionValueLabel);
        _sectionBreadcrumbRT = RectOf(_sectionBreadcrumbLabel);
        _sectionRowCountRT = RectOf(_sectionRowCountLabel);
        _sectionMinRT = RectOf(_sectionMinLabel);
        _sectionMaxRT = RectOf(_sectionMaxLabel);
        _sectionAverageRT = RectOf(_sectionAverageLabel);

        _rowsLabelRT = GetPreviousSiblingRect(_sectionRowCountRT);
        _minLabelRT = GetPreviousSiblingRect(_sectionMinRT);
        _maxLabelRT = GetPreviousSiblingRect(_sectionMaxRT);
        _avgLabelRT = GetPreviousSiblingRect(_sectionAverageRT);
        _sectionDividerRT = FindRectInSection("Divider");

        _columnLabelRT = RectOf(_columnLabel);
        _valueLabelRT = RectOf(_valueLabel);

        EnableWrap(_columnLabel);
        EnableWrap(_valueLabel);
        EnableWrap(_companyLabel);
        EnableWrap(_sectionTitleLabel);
        EnableWrap(_sectionValueLabel);
        EnableWrap(_sectionBreadcrumbLabel);

        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    private static void EnableWrap(TextMeshProUGUI label)
    {
        if (label == null) return;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    public void ShowCell(Vector3 worldHitPoint, string ticker, string companyName,
        string industry, string country, string year,
        string columnName, float rawValue, float normalizedValue,
        bool hasPlaceholder = false, float placeholderValue = 0f, string placeholderName = null)
    {
        SetMode(cellMode: true);

        bool isCurrency = ColumnDisplayNames.IsCurrencyColumn(columnName);

        if (_colorSwatch != null) _colorSwatch.color = Heatmap.Sample(normalizedValue);
        if (_tickerLabel != null) _tickerLabel.text = ticker;
        if (_companyLabel != null) _companyLabel.text = companyName;
        if (_industryLabel != null) _industryLabel.text = industry;
        if (_countryLabel != null) _countryLabel.text = CountryNames.GetFullName(country);
        if (_yearLabel != null) _yearLabel.text = year;
        if (_columnLabel != null) _columnLabel.text = ColumnDisplayNames.GetDisplayName(columnName);
        if (_valueLabel != null)
        {
            string cellValue = FormatCompactValue(rawValue, isCurrency);
            string tickerTag = string.IsNullOrEmpty(ticker) ? "" : ticker.ToUpperInvariant();
            const string numberColumn = "<pos=5em>";
            string firstRow = string.IsNullOrEmpty(tickerTag)
                ? cellValue
                : $"{tickerTag}{numberColumn}{cellValue}";
            if (hasPlaceholder)
            {
                string peer = FormatCompactValue(placeholderValue, isCurrency);
                _valueLabel.text = $"{firstRow}\nBPI{numberColumn}{peer}";
            }
            else
            {
                _valueLabel.text = firstRow;
            }
        }

        SetCanvasHeight(CellModeHeight + ReflowCellLabels());
        Present(worldHitPoint);
    }

    private float ReflowCellLabels()
    {
        CacheCellLayout();

        float columnExtra = FitLabel(_columnLabel, _columnLabelRT, _columnLabelBaseHeight);

        if (_valueLabelRT != null)
        {
            Vector2 pos = _valueLabelRT.anchoredPosition;
            _valueLabelRT.anchoredPosition = new Vector2(pos.x, _valueLabelBaseY - columnExtra);
        }

        float valueExtra = FitLabel(_valueLabel, _valueLabelRT, _valueLabelBaseHeight);

        return columnExtra + valueExtra;
    }

    private void CacheCellLayout()
    {
        if (_cellLayoutCached) return;
        if (_columnLabelRT != null) _columnLabelBaseHeight = _columnLabelRT.rect.height;
        if (_valueLabelRT != null)
        {
            _valueLabelBaseHeight = _valueLabelRT.rect.height;
            _valueLabelBaseY = _valueLabelRT.anchoredPosition.y;
        }
        _cellLayoutCached = true;
    }

    private static float FitLabel(TextMeshProUGUI label, RectTransform rt, float baseHeight)
    {
        if (label == null || rt == null) return 0f;
        label.ForceMeshUpdate();
        float preferred = label.preferredHeight;
        float height = Mathf.Max(preferred, baseHeight);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        float extra = height - baseHeight;
        return extra > 0f ? extra : 0f;
    }

    public void ShowColumnSection(Vector3 worldHitPoint, string columnDisplayName,
        float columnMin, float columnMax, float columnAverage, bool isCurrency, Color swatchColor)
    {
        SetMode(cellMode: false);

        if (_colorSwatch != null) _colorSwatch.color = swatchColor;
        if (_sectionTitleLabel != null) _sectionTitleLabel.text = columnDisplayName;

        SetRow(_sectionValueLabel, _sectionValueRT, null, show: false);
        SetRow(_sectionBreadcrumbLabel, _sectionBreadcrumbRT, null, show: false);
        SetLabeledRow(_rowsLabelRT, _sectionRowCountLabel, _sectionRowCountRT, null, show: false);

        SetLabeledRow(_minLabelRT, _sectionMinLabel, _sectionMinRT, FormatCompactValue(columnMin, isCurrency), show: true);
        SetLabeledRow(_maxLabelRT, _sectionMaxLabel, _sectionMaxRT, FormatCompactValue(columnMax, isCurrency), show: true);
        SetLabeledRow(_avgLabelRT, _sectionAverageLabel, _sectionAverageRT, FormatCompactValue(columnAverage, isCurrency), show: true);

        ReflowSectionGroup();
        Present(worldHitPoint);
    }

    public void ShowRowSortSection(Vector3 worldHitPoint, string sortFieldLabel,
        string sectionValue, string breadcrumb, int rowCount, Color swatchColor)
    {
        SetMode(cellMode: false);

        if (_colorSwatch != null) _colorSwatch.color = swatchColor;
        if (_sectionTitleLabel != null) _sectionTitleLabel.text = sortFieldLabel;

        SetRow(_sectionValueLabel, _sectionValueRT, sectionValue, show: !string.IsNullOrEmpty(sectionValue));
        SetRow(_sectionBreadcrumbLabel, _sectionBreadcrumbRT, breadcrumb, show: !string.IsNullOrEmpty(breadcrumb));
        SetLabeledRow(_rowsLabelRT, _sectionRowCountLabel, _sectionRowCountRT, rowCount.ToString(), show: true);

        SetLabeledRow(_minLabelRT, _sectionMinLabel, _sectionMinRT, null, show: false);
        SetLabeledRow(_maxLabelRT, _sectionMaxLabel, _sectionMaxRT, null, show: false);
        SetLabeledRow(_avgLabelRT, _sectionAverageLabel, _sectionAverageRT, null, show: false);

        ReflowSectionGroup();
        Present(worldHitPoint);
    }

    public void Hide()
    {
        if (_canvas != null && _canvas.gameObject.activeSelf)
            _canvas.gameObject.SetActive(false);

        if (_colorSwatch != null && _hasDefaultSwatchColor)
            _colorSwatch.color = _defaultSwatchColor;
    }

    public void UpdatePosition(Vector3 worldHitPoint)
    {
        Transform cam = CameraRig.MainTransform;
        if (cam == null) return;

        Vector3 headForward = cam.forward;
        Vector3 rayDirection = (worldHitPoint - cam.position).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, headForward).normalized;

        Vector3 blended = new Vector3(rayDirection.x, headForward.y, rayDirection.z).normalized;

        transform.position = cam.position
            + blended * fixedDistance
            + Vector3.up * verticalOffset
            + right * horizontalOffset;

        transform.rotation = Quaternion.LookRotation(blended, Vector3.up);
    }

    private void Present(Vector3 worldHitPoint)
    {
        UpdatePosition(worldHitPoint);
        if (_canvas != null && !_canvas.gameObject.activeSelf)
            _canvas.gameObject.SetActive(true);
    }

    private void SetMode(bool cellMode)
    {
        if (_cellGroup != null) _cellGroup.SetActive(cellMode);
        if (_sectionGroup != null) _sectionGroup.SetActive(!cellMode);
    }

    private void SetRow(TextMeshProUGUI label, RectTransform rt, string text, bool show)
    {
        if (label == null) return;
        label.gameObject.SetActive(show);
        if (show) label.text = text;
    }

    private void SetLabeledRow(RectTransform labelRT, TextMeshProUGUI valueLabel,
        RectTransform valueRT, string valueText, bool show)
    {
        if (labelRT != null) labelRT.gameObject.SetActive(show);
        if (valueLabel != null)
        {
            valueLabel.gameObject.SetActive(show);
            if (show) valueLabel.text = valueText;
        }
    }

    private void ReflowSectionGroup()
    {
        float y = -TopPad;

        y = PlaceWrappingText(_sectionTitleLabel, _sectionTitleRT, y, WrapGap);
        y = PlaceWrappingText(_sectionValueLabel, _sectionValueRT, y, WrapGap);
        y = PlaceWrappingText(_sectionBreadcrumbLabel, _sectionBreadcrumbRT, y, WrapGap);

        bool hasRows = (_sectionRowCountRT != null && _sectionRowCountRT.gameObject.activeSelf)
            || (_sectionMinRT != null && _sectionMinRT.gameObject.activeSelf);

        if (hasRows && _sectionDividerRT != null)
        {
            _sectionDividerRT.gameObject.SetActive(true);
            _sectionDividerRT.anchoredPosition = new Vector2(_sectionDividerRT.anchoredPosition.x, y);
            y -= DividerGap;
        }
        else if (_sectionDividerRT != null)
        {
            _sectionDividerRT.gameObject.SetActive(false);
        }

        y = PlaceLabeledRow(_rowsLabelRT, _sectionRowCountRT, y);
        y = PlaceLabeledRow(_minLabelRT, _sectionMinRT, y);
        y = PlaceLabeledRow(_maxLabelRT, _sectionMaxRT, y);
        y = PlaceLabeledRow(_avgLabelRT, _sectionAverageRT, y);

        SetCanvasHeight(-y + BottomPad);
    }

    private float PlaceWrappingText(TextMeshProUGUI text, RectTransform rt, float y, float gapAfter)
    {
        if (rt == null || !rt.gameObject.activeSelf) return y;

        float height = 0f;
        if (text != null)
        {
            text.ForceMeshUpdate();
            height = text.preferredHeight;
        }
        float minHeight = text != null ? text.fontSize + 4f : 18f;
        if (height < minHeight) height = minHeight;

        rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
        return y - height - gapAfter;
    }

    private float PlaceLabeledRow(RectTransform labelRT, RectTransform valueRT, float y)
    {
        bool labelOn = labelRT != null && labelRT.gameObject.activeSelf;
        bool valueOn = valueRT != null && valueRT.gameObject.activeSelf;
        if (!labelOn && !valueOn) return y;

        if (labelRT != null)
            labelRT.anchoredPosition = new Vector2(labelRT.anchoredPosition.x, y);
        if (valueRT != null)
            valueRT.anchoredPosition = new Vector2(valueRT.anchoredPosition.x, y);

        return y - LabeledRowHeight - LabeledRowGap;
    }

    private void SetCanvasHeight(float height)
    {
        if (_canvasRect == null) return;
        _canvasRect.sizeDelta = new Vector2(_canvasRect.sizeDelta.x, Mathf.Max(60f, height));
    }

    private T FindComponent<T>(string name) where T : Component
    {
        Transform t = UITransformSearch.FindDeep(transform, name);
        return t != null ? t.GetComponent<T>() : null;
    }

    private GameObject FindChild(string name)
    {
        Transform t = UITransformSearch.FindDeep(transform, name);
        return t != null ? t.gameObject : null;
    }

    private RectTransform FindRectInSection(string name)
    {
        if (_sectionGroup == null) return null;
        Transform t = UITransformSearch.FindDeep(_sectionGroup.transform, name);
        return t != null ? t as RectTransform : null;
    }

    private static RectTransform GetPreviousSiblingRect(RectTransform rt)
    {
        if (rt == null) return null;
        Transform parent = rt.parent;
        if (parent == null) return null;
        int idx = rt.GetSiblingIndex();
        if (idx <= 0) return null;
        Transform sibling = parent.GetChild(idx - 1);
        if (sibling == null || sibling.GetComponent<TMPro.TextMeshProUGUI>() == null) return null;
        return sibling as RectTransform;
    }

    private static RectTransform RectOf(Component c)
    {
        return c != null ? c.transform as RectTransform : null;
    }

    public static string FormatCompactValue(float value, bool isCurrency)
    {
        string format = Mathf.Abs(value - Mathf.Round(value)) < 0.0001f ? "N0" : "N2";
        if (!isCurrency)
            return value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

        string prefix = value < 0f ? "-$" : "$";
        return prefix + Mathf.Abs(value).ToString(format, System.Globalization.CultureInfo.InvariantCulture);
    }
}
