using System;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SheetGenerator : MonoBehaviour
{
    public event Action OnSheetLayoutChanged;
    public event Action OnSheetCollapseStarted;

    public DataSource dataSource;

    [UnityEngine.Serialization.FormerlySerializedAs("fullDatasetSpan")]
    [UnityEngine.Serialization.FormerlySerializedAs("maximumRegionXOrZ")]
    public float maximumSheetXOrZ = 1f;
    [UnityEngine.Serialization.FormerlySerializedAs("maxSurfaceHeight")]
    [UnityEngine.Serialization.FormerlySerializedAs("maximumY")]
    [UnityEngine.Serialization.FormerlySerializedAs("maximumRegionY")]
    public float maximumSheetY = 1f;

    [UnityEngine.Serialization.FormerlySerializedAs("zOffsetFromCamera")]
    [UnityEngine.Serialization.FormerlySerializedAs("minEdgeDistanceFromCamera")]
    public float minimumZOffsetFromCamera = 0f;

    [Range(0.1f, 1.0f)]
    [UnityEngine.Serialization.FormerlySerializedAs("surfaceAlpha")]
    [UnityEngine.Serialization.FormerlySerializedAs("regionAlpha")]
    public float sheetAlpha = 0.5f;

    [UnityEngine.Serialization.FormerlySerializedAs("animationSpeed")]
    [UnityEngine.Serialization.FormerlySerializedAs("regionRiseSpeed")]
    public float sheetRiseSpeed = 3.0f;
    [UnityEngine.Serialization.FormerlySerializedAs("collapseSpeed")]
    [UnityEngine.Serialization.FormerlySerializedAs("regionFallSpeed")]
    public float sheetFallSpeed = 3f;

    private DataSource _boundSource;

    private DataSource ResolveSource()
    {
        if (dataSource != null) return dataSource;
        return DatasetManager.ActiveSource ?? FileReader.Instance;
    }

    public void SetDataSource(DataSource source)
    {
        StopAllCoroutines();
        Unbind();
        dataSource = source;
        Bind();
    }

    private void Bind()
    {
        DataSource source = dataSource;
        if (source == null) return;
        _boundSource = source;
        if (source.IsLoaded) OnDataLoaded();
        else source.OnDataLoaded += OnDataLoaded;
        source.OnFilterChanged += OnFilterChanged;
    }

    private void Unbind()
    {
        if (_boundSource == null) return;
        _boundSource.OnDataLoaded -= OnDataLoaded;
        _boundSource.OnFilterChanged -= OnFilterChanged;
        _boundSource = null;
    }

    private enum SheetState { Idle, Collapsing, Rising }

    private Mesh _mesh;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private MeshCollider _meshCollider;
    private SheetManager _sheets;

    private Vector3[] _vertices;
    private Vector3[] _targetVertices;
    private Color[] _colors;
    private Color[] _targetColors;
    private int[] _triangles;

    private int _maxVertexCount;
    private int _activeVertexCount;
    private int _activeTriangleCount;
    private int _rowCount;
    private int _visibleColCount;
    private int _renderRowCount;
    private int _renderColCount;
    private int _gridRowCount;
    private int _gridColCount;
    private float _currentColumnSpacing;
    private float _currentRowSpacing;
    private float _fixedColSpacing;
    private float _fixedRowSpacing;
    private int _fullColCount;
    private int _fullRowCount;
    private List<int> _activeColumnIndices = new List<int>();
    private List<int> _visibleRowIndices = new List<int>();
    private SheetState _state = SheetState.Idle;
    private bool _meshBuilt;
    private bool _arraysAllocated;

    private const float CONVERGENCE_THRESHOLD = 0.001f;
    private const float CONVERGENCE_THRESHOLD_SQ = CONVERGENCE_THRESHOLD * CONVERGENCE_THRESHOLD;

    private const UnityEngine.Rendering.MeshUpdateFlags FastUpload =
        UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds |
        UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices;

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _meshCollider = GetComponent<MeshCollider>();
        _mesh = new Mesh { name = "DataSheet" };
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _meshFilter.mesh = _mesh;
        ApplyMaterialBlendMode();
    }

    private void OnEnable()
    {
        StartCoroutine(WaitForDataManager());
    }

    private System.Collections.IEnumerator WaitForDataManager()
    {
        DataSource source = ResolveSource();
        while (source == null)
        {
            yield return null;
            source = ResolveSource();
        }

        if (_boundSource == source) yield break;
        dataSource = source;
        Bind();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        Unbind();
    }

    private void Update()
    {
        if (_state == SheetState.Idle || !_meshBuilt) return;

        bool verticesMoving = false;
        bool colorsMoving = false;
        float speed = (_state == SheetState.Collapsing) ? sheetFallSpeed : sheetRiseSpeed;
        float step = speed * Time.deltaTime;

        for (int i = 0; i < _activeVertexCount; i++)
        {
            Vector3 v = _vertices[i];
            Vector3 vt = _targetVertices[i];
            float dvx = v.x - vt.x;
            float dvy = v.y - vt.y;
            float dvz = v.z - vt.z;
            if (dvx * dvx + dvy * dvy + dvz * dvz > CONVERGENCE_THRESHOLD_SQ)
            {
                _vertices[i] = Vector3.MoveTowards(v, vt, step);
                verticesMoving = true;
            }
            else
            {
                _vertices[i] = vt;
            }

            Color c = _colors[i];
            Color ct = _targetColors[i];
            float dcr = c.r - ct.r;
            float dcg = c.g - ct.g;
            float dcb = c.b - ct.b;
            float dca = c.a - ct.a;
            if (dcr * dcr + dcg * dcg + dcb * dcb + dca * dca > CONVERGENCE_THRESHOLD_SQ)
            {
                _colors[i] = Color.Lerp(c, ct, step * 2f);
                colorsMoving = true;
            }
            else
            {
                _colors[i] = ct;
            }
        }

        if (verticesMoving) UploadVertices();
        if (colorsMoving) UploadColors();

        bool stillMoving = verticesMoving || colorsMoving;

        if (!stillMoving)
        {
            if (_state == SheetState.Collapsing)
            {
                BuildSheet();
                _state = SheetState.Rising;
            }
            else
            {
                _state = SheetState.Idle;
                RefreshCollider();
            }
        }
    }

    private void UploadVertices()
    {
        _mesh.SetVertices(_vertices, 0, _activeVertexCount, FastUpload);
    }

    private void UploadColors()
    {
        _mesh.SetColors(_colors, 0, _activeVertexCount, FastUpload);
    }

    private void ApplyMaterialBlendMode()
    {
        Material mat = _meshRenderer != null ? _meshRenderer.sharedMaterial : null;
        if (mat == null || !mat.HasProperty("_SrcBlend")) return;

        bool transparent = sheetAlpha < 0.999f;
        mat.SetFloat("_SrcBlend", (float)(transparent
            ? UnityEngine.Rendering.BlendMode.SrcAlpha
            : UnityEngine.Rendering.BlendMode.One));
        mat.SetFloat("_DstBlend", (float)(transparent
            ? UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
            : UnityEngine.Rendering.BlendMode.Zero));
        mat.SetFloat("_ZWrite", transparent ? 0f : 1f);
        mat.renderQueue = (int)(transparent
            ? UnityEngine.Rendering.RenderQueue.Transparent
            : UnityEngine.Rendering.RenderQueue.Geometry);
    }

    private void AllocateArrays()
    {
        DataSource data = ResolveSource();
        int totalRows = data.RowCount;
        int totalCols = data.ColumnCount;

        int maxGridRows = Mathf.Max(totalRows, 1) + 2;
        int maxGridCols = Mathf.Max(totalCols, 1) + 2;
        _maxVertexCount = maxGridRows * maxGridCols;

        _vertices = new Vector3[_maxVertexCount];
        _targetVertices = new Vector3[_maxVertexCount];
        _colors = new Color[_maxVertexCount];
        _targetColors = new Color[_maxVertexCount];

        int maxQuads = (maxGridRows - 1) * (maxGridCols - 1);
        _triangles = new int[maxQuads * 6];

        _activeVertexCount = 0;

        _fullColCount = totalCols;
        _fullRowCount = totalRows;
        float cellSize = ResolveCellSize(totalRows, totalCols);
        _fixedColSpacing = cellSize;
        _fixedRowSpacing = cellSize;

        _arraysAllocated = true;
    }

    protected virtual float ResolveCellSize(int totalRows, int totalCols)
    {
        int maxSpan = Mathf.Max(Mathf.Max(totalCols, totalRows), 1);
        return maximumSheetXOrZ / maxSpan;
    }

    private void OnDataLoaded()
    {
        AllocateArrays();
        BuildSheet();
        _state = SheetState.Rising;
    }

    private void OnFilterChanged()
    {
        if (!_meshBuilt)
        {
            BuildSheet();
            _state = SheetState.Rising;
            return;
        }

        if (_state == SheetState.Collapsing)
            return;

        StartCollapse();
    }

    private void StartCollapse()
    {
        for (int i = 0; i < _activeVertexCount; i++)
        {
            Vector3 v = _vertices[i];
            _targetVertices[i] = new Vector3(v.x, 0f, v.z);
            _targetColors[i] = new Color(_colors[i].r, _colors[i].g, _colors[i].b, 0f);
        }

        _state = SheetState.Collapsing;
        OnSheetCollapseStarted?.Invoke();
    }

    public void BuildSheet()
    {
        DataSource data = ResolveSource();
        if (data == null || !_arraysAllocated)
        {
            ClearSheet();
            return;
        }

        RefreshVisibleIndices();

        if (_visibleRowIndices.Count == 0)
        {
            ClearSheet();
            return;
        }

        _rowCount = _visibleRowIndices.Count;
        _visibleColCount = _activeColumnIndices.Count;

        if (_visibleColCount == 0 || _rowCount == 0)
        {
            ClearSheet();
            return;
        }

        _currentColumnSpacing = _fixedColSpacing;
        _currentRowSpacing = _fixedRowSpacing;

        _renderRowCount = _rowCount;
        _renderColCount = _visibleColCount;
        _gridRowCount = _renderRowCount + 2;
        _gridColCount = _renderColCount + 2;

        float colHalf = _currentColumnSpacing * 0.5f;
        float rowHalf = _currentRowSpacing * 0.5f;

        int newVertexCount = _gridRowCount * _gridColCount;

        for (int gr = 0; gr < _gridRowCount; gr++)
        {
            int r = Mathf.Clamp(gr - 1, 0, _renderRowCount - 1);
            int dataRowIndex = _visibleRowIndices[r];
            float z = r * _currentRowSpacing
                + (gr == 0 ? -rowHalf : gr == _gridRowCount - 1 ? rowHalf : 0f);
            for (int gc = 0; gc < _gridColCount; gc++)
            {
                int vc = Mathf.Clamp(gc - 1, 0, _renderColCount - 1);
                int dataColIndex = _activeColumnIndices[vc];
                int idx = gr * _gridColCount + gc;
                float normalized = data.GetNormalizedValue(dataRowIndex, dataColIndex);

                float x = vc * _currentColumnSpacing
                    + (gc == 0 ? -colHalf : gc == _gridColCount - 1 ? colHalf : 0f);
                float y = normalized * maximumSheetY;

                Color color = Heatmap.Sample(normalized);
                color.a = sheetAlpha;

                _targetVertices[idx] = new Vector3(x, y, z);
                _targetColors[idx] = color;

                _vertices[idx] = new Vector3(x, 0f, z);
                _colors[idx] = new Color(color.r, color.g, color.b, 0f);
            }
        }

        for (int i = newVertexCount; i < _activeVertexCount; i++)
        {
            _vertices[i] = Vector3.zero;
            _targetVertices[i] = Vector3.zero;
            _colors[i] = Color.clear;
            _targetColors[i] = Color.clear;
        }

        _activeVertexCount = newVertexCount;

        BuildTriangles();

        _mesh.Clear();
        _mesh.SetVertices(_vertices, 0, _activeVertexCount, FastUpload);
        _mesh.SetTriangles(_triangles, 0, _activeTriangleCount, 0, false);
        _mesh.SetColors(_colors, 0, _activeVertexCount, FastUpload);
        SetFixedBounds();

        _meshBuilt = true;
        OnSheetLayoutChanged?.Invoke();
    }

    private void ClearSheet()
    {
        _mesh.Clear();
        _meshBuilt = false;
        _rowCount = 0;
        _visibleColCount = 0;
        _renderRowCount = 0;
        _renderColCount = 0;
        _gridRowCount = 0;
        _gridColCount = 0;
        _activeVertexCount = 0;
        OnSheetLayoutChanged?.Invoke();
    }

    private void SetFixedBounds()
    {
        float colHalf = _currentColumnSpacing * 0.5f;
        float rowHalf = _currentRowSpacing * 0.5f;
        float xMin = -colHalf;
        float xMax = (_renderColCount - 1) * _currentColumnSpacing + colHalf;
        float zMin = -rowHalf;
        float zMax = (_renderRowCount - 1) * _currentRowSpacing + rowHalf;
        float width = xMax - xMin;
        float depth = zMax - zMin;
        _mesh.bounds = new Bounds(
            new Vector3((xMin + xMax) * 0.5f, maximumSheetY * 0.5f, (zMin + zMax) * 0.5f),
            new Vector3(width, maximumSheetY, depth));
    }

    private void RefreshCollider()
    {
        if (_meshCollider == null) return;
        if (_sheets != null && _sheets.IsBaked) return;
        _meshCollider.sharedMesh = null;
        _meshCollider.sharedMesh = _mesh;
    }

    public void SetSheetResolver(SheetManager sheets) => _sheets = sheets;

    private bool _presented = true;

    public bool IsPresented => _presented;

    public void SetPresented(bool presented)
    {
        _presented = presented;
        bool piecesBaked = _sheets != null && _sheets.IsBaked;
        if (_meshRenderer != null) _meshRenderer.enabled = presented && !piecesBaked;
        if (_meshCollider != null) _meshCollider.enabled = presented;
        if (_sheets != null) _sheets.SetPiecesPresented(presented);
    }

    public bool TryGetCellOverride(int visRow, int visCol, out Color color)
    {
        if (_sheets != null) return _sheets.TryGetOverride(visRow, visCol, out color);
        color = default;
        return false;
    }

    public void SetColliderMesh(Mesh mesh)
    {
        if (_meshCollider == null) return;
        _meshCollider.sharedMesh = null;
        _meshCollider.sharedMesh = mesh;
    }

    public void RestoreColliderMesh()
    {
        if (_meshCollider == null) return;
        _meshCollider.sharedMesh = null;
        _meshCollider.sharedMesh = _mesh;
    }

    private void BuildTriangles()
    {
        int quadCount = (_gridRowCount - 1) * (_gridColCount - 1);
        _activeTriangleCount = quadCount * 6;
        int t = 0;

        for (int r = 0; r < _gridRowCount - 1; r++)
        {
            for (int c = 0; c < _gridColCount - 1; c++)
            {
                int topLeft = r * _gridColCount + c;
                int topRight = topLeft + 1;
                int bottomLeft = (r + 1) * _gridColCount + c;
                int bottomRight = bottomLeft + 1;

                _triangles[t++] = topLeft;
                _triangles[t++] = bottomLeft;
                _triangles[t++] = topRight;

                _triangles[t++] = topRight;
                _triangles[t++] = bottomLeft;
                _triangles[t++] = bottomRight;
            }
        }

        for (int i = t; i < _triangles.Length; i++)
            _triangles[i] = 0;
    }

    private void RefreshVisibleIndices()
    {
        DataSource data = ResolveSource();

        _activeColumnIndices.Clear();
        IReadOnlyList<int> visCols = data.VisibleColumnIndices;
        for (int i = 0; i < visCols.Count; i++)
            _activeColumnIndices.Add(visCols[i]);

        _visibleRowIndices.Clear();
        IReadOnlyList<int> visRows = data.VisibleRowIndices;
        for (int i = 0; i < visRows.Count; i++)
            _visibleRowIndices.Add(visRows[i]);
    }

    public (int row, int col) GetNearestCell(Vector3 worldPoint)
    {
        if (!_meshBuilt || _visibleColCount <= 0 || _rowCount <= 0 ||
            _activeColumnIndices.Count == 0 ||
            _currentColumnSpacing <= 0f || _currentRowSpacing <= 0f)
            return (-1, -1);

        if (_sheets != null && _sheets.IsBaked)
        {
            if (!_sheets.TryResolveVisibleCell(worldPoint, out int rvr, out int rvc))
                return (-1, -1);
            int dr = VisibleRowToData(rvr);
            int dc = VisibleColToData(rvc);
            return (dr < 0 || dc < 0) ? (-1, -1) : (dr, dc);
        }

        Vector3 local = transform.InverseTransformPoint(worldPoint);

        int visCol = Mathf.Clamp(Mathf.RoundToInt(local.x / _currentColumnSpacing), 0, _visibleColCount - 1);
        int row = Mathf.Clamp(Mathf.RoundToInt(local.z / _currentRowSpacing), 0, _rowCount - 1);

        visCol = Mathf.Clamp(visCol, 0, _activeColumnIndices.Count - 1);
        int dataCol = _activeColumnIndices[visCol];

        row = Mathf.Clamp(row, 0, _visibleRowIndices.Count - 1);
        int dataRow = _visibleRowIndices[row];

        return (dataRow, dataCol);
    }

    public (int visRow, int visCol) GetNearestVisibleCell(Vector3 worldPoint)
    {
        if (!_meshBuilt || _visibleColCount <= 0 || _rowCount <= 0 ||
            _currentColumnSpacing <= 0f || _currentRowSpacing <= 0f)
            return (-1, -1);

        if (_sheets != null && _sheets.IsBaked)
            return _sheets.TryResolveVisibleCell(worldPoint, out int rvr, out int rvc)
                ? (rvr, rvc)
                : (-1, -1);

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        int visCol = Mathf.Clamp(Mathf.RoundToInt(local.x / _currentColumnSpacing), 0, _visibleColCount - 1);
        int visRow = Mathf.Clamp(Mathf.RoundToInt(local.z / _currentRowSpacing), 0, _rowCount - 1);
        return (visRow, visCol);
    }

    private RayInteractable _syntheticRay;
    private const int SyntheticPointerId = 424242;

    public bool VisibleCellToWorld(int visRow, int visCol, out Vector3 world)
    {
        world = Vector3.zero;
        if (!_meshBuilt || _visibleColCount <= 0 || _rowCount <= 0 ||
            _currentColumnSpacing <= 0f || _currentRowSpacing <= 0f)
            return false;

        if (_sheets != null && _sheets.IsBaked)
            return _sheets.TryGetVisibleCellWorld(visRow, visCol, out world);

        if (visRow < 0 || visRow >= _rowCount || visCol < 0 || visCol >= _visibleColCount)
            return false;

        Vector3 local = new Vector3(visCol * _currentColumnSpacing, maximumSheetY, visRow * _currentRowSpacing);
        world = transform.TransformPoint(local);
        return true;
    }

    public bool PublishSyntheticPointer(PointerEventType type, Vector3 world)
    {
        if (_syntheticRay == null) _syntheticRay = GetComponentInChildren<RayInteractable>(true);
        if (_syntheticRay == null) return false;
        _syntheticRay.PublishPointerEvent(new PointerEvent(SyntheticPointerId, type, new Pose(world, Quaternion.identity)));
        return true;
    }

    public int VisibleRowToData(int visRow) =>
        visRow >= 0 && visRow < _visibleRowIndices.Count ? _visibleRowIndices[visRow] : -1;

    public int VisibleColToData(int visCol) =>
        visCol >= 0 && visCol < _activeColumnIndices.Count ? _activeColumnIndices[visCol] : -1;

    public int DataRowToVisible(int dataRow) => _visibleRowIndices.IndexOf(dataRow);

    public int DataColToVisible(int dataCol) => _activeColumnIndices.IndexOf(dataCol);

    public int RowCount => _rowCount;
    public int VisibleColCount => _visibleColCount;
    public List<int> ActiveColumnIndices => _activeColumnIndices;
    public float CurrentRowSpacing => _currentRowSpacing;
    public float CurrentColumnSpacing => _currentColumnSpacing;
    public float SheetTopY => maximumSheetY;
    public bool MeshBuilt => _meshBuilt;
    public float LockedCellSize => _fixedColSpacing;
    public int FullColumnCount => _fullColCount;
    public int FullRowCount => _fullRowCount;
}
