using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DataSurfaceGenerator : MonoBehaviour
{
    public float maxSurfaceWidth = 3f;
    public float maxSurfaceDepth = 5f;
    public float maxSurfaceHeight = 0.5f;

    [Range(0.1f, 1.0f)]
    public float surfaceAlpha = 0.6f;

    public float animationSpeed = 3.0f;
    public float collapseSpeed = 4.5f;
    public bool skipIntroAnimation = false;

    private enum SurfaceState { Idle, Collapsing, Rising }

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

    private static readonly Color[] _heatmapStops = {
        new Color(0.0f, 0.0f, 1.0f),
        new Color(0.0f, 1.0f, 1.0f),
        new Color(0.0f, 1.0f, 0.0f),
        new Color(1.0f, 1.0f, 0.0f),
        new Color(1.0f, 0.0f, 0.0f)
    };

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
        while (CSVDataManager.Instance == null)
            yield return null;

        if (CSVDataManager.Instance.IsLoaded)
            OnDataLoaded();
        else
            CSVDataManager.Instance.OnDataLoaded += OnDataLoaded;

        CSVDataManager.Instance.OnFilterChanged += OnFilterChanged;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (CSVDataManager.Instance != null)
        {
            CSVDataManager.Instance.OnDataLoaded -= OnDataLoaded;
            CSVDataManager.Instance.OnFilterChanged -= OnFilterChanged;
        }
    }

    private void Update()
    {
        if (_state == SurfaceState.Idle || !_meshBuilt) return;

        bool stillMoving = false;
        float speed = (_state == SurfaceState.Collapsing) ? collapseSpeed : animationSpeed;
        float step = speed * Time.deltaTime;

        for (int i = 0; i < _activeVertexCount; i++)
        {
            if (Vector3.Distance(_vertices[i], _targetVertices[i]) > CONVERGENCE_THRESHOLD)
            {
                _vertices[i] = Vector3.MoveTowards(_vertices[i], _targetVertices[i], step);
                stillMoving = true;
            }
            else
            {
                _vertices[i] = _targetVertices[i];
            }

            if (ColorDistance(_colors[i], _targetColors[i]) > CONVERGENCE_THRESHOLD)
            {
                _colors[i] = Color.Lerp(_colors[i], _targetColors[i], step * 2f);
                stillMoving = true;
            }
            else
            {
                _colors[i] = _targetColors[i];
            }
        }

        _mesh.vertices = _vertices;
        _mesh.colors = _colors;

        if (!stillMoving)
        {
            if (_state == SurfaceState.Collapsing)
            {
                _pendingRebuild = false;
                BuildSurface(false);
                _state = SurfaceState.Rising;
            }
            else
            {
                _state = SurfaceState.Idle;
                _mesh.RecalculateNormals();
                RefreshCollider();
            }
        }
    }

    private void AllocateArrays()
    {
        CSVDataManager data = CSVDataManager.Instance;
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
    }

    public void BuildSurface(bool blendFromCurrent)
    {
        CSVDataManager data = CSVDataManager.Instance;
        if (data == null || !_arraysAllocated)
        {
            _mesh.Clear();
            _meshBuilt = false;
            return;
        }

        if (data.FilteredRows.Count == 0)
        {
            _mesh.Clear();
            _meshBuilt = false;
            return;
        }

        _activeColumnIndices = GetActiveColumnIndices();
        _rowCount = data.FilteredRows.Count;
        _visibleColCount = _activeColumnIndices.Count;

        if (_visibleColCount < 2 || _rowCount < 2)
        {
            _mesh.Clear();
            _meshBuilt = false;
            return;
        }

        _currentColumnSpacing = _fixedColSpacing;
        _currentRowSpacing = _fixedRowSpacing;

        int newVertexCount = _rowCount * _visibleColCount;

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

                Color color = SampleHeatmap(normalized);
                color.a = surfaceAlpha;

                _targetVertices[idx] = new Vector3(x, y, z);
                _targetColors[idx] = color;

                if (skipIntroAnimation && !blendFromCurrent)
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
        _mesh.vertices = _vertices;
        _mesh.triangles = _triangles;
        _mesh.colors = _colors;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        _meshBuilt = true;
        RefreshCollider();
    }

    private void RefreshCollider()
    {
        if (_meshCollider != null)
        {
            _meshCollider.sharedMesh = null;
            _meshCollider.sharedMesh = _mesh;
        }
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

    private List<int> GetActiveColumnIndices()
    {
        var indices = new List<int>();
        CSVDataManager data = CSVDataManager.Instance;
        int totalCols = data.NumericColumnNames.Count;

        for (int i = 0; i < totalCols; i++)
        {
            if (data.IsColumnActive(i))
                indices.Add(i);
        }

        return indices;
    }

    private static Color SampleHeatmap(float t)
    {
        t = Mathf.Clamp01(t);

        float scaledT = t * (_heatmapStops.Length - 1);
        int lower = Mathf.FloorToInt(scaledT);
        int upper = Mathf.Min(lower + 1, _heatmapStops.Length - 1);
        float frac = scaledT - lower;

        return Color.Lerp(_heatmapStops[lower], _heatmapStops[upper], frac);
    }

    private static float ColorDistance(Color a, Color b)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        float da = a.a - b.a;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db + da * da);
    }

    public (int row, int col) GetNearestCell(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);

        int visCol = Mathf.Clamp(Mathf.RoundToInt(local.x / _currentColumnSpacing), 0, _visibleColCount - 1);
        int row = Mathf.Clamp(Mathf.RoundToInt(local.z / _currentRowSpacing), 0, _rowCount - 1);

        int dataCol = (visCol < _activeColumnIndices.Count) ? _activeColumnIndices[visCol] : 0;

        return (row, dataCol);
    }

    public int RowCount => _rowCount;
    public int VisibleColCount => _visibleColCount;
    public List<int> ActiveColumnIndices => _activeColumnIndices;
}