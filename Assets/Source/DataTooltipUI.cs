using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DataTooltipUI : MonoBehaviour
{
    public float fixedDistance = 0.7f;
    public float horizontalOffset = 0.1f;
    public float verticalOffset = 0.1f;

    private Canvas _canvas;
    private Image _colorSwatch;
    private TextMeshProUGUI _tickerLabel;
    private TextMeshProUGUI _companyLabel;
    private TextMeshProUGUI _industryLabel;
    private TextMeshProUGUI _countryLabel;
    private TextMeshProUGUI _yearLabel;
    private TextMeshProUGUI _columnLabel;
    private TextMeshProUGUI _valueLabel;

    private static readonly Color[] HeatmapStops = {
        new Color(0.0f, 0.0f, 1.0f),
        new Color(0.0f, 1.0f, 1.0f),
        new Color(0.0f, 1.0f, 0.0f),
        new Color(1.0f, 1.0f, 0.0f),
        new Color(1.0f, 0.0f, 0.0f)
    };

    private void Awake()
    {
        _canvas = GetComponentInChildren<Canvas>(true);

        _colorSwatch = FindComponent<Image>("ColorSwatch");
        _tickerLabel = FindComponent<TextMeshProUGUI>("Ticker");
        _companyLabel = FindComponent<TextMeshProUGUI>("Company");
        _industryLabel = FindComponent<TextMeshProUGUI>("IndustryValue");
        _countryLabel = FindComponent<TextMeshProUGUI>("CountryValue");
        _yearLabel = FindComponent<TextMeshProUGUI>("YearValue");
        _columnLabel = FindComponent<TextMeshProUGUI>("ColumnValue");
        _valueLabel = FindComponent<TextMeshProUGUI>("ValueValue");

        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    public void Show(Vector3 worldHitPoint, string ticker, string companyName,
        string industry, string country, string year,
        string columnName, float rawValue, float normalizedValue)
    {
        if (_colorSwatch != null) _colorSwatch.color = SampleHeatmap(normalizedValue);
        if (_tickerLabel != null) _tickerLabel.text = ticker;
        if (_companyLabel != null) _companyLabel.text = companyName;
        if (_industryLabel != null) _industryLabel.text = industry;
        if (_countryLabel != null) _countryLabel.text = CountryNames.GetFullName(country);
        if (_yearLabel != null) _yearLabel.text = year;
        if (_columnLabel != null) _columnLabel.text = ColumnDisplayNames.GetDisplayName(columnName);
        if (_valueLabel != null) _valueLabel.text = FormatCompactValue(rawValue);

        UpdatePosition(worldHitPoint);

        if (_canvas != null && !_canvas.gameObject.activeSelf)
            _canvas.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (_canvas != null && _canvas.gameObject.activeSelf)
            _canvas.gameObject.SetActive(false);
    }

    public void UpdatePosition(Vector3 worldHitPoint)
    {
        Transform cam = Camera.main?.transform;
        if (cam == null) return;

        Vector3 headForward = cam.forward;
        Vector3 rayDirection = (worldHitPoint - cam.position).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, headForward).normalized;

        // Horizontal placement follows the ray, vertical follows the head
        Vector3 blended = new Vector3(rayDirection.x, headForward.y, rayDirection.z).normalized;

        transform.position = cam.position
            + blended * fixedDistance
            + Vector3.up * verticalOffset
            + right * horizontalOffset;

        transform.rotation = Quaternion.LookRotation(blended, Vector3.up);
    }

    private T FindComponent<T>(string name) where T : Component
    {
        Transform t = FindDeep(transform, name);
        return t != null ? t.GetComponent<T>() : null;
    }

    private Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindDeep(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }

    private static string FormatCompactValue(float value)
    {
        float abs = Mathf.Abs(value);
        string sign = value < 0 ? "-" : "";

        if (abs < 1f)
            return value.ToString("F2");
        if (abs < 1000f)
            return value.ToString("F1");
        if (abs < 1000000f)
            return sign + FormatUnit(abs, 1000f, "thousand");
        if (abs < 1000000000f)
            return sign + FormatUnit(abs, 1000000f, "million");
        if (abs < 1000000000000f)
            return sign + FormatUnit(abs, 1000000000f, "billion");

        return sign + FormatUnit(abs, 1000000000000f, "trillion");
    }

    private static string FormatUnit(float abs, float divisor, string unit)
    {
        float scaled = abs / divisor;
        if (scaled >= 100f)
            return Mathf.RoundToInt(scaled) + " " + unit;
        if (scaled >= 10f)
            return scaled.ToString("F1") + " " + unit;
        return scaled.ToString("F2") + " " + unit;
    }

    private static Color SampleHeatmap(float t)
    {
        t = Mathf.Clamp01(t);
        float scaledT = t * (HeatmapStops.Length - 1);
        int lower = Mathf.FloorToInt(scaledT);
        int upper = Mathf.Min(lower + 1, HeatmapStops.Length - 1);
        float frac = scaledT - lower;
        return Color.Lerp(HeatmapStops[lower], HeatmapStops[upper], frac);
    }
}