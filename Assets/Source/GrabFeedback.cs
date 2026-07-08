using UnityEngine;
using Oculus.Interaction;

public class GrabFeedback : MonoBehaviour
{
    private const float PopScale = 1.03f;
    private const float PopDuration = 0.08f;

    [SerializeField] private Grabbable _grabbable;

    private Transform _target;
    private Vector3 _baseScale = Vector3.one;
    private int _grabCount;
    private bool _grabbed;
    private float _popT;

    private void Awake()
    {
        if (_grabbable == null) _grabbable = GetComponent<Grabbable>();
        _target = transform;
        _baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        if (_grabbable != null) _grabbable.WhenPointerEventRaised -= OnPointerEvent;
        _grabCount = 0;
        _grabbed = false;
        _popT = 0f;
        if (_target != null) _target.localScale = _baseScale;
    }

    private void Subscribe()
    {
        if (_grabbable == null) return;
        _grabbable.WhenPointerEventRaised -= OnPointerEvent;
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Select:
                _grabCount++;
                if (_grabCount == 1) _grabbed = true;
                break;
            case PointerEventType.Unselect:
            case PointerEventType.Cancel:
                _grabCount = Mathf.Max(0, _grabCount - 1);
                if (_grabCount == 0) _grabbed = false;
                break;
        }
    }

    private void Update()
    {
        float goal = _grabbed ? 1f : 0f;
        if (Mathf.Approximately(_popT, goal)) return;

        if (_grabbed && _popT == 0f) _baseScale = _target.localScale;
        _popT = Mathf.MoveTowards(_popT, goal, Time.unscaledDeltaTime / PopDuration);
        if (_target != null)
            _target.localScale = _baseScale * Mathf.Lerp(1f, PopScale, _popT);
    }
}
