using UnityEngine;

public class PanelGrab : MonoBehaviour
{
    [SerializeField] private Collider grabCollider;

    public void SetGrabbable(bool on)
    {
        if (grabCollider != null) grabCollider.enabled = on;
    }
}
