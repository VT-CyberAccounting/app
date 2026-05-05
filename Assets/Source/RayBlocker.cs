using UnityEngine;
using Oculus.Interaction;

public class RayBlocker : MonoBehaviour
{
    private RayInteractable _rayInteractable;

    private void Awake()
    {
        _rayInteractable = GetComponent<RayInteractable>();
    }

    private void OnEnable()
    {
        if (_rayInteractable != null)
            _rayInteractable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDisable()
    {
        if (_rayInteractable != null)
            _rayInteractable.WhenPointerEventRaised -= OnPointerEvent;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
    }
}