using UnityEngine;

public class TMPHandler : MonoBehaviour {
    private TMPro.TextMeshProUGUI textMeshProUGUI;
    private SignalEvent trigger;
    private string text = "";

    private void OnEnable() {
        textMeshProUGUI = GetComponent<TMPro.TextMeshProUGUI>();
        trigger = SignalEvent.Get("PANEL");
        if (trigger != null) {
            trigger += onTrigger;
        }
    }

    private void OnDisable() {
        if (trigger != null) {
            trigger -= onTrigger;
        }
    }

    private void Start() {
        if (textMeshProUGUI == null) {
            Debug.LogError("TextMeshProUGUI reference is not set in the inspector.");
        }
    }

    private void onTrigger() {
        if (text == "") {
            text = "PANEL ON";
        }
        else {
            text = "";
        }
        textMeshProUGUI.text = text;
    }
}