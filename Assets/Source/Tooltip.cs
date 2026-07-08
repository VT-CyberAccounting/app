using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Tooltip : MonoBehaviour
{
    public float zOffsetFromCamera = 0.5f;
    public float xOffsetFromCamera = 0.125f;
    public float yOffsetFromCamera = 0.125f;

    public TMP_FontAsset bodyFont;
    public TMP_FontAsset titleFont;

    private const float TopPad = MetaTokens.Spacing;
    private const float TitleHeight = 12f;
    private const float TitleGap = MetaTokens.Spacing;
    private const float RowHeight = 12f;
    private const float RowGap = MetaTokens.Spacing;

    private const float HintBodyTop = 2f + TopPad + TitleHeight + TitleGap;
    private const float HintBottomPad = MetaTokens.Spacing + 2f;

    private enum Mode { Cell, Hint, Header, Sheet }

    private Canvas _canvas;
    private RectTransform _canvasRect;
    private Image _colorSwatch;
    private GameObject _cellGroup;
    private GameObject _hintGroup;
    private GameObject _headerGroup;
    private GameObject _sheetGroup;

    private TextMeshProUGUI _columnLabel;
    private TextMeshProUGUI _rowValueLabel;
    private TextMeshProUGUI _valueLabel;

    private TextMeshProUGUI _hintTitleLabel;
    private TextMeshProUGUI _hintBodyLabel;

    private TextMeshProUGUI _headerTitleLabel;
    private TextMeshProUGUI _headerMaxLabel;
    private TextMeshProUGUI _headerMaxLocLabel;
    private TextMeshProUGUI _headerMinLabel;
    private TextMeshProUGUI _headerMinLocLabel;
    private TextMeshProUGUI _headerMeanLabel;
    private TextMeshProUGUI _headerCellsLabel;
    private TextMeshProUGUI _headerSumLabel;

    private TextMeshProUGUI _sheetTitleLabel;
    private TextMeshProUGUI _sheetMaxLabel;
    private TextMeshProUGUI _sheetMaxLocLabel;
    private TextMeshProUGUI _sheetMinLabel;
    private TextMeshProUGUI _sheetMinLocLabel;
    private TextMeshProUGUI _sheetMeanLabel;
    private TextMeshProUGUI _sheetCellsLabel;
    private TextMeshProUGUI _sheetSumLabel;

    private struct StatRow
    {
        public RectTransform Caption;
        public RectTransform Value;
    }

    private readonly List<StatRow> _sheetRows = new List<StatRow>(5);
    private readonly List<StatRow> _headerRows = new List<StatRow>(3);

    private const float StatFirstRowY = -(TopPad + TitleHeight + TitleGap);
    private const float StatRowPitch = RowHeight + RowGap;
    private const float SheetCaptionX = MetaTokens.PanelGutter;
    private const float SheetCaptionWidth = 92f;
    private const float SheetValueX = 112f;
    private const float SheetValueWidth = 170f;
    private const float HeaderCaptionX = MetaTokens.PanelGutter;
    private const float HeaderCaptionWidth = 64f;
    private const float HeaderValueX = 78f;
    private const float HeaderValueWidth = 208f;

    private Color _defaultSwatchColor;
    private bool _hasDefaultSwatchColor;

    private const float SwatchSize = 12f;
    private const float SwatchGap = 6f;
    private const float SwatchTitleGap = 10f;

    private RectTransform _colorSwatchRT;
    private Vector2 _swatchBasePos;
    private readonly List<Image> _extraSwatches = new List<Image>();
    private readonly List<Color> _swatchBuffer = new List<Color>(1);

    private RectTransform _sheetTitleRT;
    private Vector2 _sheetTitleBasePos;
    private float _sheetTitleBaseWidth;
    private bool _sheetTitleCaptured;

    private RectTransform _columnLabelRT;
    private RectTransform _rowValueRT;
    private RectTransform _valueLabelRT;
    private RectTransform _columnCaptionRT;
    private RectTransform _rowCaptionRT;
    private RectTransform _valueCaptionRT;

    private void Awake()
    {
        _canvas = GetComponentInChildren<Canvas>(true);
        _canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;

        _colorSwatch = FindComponent<Image>("ColorSwatch");
        if (_colorSwatch != null)
        {
            _defaultSwatchColor = _colorSwatch.color;
            _hasDefaultSwatchColor = true;
            _colorSwatchRT = RectOf(_colorSwatch);
            if (_colorSwatchRT != null)
                _swatchBasePos = new Vector2(_colorSwatchRT.anchoredPosition.x, -TopPad);
        }

        _cellGroup = FindChild("CellGroup");
        _hintGroup = FindChild("HintGroup");
        _headerGroup = FindChild("HeaderGroup");
        _sheetGroup = FindChild("SheetGroup");

        _columnLabel = FindComponent<TextMeshProUGUI>("ColumnValue");
        _rowValueLabel = FindComponent<TextMeshProUGUI>("RowValue");
        _valueLabel = FindComponent<TextMeshProUGUI>("ValueValue");

        _hintTitleLabel = FindComponent<TextMeshProUGUI>("HintTitle");
        _hintBodyLabel = FindComponent<TextMeshProUGUI>("HintBody");

        _headerTitleLabel = FindComponent<TextMeshProUGUI>("HeaderTitle");
        _headerMaxLabel = FindComponent<TextMeshProUGUI>("HeaderMax");
        _headerMaxLocLabel = FindComponent<TextMeshProUGUI>("HeaderMaxLoc");
        _headerMinLabel = FindComponent<TextMeshProUGUI>("HeaderMin");
        _headerMinLocLabel = FindComponent<TextMeshProUGUI>("HeaderMinLoc");
        _headerMeanLabel = FindComponent<TextMeshProUGUI>("HeaderMean");
        _headerCellsLabel = FindComponent<TextMeshProUGUI>("HeaderCells");

        _sheetTitleLabel = FindComponent<TextMeshProUGUI>("SheetTitle");
        _sheetTitleRT = RectOf(_sheetTitleLabel);
        if (_sheetTitleRT != null)
        {
            _sheetTitleBasePos = _sheetTitleRT.anchoredPosition;
            _sheetTitleBaseWidth = _sheetTitleRT.sizeDelta.x;
            _sheetTitleCaptured = true;
        }
        _sheetMaxLabel = FindComponent<TextMeshProUGUI>("SheetMax");
        _sheetMaxLocLabel = FindComponent<TextMeshProUGUI>("SheetMaxLoc");
        _sheetMinLabel = FindComponent<TextMeshProUGUI>("SheetMin");
        _sheetMinLocLabel = FindComponent<TextMeshProUGUI>("SheetMinLoc");
        _sheetMeanLabel = FindComponent<TextMeshProUGUI>("SheetMean");
        _sheetCellsLabel = FindComponent<TextMeshProUGUI>("SheetCells");
        BuildSheetRows();
        BuildHeaderRows();

        _columnLabelRT = RectOf(_columnLabel);
        _rowValueRT = RectOf(_rowValueLabel);
        _valueLabelRT = RectOf(_valueLabel);
        _columnCaptionRT = GetPreviousSiblingRect(_columnLabelRT);
        _rowCaptionRT = GetPreviousSiblingRect(_rowValueRT);
        _valueCaptionRT = GetPreviousSiblingRect(_valueLabelRT);

        EnableWrap(_hintTitleLabel);
        EnableWrap(_hintBodyLabel);
        EnableWrap(_headerTitleLabel);
        EnableWrap(_sheetTitleLabel);

        ApplyFonts();

        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    private void ApplyFonts()
    {
        if (bodyFont != null)
        {
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++) texts[i].font = bodyFont;
        }

        if (titleFont != null)
        {
            if (_hintTitleLabel != null) _hintTitleLabel.font = titleFont;
            if (_headerTitleLabel != null) _headerTitleLabel.font = titleFont;
            if (_sheetTitleLabel != null) _sheetTitleLabel.font = titleFont;
        }
    }

    private static void EnableWrap(TextMeshProUGUI label)
    {
        if (label == null) return;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    public void ShowCell(Vector3 worldHitPoint, string xTitle, string zTitle,
        float rawValue, Color swatchColor)
    {
        SetMode(Mode.Cell);

        ShowSwatch(swatchColor);

        if (_columnLabel != null) _columnLabel.text = xTitle;

        bool hasRow = !string.IsNullOrEmpty(zTitle);
        if (_rowCaptionRT != null) _rowCaptionRT.gameObject.SetActive(hasRow);
        if (_rowValueLabel != null)
        {
            _rowValueLabel.gameObject.SetActive(hasRow);
            if (hasRow) _rowValueLabel.text = zTitle;
        }

        if (_valueLabel != null)
            _valueLabel.text = FormatCompactValue(rawValue, false);

        ReflowCellGroup();
        SetPanelHeight(PanelHeightForRows(hasRow ? 3 : 2));
        Present(worldHitPoint);
    }

    public void ShowHint(Vector3 worldHitPoint, string title, string body)
    {
        SetMode(Mode.Hint);

        HideSwatches();

        if (_hintTitleLabel != null) _hintTitleLabel.text = title;
        if (_hintBodyLabel != null)
        {
            _hintBodyLabel.text = body;
            RectTransform bodyRT = _hintBodyLabel.rectTransform;
            bodyRT.anchoredPosition = new Vector2(bodyRT.anchoredPosition.x, -(TopPad + TitleHeight + TitleGap));
        }

        Present(worldHitPoint);
        SetPanelHeight(HintHeightFor(body));
    }

    private float HintHeightFor(string body)
    {
        if (_hintBodyLabel == null) return HintBodyTop + HintBottomPad;
        float width = _hintBodyLabel.rectTransform.rect.width;
        float bodyHeight = _hintBodyLabel.GetPreferredValues(body, width, 0f).y;
        return HintBodyTop + bodyHeight + HintBottomPad;
    }

    private void SetPanelHeight(float height)
    {
        if (_canvasRect != null)
            _canvasRect.sizeDelta = new Vector2(_canvasRect.sizeDelta.x, height);
    }

    private static float PanelHeightForRows(int rowCount)
        => HintBodyTop + rowCount * RowHeight + (rowCount - 1) * RowGap + HintBottomPad;

    private void ReflowCellGroup()
    {
        float y = -(TopPad + TitleHeight + TitleGap);
        y = PlaceRow(_columnCaptionRT, _columnLabelRT, y);
        y = PlaceRow(_rowCaptionRT, _rowValueRT, y);
        y = PlaceRow(_valueCaptionRT, _valueLabelRT, y);
    }

    private float PlaceRow(RectTransform aRT, RectTransform bRT, float y)
    {
        bool aOn = aRT != null && aRT.gameObject.activeSelf;
        bool bOn = bRT != null && bRT.gameObject.activeSelf;
        if (!aOn && !bOn) return y;
        if (aOn) aRT.anchoredPosition = new Vector2(aRT.anchoredPosition.x, y);
        if (bOn) bRT.anchoredPosition = new Vector2(bRT.anchoredPosition.x, y);
        return y - RowHeight - RowGap;
    }

    private void BuildSheetRows()
    {
        if (_sheetSumLabel == null)
            _sheetSumLabel = CloneLabel(_sheetCellsLabel, "SheetSum", "0",
                GetPreviousSiblingRect(RectOf(_sheetCellsLabel)), "SumLabel", "Sum");

        SetCaptionText(_sheetCellsLabel, "Count");
        HideRow(_sheetMinLabel, _sheetMinLocLabel);
        HideRow(_sheetMaxLabel, _sheetMaxLocLabel);

        _sheetRows.Clear();
        AddStatRow(_sheetRows, _sheetCellsLabel);
        AddStatRow(_sheetRows, _sheetMeanLabel);
        AddStatRow(_sheetRows, _sheetSumLabel);
    }

    private void BuildHeaderRows()
    {
        if (_headerSumLabel == null)
            _headerSumLabel = CloneLabel(_headerCellsLabel, "HeaderSum", "0",
                GetPreviousSiblingRect(RectOf(_headerCellsLabel)), "HeaderSumLabel", "Sum");

        SetCaptionText(_headerCellsLabel, "Count");
        HideRow(_headerMinLabel, _headerMinLocLabel);
        HideRow(_headerMaxLabel, _headerMaxLocLabel);

        _headerRows.Clear();
        AddStatRow(_headerRows, _headerCellsLabel);
        AddStatRow(_headerRows, _headerMeanLabel);
        AddStatRow(_headerRows, _headerSumLabel);
    }

    private TextMeshProUGUI CloneLabel(TextMeshProUGUI value, string valueName, string valueText,
        RectTransform caption, string captionName, string captionText)
    {
        if (value == null) return null;

        if (caption != null)
        {
            GameObject capClone = Instantiate(caption.gameObject, caption.parent);
            capClone.name = captionName;
            TextMeshProUGUI capLabel = capClone.GetComponent<TextMeshProUGUI>();
            if (capLabel != null) { capLabel.text = captionText; capLabel.raycastTarget = false; }
        }

        GameObject valClone = Instantiate(value.gameObject, value.transform.parent);
        valClone.name = valueName;
        TextMeshProUGUI valLabel = valClone.GetComponent<TextMeshProUGUI>();
        if (valLabel != null) { valLabel.text = valueText; valLabel.raycastTarget = false; }
        return valLabel;
    }

    private void AddStatRow(List<StatRow> rows, TextMeshProUGUI value)
    {
        if (value == null) return;
        RectTransform valueRT = RectOf(value);
        rows.Add(new StatRow
        {
            Caption = GetPreviousSiblingRect(valueRT),
            Value = valueRT
        });
    }

    private void SetCaptionText(TextMeshProUGUI value, string text)
    {
        RectTransform cap = GetPreviousSiblingRect(RectOf(value));
        if (cap == null) return;
        TextMeshProUGUI label = cap.GetComponent<TextMeshProUGUI>();
        if (label != null) label.text = text;
    }

    private void HideRow(TextMeshProUGUI value, TextMeshProUGUI loc)
    {
        if (value != null)
        {
            RectTransform cap = GetPreviousSiblingRect(RectOf(value));
            if (cap != null) cap.gameObject.SetActive(false);
            value.gameObject.SetActive(false);
        }
        if (loc != null) loc.gameObject.SetActive(false);
    }

    private void ReflowSheetGroup() =>
        ReflowStatRows(_sheetRows, SheetCaptionX, SheetCaptionWidth, SheetValueX, SheetValueWidth);

    private void ReflowHeaderGroup() =>
        ReflowStatRows(_headerRows, HeaderCaptionX, HeaderCaptionWidth, HeaderValueX, HeaderValueWidth);

    private void ReflowStatRows(List<StatRow> rows, float captionX, float captionWidth,
        float valueX, float valueWidth)
    {
        float y = StatFirstRowY;
        for (int i = 0; i < rows.Count; i++)
        {
            StatRow row = rows[i];
            SetRowLayout(row.Caption, captionX, y, captionWidth);
            SetRowLayout(row.Value, valueX, y, valueWidth);
            y -= StatRowPitch;
        }
    }

    private static void SetRowLayout(RectTransform rt, float x, float y, float width)
    {
        if (rt == null) return;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
    }

    public void ShowSheet(Vector3 worldHitPoint, int cellCount,
        float maxValue, string maxLocation, float minValue, string minLocation,
        float meanValue, float sumValue, bool isCurrency, IReadOnlyList<Color> swatchColors)
    {
        SetMode(Mode.Sheet);

        ShowSwatches(swatchColors);
        if (_sheetTitleLabel != null) _sheetTitleLabel.text = "Sheet";

        if (_sheetMaxLabel != null) _sheetMaxLabel.text = FormatCompactValue(maxValue, isCurrency);
        if (_sheetMaxLocLabel != null) _sheetMaxLocLabel.text = maxLocation;
        if (_sheetMinLabel != null) _sheetMinLabel.text = FormatCompactValue(minValue, isCurrency);
        if (_sheetMinLocLabel != null) _sheetMinLocLabel.text = minLocation;
        if (_sheetMeanLabel != null) _sheetMeanLabel.text = FormatCompactValue(meanValue, isCurrency);
        if (_sheetCellsLabel != null) _sheetCellsLabel.text = cellCount.ToString();
        if (_sheetSumLabel != null) _sheetSumLabel.text = FormatCompactValue(sumValue, isCurrency);

        ReflowSheetGroup();
        SetPanelHeight(PanelHeightForRows(3));
        Present(worldHitPoint);
    }

    public void ShowHeader(Vector3 worldHitPoint, string title, int cellCount,
        float meanValue, float sumValue, bool isCurrency)
    {
        SetMode(Mode.Header);

        HideSwatches();
        if (_headerTitleLabel != null) _headerTitleLabel.text = title;

        if (_headerCellsLabel != null) _headerCellsLabel.text = cellCount.ToString();
        if (_headerMeanLabel != null) _headerMeanLabel.text = FormatCompactValue(meanValue, isCurrency);
        if (_headerSumLabel != null) _headerSumLabel.text = FormatCompactValue(sumValue, isCurrency);

        ReflowHeaderGroup();
        SetPanelHeight(PanelHeightForRows(3));
        Present(worldHitPoint);
    }

    public bool IsVisible => _canvas != null && _canvas.gameObject.activeSelf;

    public void Hide()
    {
        if (_canvas != null && _canvas.gameObject.activeSelf)
            _canvas.gameObject.SetActive(false);

        for (int i = 0; i < _extraSwatches.Count; i++)
            if (_extraSwatches[i] != null) _extraSwatches[i].gameObject.SetActive(false);

        if (_colorSwatch != null && _hasDefaultSwatchColor)
            _colorSwatch.color = _defaultSwatchColor;
    }

    private void HideSwatches()
    {
        if (_colorSwatch != null) _colorSwatch.gameObject.SetActive(false);
        for (int i = 0; i < _extraSwatches.Count; i++)
            if (_extraSwatches[i] != null) _extraSwatches[i].gameObject.SetActive(false);
    }

    private void ShowSwatch(Color color)
    {
        _swatchBuffer.Clear();
        _swatchBuffer.Add(color);
        ShowSwatches(_swatchBuffer);
    }

    private void ShowSwatches(IReadOnlyList<Color> colors)
    {
        if (_colorSwatch == null) return;

        int n = colors != null ? colors.Count : 0;
        if (n == 0) return;

        EnsureExtraSwatches(n - 1);

        _colorSwatch.gameObject.SetActive(true);
        PlaceSwatch(_colorSwatchRT, _swatchBasePos.x);
        _colorSwatch.color = Opaque(colors[0]);

        for (int i = 0; i < _extraSwatches.Count; i++)
        {
            Image sw = _extraSwatches[i];
            if (sw == null) continue;
            int colorIndex = i + 1;
            if (colorIndex < n)
            {
                sw.gameObject.SetActive(true);
                PlaceSwatch(sw.rectTransform, _swatchBasePos.x + colorIndex * (SwatchSize + SwatchGap));
                sw.color = Opaque(colors[colorIndex]);
            }
            else
            {
                sw.gameObject.SetActive(false);
            }
        }

        ShiftSheetTitle(n);
    }

    private void EnsureExtraSwatches(int needed)
    {
        while (_extraSwatches.Count < needed)
        {
            GameObject clone = Instantiate(_colorSwatch.gameObject, _colorSwatch.transform.parent);
            clone.name = "ColorSwatchExtra";
            Image img = clone.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
            _extraSwatches.Add(img);
        }
    }

    private void PlaceSwatch(RectTransform rt, float x)
    {
        if (rt == null) return;
        rt.anchoredPosition = new Vector2(x, _swatchBasePos.y);
        rt.sizeDelta = new Vector2(SwatchSize, SwatchSize);
    }

    private void ShiftSheetTitle(int swatchCount)
    {
        if (!_sheetTitleCaptured || _sheetTitleRT == null) return;

        float lastRight = _swatchBasePos.x + (swatchCount - 1) * (SwatchSize + SwatchGap) + SwatchSize;
        float titleX = lastRight + SwatchTitleGap;
        float titleRight = _sheetTitleBasePos.x + _sheetTitleBaseWidth;
        float width = Mathf.Max(40f, titleRight - titleX);

        _sheetTitleRT.anchoredPosition = new Vector2(titleX, _sheetTitleBasePos.y);
        _sheetTitleRT.sizeDelta = new Vector2(width, _sheetTitleRT.sizeDelta.y);
    }

    private static Color Opaque(Color c)
    {
        c.a = 1f;
        return c;
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
            + blended * zOffsetFromCamera
            + Vector3.up * yOffsetFromCamera
            + right * xOffsetFromCamera;

        transform.rotation = Quaternion.LookRotation(blended, Vector3.up);
    }

    private void Present(Vector3 worldHitPoint)
    {
        UpdatePosition(worldHitPoint);
        if (_canvas != null && !_canvas.gameObject.activeSelf)
            _canvas.gameObject.SetActive(true);
    }

    private void SetMode(Mode mode)
    {
        if (_cellGroup != null) _cellGroup.SetActive(mode == Mode.Cell);
        if (_hintGroup != null) _hintGroup.SetActive(mode == Mode.Hint);
        if (_headerGroup != null) _headerGroup.SetActive(mode == Mode.Header);
        if (_sheetGroup != null) _sheetGroup.SetActive(mode == Mode.Sheet);
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
