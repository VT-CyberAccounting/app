using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SheetPin : MonoBehaviour
{
    private static Material _sharedLineMaterial;

    public static Material SharedLineMaterial
    {
        get
        {
            if (_sharedLineMaterial == null)
                _sharedLineMaterial = new Material(Shader.Find("Sprites/Default"));
            return _sharedLineMaterial;
        }
    }

    private LineRenderer _line;
    private SheetGenerator _sheet;

    public void Init(SheetGenerator sheet, Color color, float width)
    {
        _sheet = sheet;

        _line = GetComponent<LineRenderer>();
        _line.useWorldSpace = false;
        _line.loop = true;
        _line.positionCount = 4;
        _line.widthMultiplier = width;
        _line.numCornerVertices = 0;
        _line.numCapVertices = 0;
        _line.alignment = LineAlignment.View;
        _line.textureMode = LineTextureMode.Stretch;
        _line.sharedMaterial = SharedLineMaterial;
        _line.startColor = color;
        _line.endColor = color;

        Hide();
    }

    public const float Lift = 0.01f;
    public const int HoverSortingOrder = 100;

    public void SetSortingOrder(int order)
    {
        if (_line != null) _line.sortingOrder = order;
    }

    public void Show(SheetTarget sheet, SheetManager sheets)
    {
        if (_sheet == null || _line == null || sheet.kind != SheetTarget.Kind.Sheet)
        {
            Hide();
            return;
        }

        ComputeCorners(_sheet, sheets, sheet.visRowMin, sheet.visRowMax, sheet.visColMin, sheet.visColMax,
            out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d);
        SetCorners(a, b, c, d);
    }

    public static void ComputeCorners(SheetGenerator sheetGenerator, SheetManager sheets,
        int rowMin, int rowMax, int colMin, int colMax,
        out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d)
    {
        float colSp = sheetGenerator.CurrentColumnSpacing;
        float rowSp = sheetGenerator.CurrentRowSpacing;
        float half = sheetGenerator.LockedCellSize * 0.5f;
        float y = sheetGenerator.SheetTopY + Lift;

        float xMin = colMin * colSp - half;
        float xMax = colMax * colSp + half;
        float zMin = rowMin * rowSp - half;
        float zMax = rowMax * rowSp + half;

        a = new Vector3(xMin, y, zMin);
        b = new Vector3(xMax, y, zMin);
        c = new Vector3(xMax, y, zMax);
        d = new Vector3(xMin, y, zMax);

        SheetManager.Sheet sheet = sheets != null && sheets.IsBaked ? sheets.SheetAt(rowMin, colMin) : null;
        if (sheet == null || sheet.piece == null) return;

        float centerX = (sheet.colMin + sheet.colMax) * 0.5f * colSp;
        float centerZ = (sheet.rowMin + sheet.rowMax) * 0.5f * rowSp;
        Vector3 center = new Vector3(centerX, 0f, centerZ);
        Transform pt = sheet.piece.transform;
        a = pt.localPosition + pt.localRotation * (a - center);
        b = pt.localPosition + pt.localRotation * (b - center);
        c = pt.localPosition + pt.localRotation * (c - center);
        d = pt.localPosition + pt.localRotation * (d - center);
    }

    public void SetCorners(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        if (_line == null) return;
        _line.SetPosition(0, a);
        _line.SetPosition(1, b);
        _line.SetPosition(2, c);
        _line.SetPosition(3, d);
        _line.enabled = true;
    }

    public void SetColor(Color color)
    {
        if (_line == null) return;
        _line.startColor = color;
        _line.endColor = color;
    }

    public void Hide()
    {
        if (_line != null) _line.enabled = false;
    }
}
