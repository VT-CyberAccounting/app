using Meta.XR.MRUtilityKit;
using UnityEngine;
using UnityEngine.SceneManagement;

public class QrGateLoader : MonoBehaviour
{
    [SerializeField] string nextScene = "Main";
    [SerializeField] string expectedPayload = "";
    bool loaded;

    public static string LastPayload;

    void Start()
    {
        MRUK.Instance.SceneSettings.TrackableAdded.AddListener(OnTrackableAdded);
    }

    void OnDestroy()
    {
        if (MRUK.Instance != null)
            MRUK.Instance.SceneSettings.TrackableAdded.RemoveListener(OnTrackableAdded);
    }

    void OnTrackableAdded(MRUKTrackable trackable)
    {
        if (loaded) return;
        if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode) return;

        var payload = trackable.MarkerPayloadString;
        if (string.IsNullOrEmpty(payload)) return;
        if (!string.IsNullOrEmpty(expectedPayload) && payload != expectedPayload) return;

        LastPayload = payload;
        loaded = true;
        Debug.Log($"[QrGateLoader] QR payload '{payload}' → loading {nextScene}");
        SceneManager.LoadScene(nextScene, LoadSceneMode.Single);
    }
}
