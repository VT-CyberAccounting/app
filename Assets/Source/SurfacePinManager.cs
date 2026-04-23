using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SurfacePinManager : MonoBehaviour
{
    public enum PinKind { Column, RowSort }

    public DataSurfaceGenerator surfaceGenerator;
    public SurfaceDataSource dataSource;
    public DataTooltipUI tooltip;

    public GameObject pinPrefab;

    public float primaryPinScale = 0.05f;
    public float secondaryPinScale = 0.035f;
    public float tertiaryPinScale = 0.025f;
    public float columnPinScale = 0.045f;

    public Color columnPinColor = new Color(1f, 0.92f, 0.35f, 1f);
    public Color primarySortColor = new Color(0f, 0.8196f, 0.902f, 1f);
    public Color secondarySortColor = new Color(0.349f, 0.882f, 0.937f, 1f);
    public Color tertiarySortColor = new Color(0.6f, 0.929f, 0.961f, 1f);

    [Range(0.1f, 1.0f)]
    public float pinAlpha = 1f;

    public float columnFrontOffset = 0.5f;
    public float rowSideOffset = 0.5f;
    public float tierSpreadFraction = 0.35f;

    public float animationSpeed = 3f;
    public float collapseSpeed = 4.5f;

    private enum AnimState { Idle, Collapsing, Rising }

    private class Pin
    {
        public GameObject go;
        public Transform tf;
        public Renderer rend;
        public MaterialPropertyBlock props;
        public SurfacePinRef pinRef;
        public Vector3 currentLocal;
        public Vector3 targetLocal;
        public float currentScale;
        public float targetScale;
        public float baseScale;
    }

    private readonly List<Pin> _active = new List<Pin>();
    private readonly Stack<Pin> _pool = new Stack<Pin>();
    private AnimState _animState = AnimState.Idle;
    private bool _hooked;

    private const float POSITION_EPSILON = 0.0005f;
    private const float SCALE_EPSILON = 0.001f;

    private SurfaceDataSource ResolveSource()
    {
        if (dataSource != null) return dataSource;
        return CSVDataSource.Instance;
    }

    private void OnEnable()
    {
        StartCoroutine(HookWhenReady());
    }

    private IEnumerator HookWhenReady()
    {
        while (surfaceGenerator == null)
            yield return null;

        surfaceGenerator.OnSurfaceLayoutChanged += OnLayoutChanged;
        surfaceGenerator.OnSurfaceCollapseStarted += OnCollapseStarted;
        _hooked = true;

        if (surfaceGenerator.MeshBuilt)
            RebuildPins(true);
    }

    private void OnDisable()
    {
        if (_hooked && surfaceGenerator != null)
        {
            surfaceGenerator.OnSurfaceLayoutChanged -= OnLayoutChanged;
            surfaceGenerator.OnSurfaceCollapseStarted -= OnCollapseStarted;
        }
        _hooked = false;
        ReleaseAll();
        _animState = AnimState.Idle;
    }

    private void OnLayoutChanged()
    {
        RebuildPins(true);
    }

    private void OnCollapseStarted()
    {
        for (int i = 0; i < _active.Count; i++)
        {
            Pin p = _active[i];
            p.targetLocal = new Vector3(p.currentLocal.x, 0f, p.currentLocal.z);
            p.targetScale = 0f;
        }
        _animState = AnimState.Collapsing;
    }

    private void Update()
    {
        if (_animState == AnimState.Idle || _active.Count == 0) return;

        float speed = _animState == AnimState.Collapsing ? collapseSpeed : animationSpeed;
        float step = speed * Time.deltaTime;
        bool anyMoving = false;

        for (int i = 0; i < _active.Count; i++)
        {
            Pin p = _active[i];
            bool moved = false;

            if (Vector3.Distance(p.currentLocal, p.targetLocal) > POSITION_EPSILON)
            {
                p.currentLocal = Vector3.MoveTowards(p.currentLocal, p.targetLocal, step);
                moved = true;
            }
            else
            {
                p.currentLocal = p.targetLocal;
            }

            if (Mathf.Abs(p.currentScale - p.targetScale) > SCALE_EPSILON)
            {
                p.currentScale = Mathf.MoveTowards(p.currentScale, p.targetScale, step * 2f);
                moved = true;
            }
            else
            {
                p.currentScale = p.targetScale;
            }

            if (moved)
            {
                ApplyTransform(p);
                anyMoving = true;
            }
        }

        if (!anyMoving)
        {
            if (_animState == AnimState.Collapsing)
                ReleaseCollapsed();
            _animState = AnimState.Idle;
        }
    }

    private void RebuildPins(bool animate)
    {
        ReleaseAll();

        SurfaceDataSource src = ResolveSource();
        if (src == null || !src.IsLoaded) return;
        if (surfaceGenerator == null || !surfaceGenerator.MeshBuilt) return;

        int rowCount = surfaceGenerator.RowCount;
        int visibleCols = surfaceGenerator.VisibleColCount;
        if (rowCount < 2 || visibleCols < 2) return;

        float rowSpacing = surfaceGenerator.CurrentRowSpacing;
        float colSpacing = surfaceGenerator.CurrentColumnSpacing;
        float topY = surfaceGenerator.SurfaceTopY;

        BuildColumnPins(src, visibleCols, colSpacing, rowSpacing, topY, animate);
        BuildRowSortPins(src, rowCount, colSpacing, rowSpacing, topY, animate);

        _animState = animate && _active.Count > 0 ? AnimState.Rising : AnimState.Idle;
        if (!animate)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                Pin p = _active[i];
                p.currentLocal = p.targetLocal;
                p.currentScale = p.targetScale;
                ApplyTransform(p);
            }
        }
    }

    private void BuildColumnPins(SurfaceDataSource src, int visibleCols, float colSpacing, float rowSpacing, float topY, bool animate)
    {
        float frontZ = -rowSpacing * columnFrontOffset;
        List<int> activeCols = surfaceGenerator.ActiveColumnIndices;

        for (int vc = 0; vc < visibleCols; vc++)
        {
            int dataCol = activeCols[vc];
            string columnName = dataCol < src.NumericColumnNames.Count ? src.NumericColumnNames[dataCol] : "";

            Pin pin = AcquirePin();
            pin.baseScale = columnPinScale;
            pin.targetLocal = new Vector3(vc * colSpacing, topY, frontZ);
            pin.targetScale = 1f;
            pin.currentLocal = animate ? new Vector3(pin.targetLocal.x, 0f, pin.targetLocal.z) : pin.targetLocal;
            pin.currentScale = animate ? 0f : 1f;

            pin.pinRef.kind = PinKind.Column;
            pin.pinRef.tier = 0;
            pin.pinRef.rowIndex = -1;
            pin.pinRef.visibleColIndex = vc;
            pin.pinRef.dataColIndex = dataCol;
            pin.pinRef.sortField = null;
            pin.pinRef.sectionKey = columnName;
            pin.pinRef.sectionDisplayValue = ColumnDisplayNames.GetDisplayName(columnName);

            ApplyColor(pin, columnPinColor);
            ApplyTransform(pin);
            _active.Add(pin);
        }
    }

    private void BuildRowSortPins(SurfaceDataSource src, int rowCount, float colSpacing, float rowSpacing, float topY, bool animate)
    {
        int depth = src.SortDepth;
        if (depth <= 0) return;

        float baseX = -colSpacing * rowSideOffset;
        float tierStep = colSpacing * tierSpreadFraction;

        for (int tier = 0; tier < depth; tier++)
        {
            string field = src.GetSortFieldAt(tier);
            if (string.IsNullOrEmpty(field)) continue;

            float tierX = baseX - tier * tierStep;
            float tierScale = ScaleForTier(tier);
            Color tierColor = ColorForTier(tier);
            string prevKey = null;

            for (int r = 0; r < rowCount; r++)
            {
                string key = src.GetRowSortKey(r, field);
                bool isBoundary = r == 0 || !string.Equals(key, prevKey);
                prevKey = key;
                if (!isBoundary) continue;

                Pin pin = AcquirePin();
                pin.baseScale = tierScale;
                pin.targetLocal = new Vector3(tierX, topY, r * rowSpacing);
                pin.targetScale = 1f;
                pin.currentLocal = animate ? new Vector3(pin.targetLocal.x, 0f, pin.targetLocal.z) : pin.targetLocal;
                pin.currentScale = animate ? 0f : 1f;

                pin.pinRef.kind = PinKind.RowSort;
                pin.pinRef.tier = tier;
                pin.pinRef.rowIndex = r;
                pin.pinRef.visibleColIndex = -1;
                pin.pinRef.dataColIndex = -1;
                pin.pinRef.sortField = field;
                pin.pinRef.sectionKey = key ?? "";
                pin.pinRef.sectionDisplayValue = src.GetSortSectionDisplayValue(field, key);

                ApplyColor(pin, tierColor);
                ApplyTransform(pin);
                _active.Add(pin);
            }
        }
    }

    private float ScaleForTier(int tier)
    {
        if (tier <= 0) return primaryPinScale;
        if (tier == 1) return secondaryPinScale;
        return tertiaryPinScale;
    }

    private Color ColorForTier(int tier)
    {
        if (tier <= 0) return primarySortColor;
        if (tier == 1) return secondarySortColor;
        return tertiarySortColor;
    }

    private Pin AcquirePin()
    {
        Pin pin = _pool.Count > 0 ? _pool.Pop() : CreatePin();
        pin.go.SetActive(true);
        return pin;
    }

    private Pin CreatePin()
    {
        GameObject go;
        if (pinPrefab != null)
        {
            go = Instantiate(pinPrefab);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SurfacePin";
        }

        Transform parent = surfaceGenerator != null ? surfaceGenerator.transform : transform;
        go.transform.SetParent(parent, false);

        SurfacePinRef pinRef = go.GetComponent<SurfacePinRef>();
        if (pinRef == null) pinRef = go.AddComponent<SurfacePinRef>();

        Collider existing = go.GetComponent<Collider>();
        if (existing == null)
        {
            SphereCollider sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = false;
        }

        Renderer rend = go.GetComponentInChildren<Renderer>();

        SectionPinInspector inspector = go.GetComponent<SectionPinInspector>();
        if (inspector != null)
            inspector.Configure(tooltip, ResolveSource());

        return new Pin
        {
            go = go,
            tf = go.transform,
            rend = rend,
            props = new MaterialPropertyBlock(),
            pinRef = pinRef
        };
    }

    private void ApplyColor(Pin pin, Color color)
    {
        color.a = pinAlpha;
        if (pin.pinRef != null) pin.pinRef.color = color;
        if (pin.rend == null) return;
        pin.rend.GetPropertyBlock(pin.props);
        pin.props.SetColor("_Color", color);
        pin.props.SetColor("_BaseColor", color);
        pin.rend.SetPropertyBlock(pin.props);
    }

    private void ApplyTransform(Pin pin)
    {
        pin.tf.localPosition = pin.currentLocal;
        float s = pin.baseScale * pin.currentScale;
        pin.tf.localScale = new Vector3(s, s, s);
    }

    private void ReleaseAll()
    {
        for (int i = 0; i < _active.Count; i++)
        {
            Pin p = _active[i];
            p.go.SetActive(false);
            _pool.Push(p);
        }
        _active.Clear();
    }

    private void ReleaseCollapsed()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Pin p = _active[i];
            if (p.targetScale <= SCALE_EPSILON)
            {
                p.go.SetActive(false);
                _pool.Push(p);
                _active.RemoveAt(i);
            }
        }
    }
}
