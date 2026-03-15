using UnityEngine;

public class EventListener : MonoBehaviour {
    private SignalEvent trigger;
    private void OnEnable() {
        trigger = SignalEvent.Get("PANEL");
        if (trigger != null) {
            trigger += printDebug;
        }
        else {
            Debug.Log("PANEL event unintialized");
        }
    }

    private void OnDisable() {
        if (trigger != null) {
            trigger -= printDebug;
        }
    }

    void printDebug() {
        Debug.Log("PANEL LISTENER TRIGGERED");
    }
    
}