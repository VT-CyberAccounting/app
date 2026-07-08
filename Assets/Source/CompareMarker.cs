using UnityEngine;

public class CompareMarker : MonoBehaviour
{
    private SheetGenerator _sheet;
    private SheetManager _sheets;
    private SheetPin _sheetPin;

    private int _rowMin, _rowMax, _colMin, _colMax;

    public void Init(SheetGenerator sheet, SheetManager sheets, SheetPin sheetPin,
        int rowMin, int rowMax, int colMin, int colMax)
    {
        _sheet = sheet;
        _sheets = sheets;
        _sheetPin = sheetPin;
        _rowMin = rowMin;
        _rowMax = rowMax;
        _colMin = colMin;
        _colMax = colMax;
        Apply();
    }

    private void LateUpdate() => Apply();

    private void Apply()
    {
        if (_sheet == null || _sheetPin == null) return;

        SheetPin.ComputeCorners(_sheet, _sheets, _rowMin, _rowMax, _colMin, _colMax,
            out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d);
        _sheetPin.SetCorners(a, b, c, d);
    }
}
