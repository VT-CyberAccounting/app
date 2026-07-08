using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SheetPiece : MonoBehaviour
{
    public int rowMin, rowMax, colMin, colMax;

    private const UnityEngine.Rendering.MeshUpdateFlags FastUpload =
        UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds |
        UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices;

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;

    private Vector3[] _verts;
    private Color[] _colors;
    private int[] _tris;
    private int _vertCount;
    private int _triCount;

    private float[] _cellNorm;
    private Color[] _cellColor;
    private bool[] _cellOverride;
    private float[] _colPos;
    private int[] _colCell;
    private float[] _colFH;
    private int _nCol;
    private float[] _rowPos;
    private int[] _rowCell;
    private float[] _rowFH;
    private int _nRow;

    private Grabbable _grabbable;
    private HandGrabInteractable _handGrab;
    private Collider[] _grabColliders;
    private OneGrabTranslateTransformer _slideTransformer;

    private SheetManager _manager;
    private Vector3 _lastValidLocalPos;
    private bool _wasGrabbed;
    private Vector3 _grabStartLocalPos;
    private Quaternion _grabStartLocalRot;

    public SheetManager Manager => _manager;

    public void Build(SheetGenerator sheet, DataSource data, SheetManager sheets, Material material,
        int rMin, int rMax, int cMin, int cMax, Vector3 localOffset, float yGrabBounds)
    {
        rowMin = rMin; rowMax = rMax; colMin = cMin; colMax = cMax;
        _manager = sheets;

        if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
        if (material != null) _meshRenderer.sharedMaterial = material;

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "SheetPiece" };
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        int rows = rMax - rMin + 1;
        int cols = cMax - cMin + 1;

        float colSpacing = sheet.CurrentColumnSpacing;
        float rowSpacing = sheet.CurrentRowSpacing;
        float height = sheet.SheetTopY;
        float alpha = sheet.sheetAlpha;

        float centerX = (cMin + cMax) * 0.5f * colSpacing;
        float centerZ = (rMin + rMax) * 0.5f * rowSpacing;

        float colHalf = colSpacing * 0.5f;
        float rowHalf = rowSpacing * 0.5f;

        SampleCells(sheet, data, sheets, rMin, cMin, rows, cols);
        BuildColumnStops(rows, cols, cMin, colSpacing, colHalf);
        BuildRowStops(rows, cols, rMin, rowSpacing, rowHalf);

        int nCol = _nCol;
        int nRow = _nRow;
        _vertCount = nRow * nCol;
        if (_verts == null || _verts.Length < _vertCount)
        {
            _verts = new Vector3[_vertCount];
            _colors = new Color[_vertCount];
        }

        float maxY = 0f;
        for (int i = 0; i < nRow; i++)
        {
            float gz = _rowPos[i];
            float fr = _rowFH[i];
            int rc = _rowCell[i];
            for (int j = 0; j < nCol; j++)
            {
                int idx = i * nCol + j;
                float gx = _colPos[j];
                float norm = SampleNorm(fr, _colFH[j], rows, cols);
                float y = norm * height;
                if (y > maxY) maxY = y;
                _verts[idx] = new Vector3(gx - centerX, y, gz - centerZ);
                Color col = _cellColor[rc * cols + _colCell[j]];
                col.a = alpha;
                _colors[idx] = col;
            }
        }

        int quadCount = (nRow - 1) * (nCol - 1);
        int triCount = quadCount * 6;
        _triCount = triCount;
        if (_tris == null || _tris.Length < triCount)
            _tris = new int[triCount];

        int t = 0;
        for (int r = 0; r < nRow - 1; r++)
        {
            for (int c = 0; c < nCol - 1; c++)
            {
                int topLeft = r * nCol + c;
                int topRight = topLeft + 1;
                int bottomLeft = (r + 1) * nCol + c;
                int bottomRight = bottomLeft + 1;

                _tris[t++] = topLeft;
                _tris[t++] = bottomLeft;
                _tris[t++] = topRight;
                _tris[t++] = topRight;
                _tris[t++] = bottomLeft;
                _tris[t++] = bottomRight;
            }
        }

        _mesh.Clear();
        _mesh.SetVertices(_verts, 0, _vertCount, FastUpload);
        _mesh.SetColors(_colors, 0, _vertCount, FastUpload);
        _mesh.SetTriangles(_tris, 0, triCount, 0, false);

        float sizeX = cols * colSpacing;
        float sizeZ = rows * rowSpacing;
        _mesh.bounds = new Bounds(
            new Vector3(0f, maxY * 0.5f, 0f),
            new Vector3(sizeX, maxY, sizeZ));

        _meshFilter.sharedMesh = _mesh;

        transform.localPosition = new Vector3(centerX, 0f, centerZ) + localOffset;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        _lastValidLocalPos = transform.localPosition;
        _wasGrabbed = false;

        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc != null) { bc.center = _mesh.bounds.center; bc.size = _mesh.bounds.size; }

        float restY = transform.localPosition.y;
        ConfigureSlideConstraint(restY - yGrabBounds, restY + yGrabBounds);
    }

    private void ConfigureSlideConstraint(float minLocalY, float maxLocalY)
    {
        if (_slideTransformer == null) _slideTransformer = GetComponent<OneGrabTranslateTransformer>();
        if (_slideTransformer == null) return;

        _slideTransformer.Constraints = new OneGrabTranslateTransformer.OneGrabTranslateConstraints
        {
            ConstraintsAreRelative = false,
            MinX = new FloatConstraint(),
            MaxX = new FloatConstraint(),
            MinY = new FloatConstraint { Constrain = true, Value = minLocalY },
            MaxY = new FloatConstraint { Constrain = true, Value = maxLocalY },
            MinZ = new FloatConstraint(),
            MaxZ = new FloatConstraint()
        };
    }

    private void SampleCells(SheetGenerator sheet, DataSource data, SheetManager sheets, int rMin, int cMin, int rows, int cols)
    {
        int cellCount = rows * cols;
        if (_cellNorm == null || _cellNorm.Length < cellCount)
        {
            _cellNorm = new float[cellCount];
            _cellColor = new Color[cellCount];
            _cellOverride = new bool[cellCount];
        }

        for (int r = 0; r < rows; r++)
        {
            int visRow = rMin + r;
            int dataRow = sheet.VisibleRowToData(visRow);
            for (int c = 0; c < cols; c++)
            {
                int visCol = cMin + c;
                int dataCol = sheet.VisibleColToData(visCol);
                float norm = (dataRow >= 0 && dataCol >= 0) ? data.GetNormalizedValue(dataRow, dataCol) : 0f;
                int ci = r * cols + c;
                _cellNorm[ci] = norm;
                if (sheets != null && sheets.TryGetOverride(visRow, visCol, out Color ov))
                {
                    _cellColor[ci] = ov;
                    _cellOverride[ci] = true;
                }
                else
                {
                    _cellColor[ci] = Heatmap.Sample(norm);
                    _cellOverride[ci] = false;
                }
            }
        }
    }

    private float SampleNorm(float fr, float fc, int rows, int cols)
    {
        if (fr < 0f) fr = 0f; else if (fr > rows - 1) fr = rows - 1;
        if (fc < 0f) fc = 0f; else if (fc > cols - 1) fc = cols - 1;
        int r0 = (int)fr; int r1 = r0 + 1 < rows ? r0 + 1 : r0; float tr = fr - r0;
        int c0 = (int)fc; int c1 = c0 + 1 < cols ? c0 + 1 : c0; float tc = fc - c0;
        float n00 = _cellNorm[r0 * cols + c0];
        float n01 = _cellNorm[r0 * cols + c1];
        float n10 = _cellNorm[r1 * cols + c0];
        float n11 = _cellNorm[r1 * cols + c1];
        return Mathf.Lerp(Mathf.Lerp(n00, n01, tc), Mathf.Lerp(n10, n11, tc), tr);
    }

    private void BuildColumnStops(int rows, int cols, int cMin, float colSpacing, float colHalf)
    {
        int maxStops = cols * 3 + 1;
        if (_colPos == null || _colPos.Length < maxStops)
        {
            _colPos = new float[maxStops];
            _colCell = new int[maxStops];
            _colFH = new float[maxStops];
        }

        int n = 0;
        _colPos[n] = cMin * colSpacing - colHalf; _colCell[n] = 0; _colFH[n] = 0f; n++;
        for (int c = 0; c < cols; c++)
        {
            _colPos[n] = (cMin + c) * colSpacing; _colCell[n] = c; _colFH[n] = c; n++;
            if (c < cols - 1 && ColumnSeam(c, rows, cols))
            {
                float xb = (cMin + c + 0.5f) * colSpacing;
                _colPos[n] = xb; _colCell[n] = c; _colFH[n] = c + 0.5f; n++;
                _colPos[n] = xb; _colCell[n] = c + 1; _colFH[n] = c + 0.5f; n++;
            }
        }
        _colPos[n] = (cMin + cols - 1) * colSpacing + colHalf; _colCell[n] = cols - 1; _colFH[n] = cols - 1; n++;
        _nCol = n;
    }

    private void BuildRowStops(int rows, int cols, int rMin, float rowSpacing, float rowHalf)
    {
        int maxStops = rows * 3 + 1;
        if (_rowPos == null || _rowPos.Length < maxStops)
        {
            _rowPos = new float[maxStops];
            _rowCell = new int[maxStops];
            _rowFH = new float[maxStops];
        }

        int n = 0;
        _rowPos[n] = rMin * rowSpacing - rowHalf; _rowCell[n] = 0; _rowFH[n] = 0f; n++;
        for (int r = 0; r < rows; r++)
        {
            _rowPos[n] = (rMin + r) * rowSpacing; _rowCell[n] = r; _rowFH[n] = r; n++;
            if (r < rows - 1 && RowSeam(r, rows, cols))
            {
                float zb = (rMin + r + 0.5f) * rowSpacing;
                _rowPos[n] = zb; _rowCell[n] = r; _rowFH[n] = r + 0.5f; n++;
                _rowPos[n] = zb; _rowCell[n] = r + 1; _rowFH[n] = r + 0.5f; n++;
            }
        }
        _rowPos[n] = (rMin + rows - 1) * rowSpacing + rowHalf; _rowCell[n] = rows - 1; _rowFH[n] = rows - 1; n++;
        _nRow = n;
    }

    private bool ColumnSeam(int c, int rows, int cols)
    {
        for (int r = 0; r < rows; r++)
        {
            int a = r * cols + c;
            int b = a + 1;
            if ((_cellOverride[a] || _cellOverride[b]) && !ColorsEqual(_cellColor[a], _cellColor[b]))
                return true;
        }
        return false;
    }

    private bool RowSeam(int r, int rows, int cols)
    {
        for (int c = 0; c < cols; c++)
        {
            int a = r * cols + c;
            int b = a + cols;
            if ((_cellOverride[a] || _cellOverride[b]) && !ColorsEqual(_cellColor[a], _cellColor[b]))
                return true;
        }
        return false;
    }

    private static bool ColorsEqual(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.003f && Mathf.Abs(a.g - b.g) < 0.003f && Mathf.Abs(a.b - b.b) < 0.003f;

    public void AppendCollider(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<int> tris)
    {
        if (_mesh == null || _verts == null || _tris == null) return;

        int baseIndex = verts.Count;
        Vector3 pos = transform.localPosition;
        Quaternion rot = transform.localRotation;
        for (int i = 0; i < _vertCount; i++)
            verts.Add(pos + rot * _verts[i]);
        for (int i = 0; i < _triCount; i++)
            tris.Add(baseIndex + _tris[i]);
    }

    public void SetGrabbable(bool on)
    {
        if (_grabbable == null) _grabbable = GetComponentInChildren<Grabbable>(true);
        if (_handGrab == null) _handGrab = GetComponentInChildren<HandGrabInteractable>(true);
        if (_grabColliders == null) _grabColliders = GetComponentsInChildren<Collider>(true);

        if (_grabbable != null) _grabbable.enabled = on;
        if (_handGrab != null) _handGrab.enabled = on;
        for (int i = 0; i < _grabColliders.Length; i++)
            if (_grabColliders[i] != null) _grabColliders[i].enabled = on;
    }

    private void LateUpdate()
    {
        if (_manager == null) return;
        if (_grabbable == null) _grabbable = GetComponentInChildren<Grabbable>(true);

        bool grabbed = _grabbable != null && _grabbable.SelectingPointsCount > 0;

        if (grabbed && !_wasGrabbed)
        {
            _grabStartLocalPos = transform.localPosition;
            _grabStartLocalRot = transform.localRotation;
        }

        if (grabbed)
        {
            Vector3 candidate = transform.localPosition;
            Vector3 resolved = _manager.ResolveGrabPosition(this, _lastValidLocalPos, candidate);
            if (resolved != candidate) transform.localPosition = resolved;
            _lastValidLocalPos = resolved;
        }
        else
        {
            if (_wasGrabbed)
            {
                _manager.SettlePiece(this);
                bool moved = (transform.localPosition - _grabStartLocalPos).sqrMagnitude > 1e-6f
                    || Quaternion.Angle(transform.localRotation, _grabStartLocalRot) > 0.05f;
                if (moved) _manager.NotifyMoveCommitted(this, _grabStartLocalPos, _grabStartLocalRot);
            }
            _lastValidLocalPos = transform.localPosition;
        }

        _wasGrabbed = grabbed;
    }

    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
    }
}
