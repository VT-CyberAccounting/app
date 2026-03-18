using UnityEngine;

public class ComponentActivator : MonoBehaviour {
    public StateEvent<bool> state;
    public bool activeOnLoad = false;

    private void OnEnable() {
        transform.GetChild(0).gameObject.SetActive(activeOnLoad);
        state += activeStateToggle;
    }
    
    private void OnDisable() {
        state -= activeStateToggle;
    }

    void activeStateToggle(bool active) {
        transform.GetChild(0).gameObject.SetActive(active);
    }
}