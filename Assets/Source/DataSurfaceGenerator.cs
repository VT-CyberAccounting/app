using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DataSurfaceGenerator : MonoBehaviour
{
    #region Inspector Fields

    [Header("Bounding Box")]
    public float maxSurfaceWidth = 1.5f;
    public float maxSurfaceDepth = 1.0f;
    public float maxSurfaceHeight = 0.5f;

    [Header("Appearance")]
    [Range(0.1f, 1.0f)]
    public float surfaceAlpha = 0.6f;

    [Header("Animation")]
    public float animationSpeed = 3.0f;
    public bool skipIntroAnimation = false;

    #endregion

    #region Private Fields

    private Mesh _mesh;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;

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
    private List<int> _activeColumnIndices = new List<int>();
    private bool _isAnimating;
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

    #endregion

    #region Lifecycle

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
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
        if (!_isAnimating || !_meshBuilt) return;

        bool stillAnimating = false;
        float step = animationSpeed * Time.deltaTime;

        for (int i = 0; i < _activeVertexCount; i++)
        {
            if (Vector3.Distance(_vertices[i], _targetVertices[i]) > CONVERGENCE_THRESHOLD)
            {
                _vertices[i] = Vector3.MoveTowards(_vertices[i], _targetVertices[i], step);
                stillAnimating = true;
            }
            else
            {
                _vertices[i] = _targetVertices[i];
            }

            if (ColorDistance(_colors[i], _targetColors[i]) > CONVERGENCE_THRESHOLD)
            {
                _colors[i] = Color.Lerp(_colors[i], _targetColors[i], step * 2f);
                stillAnimating = true;
            }
            else
            {
                _colors[i] = _targetColors[i];
            }
        }

        _mesh.vertices = _vertices;
        _mesh.colors = _colors;

        if (!stillAnimating)
        {
            _isAnimating = false;
            _mesh.RecalculateNormals();
        }
    }

    #endregion

    #region Array Allocation

    private void AllocateArrays()
    {
        CSVDataManager data = CSVDataManager.Instance;
        _maxVertexCount = data.AllRows.Count * data.NumericColumnNames.Count;

        _vertices = new Vector3[_maxVertexCount];
        _targetVertices = new Vector3[_maxVertexCount];
        _colors = new Color[_maxVertexCount];
        _targetColors = new Color[_maxVertexCount];

        int maxQuads = (data.AllRows.Count - 1) * (data.NumericColumnNames.Count - 1);
        _triangles = new int[maxQuads * 6];

        _arraysAllocated = true;
    }

    #endregion

    #region Surface Construction

    private void OnDataLoaded()
    {
        AllocateArrays();
        BuildSurface(false);
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

        _currentColumnSpacing = maxSurfaceWidth / Mathf.Max(_visibleColCount - 1, 1);
        _currentRowSpacing = maxSurfaceDepth / Mathf.Max(_rowCount - 1, 1);

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

                if (blendFromCurrent && _meshBuilt && idx < _activeVertexCount)
                {
                    continue;
                }
                else if (skipIntroAnimation)
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
        _isAnimating = !skipIntroAnimation || blendFromCurrent;
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

    #endregion

    #region Filter Response

    private void OnFilterChanged()
    {
        BuildSurface(true);
    }

    public void OnColumnVisibilityChanged()
    {
        BuildSurface(true);
    }

    #endregion

    #region Active Columns

    private List<int> GetActiveColumnIndices()
    {
        var indices = new List<int>();
        SurfaceFilterController filter = SurfaceFilterController.Instance;
        int totalCols = CSVDataManager.Instance.NumericColumnNames.Count;

        if (filter == null)
        {
            for (int i = 0; i < totalCols; i++)
                indices.Add(i);
            return indices;
        }

        for (int i = 0; i < totalCols; i++)
        {
            if (filter.IsColumnVisible(i))
                indices.Add(i);
        }

        return indices;
    }

    #endregion

    #region Heatmap Sampling

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

    #endregion

    #region Public Queries

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

    #endregion
}