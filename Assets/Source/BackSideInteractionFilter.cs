using UnityEngine;
using Oculus.Interaction;

public class BackSideInteractionFilter : MonoBehaviour, IGameObjectFilter
{
    [SerializeField] private Transform _panel;
    [SerializeField] private float _tolerance = 0.02f;
    [SerializeField] private bool _invertFacing;

    private void Awake()
    {
        if (_panel == null) _panel = transform;
    }

    public bool Filter(GameObject go)
    {
        if (go == null) return true;
        Transform plane = _panel != null ? _panel : transform;

        float side = Vector3.Dot(go.transform.position - plane.position, plane.forward);
        if (_invertFacing) side = -side;
        return side >= -_tolerance;
    }
}
