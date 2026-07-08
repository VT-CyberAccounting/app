using Meta.XR.MRUtilityKit;
using UnityEngine;

public class DatabaseAuthScanner : MonoBehaviour
{
    void Start()
    {
        if (MRUK.Instance != null)
            MRUK.Instance.SceneSettings.TrackableAdded.AddListener(OnTrackableAdded);
    }

    void OnDestroy()
    {
        if (MRUK.Instance != null)
            MRUK.Instance.SceneSettings.TrackableAdded.RemoveListener(OnTrackableAdded);
        BackendAuth.Clear();
    }

    void OnTrackableAdded(MRUKTrackable trackable)
    {
        if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode) return;
        if (string.IsNullOrEmpty(trackable.MarkerPayloadString)) return;
        BackendAuth.SetToken(trackable.MarkerPayloadString);
    }
}
