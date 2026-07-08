using Meta.XR.MRUtilityKit;
using UnityEngine;

public class QrScanner : MonoBehaviour
{
    public DatasetManager datasetManager;

    void Start()
    {
        if (datasetManager == null) datasetManager = FindAnyObjectByType<DatasetManager>();

        if (MRUK.Instance != null)
            MRUK.Instance.SceneSettings.TrackableAdded.AddListener(OnTrackableAdded);

        if (DataRequest.Has) AddPayload(DataRequest.Consume());
    }

    void OnDestroy()
    {
        if (MRUK.Instance != null)
            MRUK.Instance.SceneSettings.TrackableAdded.RemoveListener(OnTrackableAdded);
    }

    void OnTrackableAdded(MRUKTrackable trackable)
    {
        if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode) return;
        AddPayload(trackable.MarkerPayloadString);
    }

    void AddPayload(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return;
        if (datasetManager != null) datasetManager.AddTab(payload);
    }
}
