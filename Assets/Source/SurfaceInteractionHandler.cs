using UnityEngine;
using Oculus.Interaction;

public class SurfaceInteractionHandler : MonoBehaviour
{
    #region Inspector Fields

    [Header("Resize Settings")]
    public float minScale = 0.2f;
    public float maxScale = 3.0f;
    public float resizeSpeed = 1.0f;

    [Header("References")]
    public Grabbable grabbable;

    #endregion

    #region Private Fields

    private float _initialGrabDistance;
    private Vector3 _initialScale;
    private bool _isTwoHandGrab;

    #endregion

    #region Lifecycle

    private void Start()
    {
        if (grabbable == null)
            grabbable = GetComponent<Grabbable>();
    }

    #endregion

    #region Update

    private void Update()
    {
        if (grabbable == null) return;

        int selectCount = grabbable.SelectingPointsCount;

        if (selectCount >= 2 && !_isTwoHandGrab)
        {
            BeginTwoHandResize();
        }
        else if (selectCount < 2 && _isTwoHandGrab)
        {
            _isTwoHandGrab = false;
        }

        if (!_isTwoHandGrab) return;

        UpdateResize();
    }

    #endregion

    #region Resize Logic

    private void BeginTwoHandResize()
    {
        var points = grabbable.GrabPoints;
        if (points.Count < 2) return;

        _initialGrabDistance = Vector3.Distance(
            points[0].position,
            points[1].position
        );
        _initialScale = transform.localScale;
        _isTwoHandGrab = true;
    }

    private void UpdateResize()
    {
        var points = grabbable.GrabPoints;
        if (points.Count < 2)
        {
            _isTwoHandGrab = false;
            return;
        }

        float currentDistance = Vector3.Distance(
            points[0].position,
            points[1].position
        );

        if (_initialGrabDistance < 0.001f) return;

        float scaleFactor = currentDistance / _initialGrabDistance;
        Vector3 targetScale = _initialScale * scaleFactor;

        float clampedUniform = Mathf.Clamp(targetScale.x, minScale, maxScale);
        targetScale = Vector3.one * clampedUniform;

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            Time.deltaTime * resizeSpeed * 10f
        );
    }

    #endregion
}