using UnityEngine;

public class PoseBoolStateLink : MonoBehaviour {
    public BoolStateEvent poseEvent;
    public void raisePoseEvent() {
        Debug.Log("PoseBoolStateLink raisePoseEvent");
        if (poseEvent == null) {
            Debug.Log("PoseBoolStateLink poseEvent is null");
        } 
        poseEvent.Set(!poseEvent.Get());
        Debug.Log($"{poseEvent.name} set to {poseEvent.Get()}");
    }
}