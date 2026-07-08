using UnityEngine;

public class SheetFitter : MonoBehaviour
{
    [UnityEngine.Serialization.FormerlySerializedAs("regionGenerator")]
    public SheetGenerator sheetGenerator;
    public bool recenterOnFilter = true;

    private bool _hooked;
    private bool _edgeDistanceEnforced;
    private bool _cameraReady;
    private Vector3 _restLocalPosition;

    private void Awake()
    {
        if (sheetGenerator == null) sheetGenerator = GetComponent<SheetGenerator>();
        if (sheetGenerator == null) sheetGenerator = FindAnyObjectByType<SheetGenerator>();
        if (sheetGenerator != null) _restLocalPosition = sheetGenerator.transform.localPosition;
    }

    private void OnEnable()
    {
        if (sheetGenerator == null) return;
        sheetGenerator.OnSheetLayoutChanged += Apply;
        _hooked = true;
        Apply();
    }

    private void OnDisable()
    {
        if (_hooked && sheetGenerator != null)
            sheetGenerator.OnSheetLayoutChanged -= Apply;
        _hooked = false;
    }

    private void Update()
    {
        if (_cameraReady) return;
        if (CameraRig.MainTransform == null) return;

        _cameraReady = true;
        Apply();
    }

    private void Apply()
    {
        if (sheetGenerator == null || !sheetGenerator.MeshBuilt) return;

        float cellSize = sheetGenerator.LockedCellSize;
        int cols = recenterOnFilter ? sheetGenerator.VisibleColCount : sheetGenerator.FullColumnCount;
        int rows = recenterOnFilter ? sheetGenerator.RowCount : sheetGenerator.FullRowCount;

        float halfWidth = Mathf.Max(cols - 1, 0) * cellSize * 0.5f;
        float halfDepth = Mathf.Max(rows - 1, 0) * cellSize * 0.5f;

        if (!_edgeDistanceEnforced)
            EnforceMinEdgeDistance(halfDepth);

        sheetGenerator.transform.localPosition = new Vector3(
            _restLocalPosition.x - halfWidth,
            CameraCenteredLocalY(),
            _restLocalPosition.z - halfDepth);
    }

    private float CameraCenteredLocalY()
    {
        Transform cam = CameraRig.MainTransform;
        if (cam == null) return _restLocalPosition.y;

        Transform parent = sheetGenerator.transform.parent;
        float parentY = parent != null ? parent.position.y : 0f;
        return cam.position.y - sheetGenerator.SheetTopY * 0.5f - parentY;
    }

    private void EnforceMinEdgeDistance(float halfDepth)
    {
        float minDistance = sheetGenerator.minimumZOffsetFromCamera;
        if (minDistance <= 0f)
        {
            _edgeDistanceEnforced = true;
            return;
        }

        Transform cam = CameraRig.MainTransform;
        if (cam == null) return;

        _edgeDistanceEnforced = true;

        Transform parent = sheetGenerator.transform.parent;
        Vector3 camLocal = parent != null
            ? parent.InverseTransformPoint(cam.position)
            : cam.position;

        float minCenterZ = camLocal.z + minDistance + halfDepth;
        if (_restLocalPosition.z < minCenterZ)
            _restLocalPosition.z = minCenterZ;
    }
}
