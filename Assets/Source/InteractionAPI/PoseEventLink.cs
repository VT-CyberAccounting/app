using UnityEngine;

public class PoseEventLink : MonoBehaviour {
    public SignalEvent poseEvent;
    public void raisePoseEvent() {
        Debug.Log($"AppEvent with name '{poseEvent.name}' raised");
        poseEvent.Raise();
    }
}
