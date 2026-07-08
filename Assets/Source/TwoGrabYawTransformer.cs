using UnityEngine;
using Oculus.Interaction;

public class TwoGrabYawTransformer : MonoBehaviour, ITransformer
{
    private IGrabbable _grabbable;
    private SheetPiece _piece;

    private float _prevAngle;
    private bool _hasAngle;
    private Quaternion _targetRotation;
    private Quaternion _lastValidRotation;
    private Vector3 _pivot;

    public void Initialize(IGrabbable grabbable)
    {
        _grabbable = grabbable;
        _piece = GetComponent<SheetPiece>();
    }

    public void BeginTransform()
    {
        Transform target = _grabbable.Transform;
        _targetRotation = target.rotation;
        _lastValidRotation = target.rotation;
        _pivot = target.position;
        _hasAngle = TryGetAngle(out _prevAngle);
    }

    public void UpdateTransform()
    {
        Transform target = _grabbable.Transform;

        if (TryGetAngle(out float angle))
        {
            if (_hasAngle)
            {
                float delta = Mathf.DeltaAngle(_prevAngle, angle);
                _targetRotation = Quaternion.AngleAxis(delta, Vector3.up) * _targetRotation;
            }
            _prevAngle = angle;
            _hasAngle = true;
        }

        if (IsBlocked(target, _targetRotation)) _targetRotation = _lastValidRotation;
        else _lastValidRotation = _targetRotation;

        target.rotation = _targetRotation;
        target.position = _pivot;
    }

    public void EndTransform() { }

    private bool IsBlocked(Transform target, Quaternion worldRotation)
    {
        SheetManager manager = _piece != null ? _piece.Manager : null;
        if (manager == null) return false;
        Transform parent = target.parent;
        Quaternion local = parent != null
            ? Quaternion.Inverse(parent.rotation) * worldRotation
            : worldRotation;
        return manager.RotationCausesOverlap(_piece, local);
    }

    private bool TryGetAngle(out float angle)
    {
        Vector3 a = _grabbable.GrabPoints[0].position;
        Vector3 b = _grabbable.GrabPoints[1].position;
        Vector3 v = b - a;
        v.y = 0f;
        if (v.sqrMagnitude < 1e-6f)
        {
            angle = 0f;
            return false;
        }
        angle = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
        return true;
    }
}
