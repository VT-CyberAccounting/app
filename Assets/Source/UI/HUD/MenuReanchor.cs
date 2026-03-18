using UnityEngine;

public class MenuReanchor : MonoBehaviour {
    [SerializeField] public GameObject centerEyeAnchor;
    [SerializeField] public float distance = 1f;
    private void OnEnable() {
        Transform anchorTransform = centerEyeAnchor.transform; 
        transform.rotation = anchorTransform.rotation;
        transform.position = anchorTransform.position + (anchorTransform.forward * distance);
    }
}