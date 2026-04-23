using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DataSurfaceGenerator : MonoBehaviour
{
    public event Action OnSurfaceLayoutChanged;
    public event Action OnSurfaceCollapseStarted;

    public SurfaceDataSource dataSource;

    public float maxSurfaceWidth = 3f;
    public float maxSurfaceDepth = 5f;
    public float maxSurfaceHeight = 0.5f;

    [Range(0.1f, 1.0f)]
    public float surfaceAlpha = 0.6f;

    public float animationSpeed = 3.0f;
    public float collapseSpeed = 4.5f;
    public bool skipIntroAnimation = false;

    private SurfaceDataSource ResolveSource()
    {
        if (dataSource != null) return dataSource;
        return CSVDataSource.Instance;
    }

    private enum SurfaceState { Idle, Collapsing, Rising }

    private bool _hideOnCollapseComplete;

    private Mesh _mesh;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private MeshCollider _meshCollider;

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
    private float _currentColumnSpacing;
    private float _currentRowSpacing;
    private float _fixedColSpacing;
    private float _fixedRowSpacing;
    private List<int> _activeColumnIndices = new List<int>();
    private SurfaceState _state = SurfaceState.Idle;
    private bool _pendingRebuild;
    private bool _meshBuilt;
    private bool _arraysAllocated;

    private const float CONVERGENCE_THRESHOLD = 0.001f;
    private const float CONVERGENCE_THRESHOLD_SQ = CONVERGENCE_THRESHOLD * CONVERGENCE_THRESHOLD;

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _meshCollider = GetComponent<MeshCollider>();
        _mesh = new Mesh { name = "DataSurface" };
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _meshFilter.mesh = _mesh;
    }

    private void OnEnable()
    {
        StartCoroutine(WaitForDataManager());
    }

    private System.Collections.IEnumerator WaitForDataManager()
    {
        SurfaceDataSource source = ResolveSource();
        while (source == null)
        {
            yield return null;
            source = ResolveSource();
        }

        if (source.IsLoaded)
            OnDataLoaded();
        else
            source.OnDataLoaded += OnDataLoaded;

        source.OnFilterChanged += OnFilterChanged;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        SurfaceDataSource source = ResolveSource();
        if (source != null)
        {
            source.OnDataLoaded -= OnDataLoaded;
            source.OnFilterChanged -= OnFilterChanged;
        }
    }

    private void Update()
    {
        if (_state == SurfaceState.Idle || !_meshBuilt) return;

        bool verticesMoving = false;
        bool colorsMoving = false;
        float speed = (_state == SurfaceState.Collapsing) ? collapseSpeed : animationSpeed;
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
        if (colorsMoving || verticesMoving) UploadColors();

        bool stillMoving = verticesMoving || colorsMoving;

        if (!stillMoving)
        {
            if (_state == SurfaceState.Collapsing)
            {
                if (_hideOnCollapseComplete)
                {
                    _hideOnCollapseComplete = false;
                    _pendingRebuild = false;
                    _state = SurfaceState.Idle;
                    gameObject.SetActive(false);
                    return;
                }
                _pendingRebuild = false;
                BuildSurface(false);
                _state = SurfaceState.Rising;
            }
            else
            {
                _state = SurfaceState.Idle;
                _mesh.RecalculateNormals();
                _mesh.RecalculateBounds();
                RefreshCollider();
            }
        }
    }

    private void UploadVertices()
    {
        _mesh.SetVertices(_vertices, 0, _activeVertexCount);
    }

    private void UploadColors()
    {
        _mesh.SetColors(_colors, 0, _activeVertexCount);
    }

    private void AllocateArrays()
    {
        SurfaceDataSource data = ResolveSource();
        int totalRows = data.AllRows.Count;
        int totalCols = data.NumericColumnNames.Count;

        _maxVertexCount = totalRows * totalCols;

        _vertices = new Vector3[_maxVertexCount];
        _targetVertices = new Vector3[_maxVertexCount];
        _colors = new Color[_maxVertexCount];
        _targetColors = new Color[_maxVertexCount];

        int maxQuads = (totalRows - 1) * (totalCols - 1);
        _triangles = new int[maxQuads * 6];

        _fixedColSpacing = maxSurfaceWidth / Mathf.Max(totalCols - 1, 1);
        _fixedRowSpacing = maxSurfaceDepth / Mathf.Max(totalRows - 1, 1);

        _arraysAllocated = true;
    }

    private void OnDataLoaded()
    {
        AllocateArrays();
        BuildSurface(false);
        _state = skipIntroAnimation ? SurfaceState.Idle : SurfaceState.Rising;
    }

    private void OnFilterChanged()
    {
        if (!_meshBuilt)
        {
            BuildSurface(false);
            _state = skipIntroAnimation ? SurfaceState.Idle : SurfaceState.Rising;
            return;
        }

        if (_state == SurfaceState.Collapsing)
        {
            _pendingRebuild = true;
            return;
        }

        StartCollapse();
    }

    public void CollapseAndDeactivate()
    {
        if (!gameObject.activeSelf) return;

        if (!_meshBuilt || _activeVertexCount == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        _hideOnCollapseComplete = true;
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

        _state = SurfaceState.Collapsing;
        _pendingRebuild = false;
        OnSurfaceCollapseStarted?.Invoke();
    }

    public void BuildSurface(bool blendFromCurrent)
    {
        SurfaceDataSource data = ResolveSource();
        if (data == null || !_arraysAllocated)
        {
            ClearSurface();
            return;
        }

        if (data.FilteredRows.Count == 0)
        {
            ClearSurface();
            return;
        }

        RefreshActiveColumnIndices();
        _rowCount = data.FilteredRows.Count;
        _visibleColCount = _activeColumnIndices.Count;

        if (_visibleColCount < 2 || _rowCount < 2)
        {
            ClearSurface();
            return;
        }

        _currentColumnSpacing = _fixedColSpacing;
        _currentRowSpacing = _fixedRowSpacing;

        int newVertexCount = _rowCount * _visibleColCount;
        bool willAnimate = !(skipIntroAnimation && !blendFromCurrent);

        for (int r = 0; r < _rowCount; r++)
        {
            for (int vc = 0; vc < _visibleColCount; vc++)
            {
                int dataColIndex = _activeColumnIndices[vc];
                int idx = r * _visibleColCount + vc;
                float normalized = data.GetNormalizedValue(r, dataColIndex);

                float x = vc * _currentColumnSpacing;
                float z = r * _currentRowSpacing;
                float y = normalized * maxSurfaceHeight;

                Color color = Heatmap.Sample(normalized);
                color.a = surfaceAlpha;

                _targetVertices[idx] = new Vector3(x, y, z);
                _targetColors[idx] = color;

                if (!willAnimate)
                {
                    _vertices[idx] = new Vector3(x, y, z);
                    _colors[idx] = color;
                }
                else
                {
                    _vertices[idx] = new Vector3(x, 0f, z);
                    _colors[idx] = new Color(color.r, color.g, color.b, 0f);
                }
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
        _mesh.SetVertices(_vertices, 0, _activeVertexCount);
        _mesh.SetTriangles(_triangles, 0, _activeTriangleCount, 0);
        _mesh.SetColors(_colors, 0, _activeVertexCount);

        if (willAnimate)
        {
            _mesh.RecalculateBounds();
        }
        else
        {
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            RefreshCollider();
        }

        _meshBuilt = true;
        OnSurfaceLayoutChanged?.Invoke();
    }

    private void ClearSurface()
    {
        _mesh.Clear();
        _meshBuilt = false;
        _rowCount = 0;
        _visibleColCount = 0;
        _activeVertexCount = 0;
        OnSurfaceLayoutChanged?.Invoke();
    }

    private void RefreshCollider()
    {
        if (_meshCollider == null) return;
        _meshCollider.sharedMesh = null;
        _meshCollider.sharedMesh = _mesh;
    }

    private void BuildTriangles()
    {
        int quadCount = (_rowCount - 1) * (_visibleColCount - 1);
        _activeTriangleCount = quadCount * 6;
        int t = 0;

        for (int r = 0; r < _rowCount - 1; r++)
        {
            for (int c = 0; c < _visibleColCount - 1; c++)
            {
                int topLeft = r * _visibleColCount + c;
                int topRight = topLeft + 1;
                int bottomLeft = (r + 1) * _visibleColCount + c;
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

    private void RefreshActiveColumnIndices()
    {
        _activeColumnIndices.Clear();
        SurfaceDataSource data = ResolveSource();
        int totalCols = data.NumericColumnNames.Count;

        for (int i = 0; i < totalCols; i++)
        {
            if (data.IsColumnActive(i))
                _activeColumnIndices.Add(i);
        }
    }

    public (int row, int col) GetNearestCell(Vector3 worldPoint)
    {
        if (!_meshBuilt || _visibleColCount <= 0 || _rowCount <= 0 ||
            _activeColumnIndices.Count == 0 ||
            _currentColumnSpacing <= 0f || _currentRowSpacing <= 0f)
            return (-1, -1);

        Vector3 local = transform.InverseTransformPoint(worldPoint);

        int visCol = Mathf.Clamp(Mathf.RoundToInt(local.x / _currentColumnSpacing), 0, _visibleColCount - 1);
        int row = Mathf.Clamp(Mathf.RoundToInt(local.z / _currentRowSpacing), 0, _rowCount - 1);

        visCol = Mathf.Clamp(visCol, 0, _activeColumnIndices.Count - 1);
        int dataCol = _activeColumnIndices[visCol];

        return (row, dataCol);
    }

    public int RowCount => _rowCount;
    public int VisibleColCount => _visibleColCount;
    public List<int> ActiveColumnIndices => _activeColumnIndices;
    public float CurrentRowSpacing => _currentRowSpacing;
    public float CurrentColumnSpacing => _currentColumnSpacing;
    public float SurfaceTopY => maxSurfaceHeight;
    public bool MeshBuilt => _meshBuilt;
}