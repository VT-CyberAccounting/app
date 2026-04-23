using System.Collections;
using UnityEngine;

public class AnchorRightOfHead : MonoBehaviour
{
    [SerializeField] Transform head;
    [SerializeField] float distance = 1.0f;
    [SerializeField] float maxWaitSeconds = 2.0f;

    void Start()
    {
        StartCoroutine(AnchorWhenReady());
    }

    IEnumerator AnchorWhenReady()
    {
        var h = head;
        if (h == null)
        {
            var go = GameObject.Find("CenterEyeAnchor");
            if (go != null) h = go.transform;
        }
        if (h == null)
        {
            Debug.LogWarning("[AnchorRightOfHead] No head transform found.");
            yield break;
        }

        float elapsed = 0f;
        while (h.position.sqrMagnitude < 0.01f && elapsed < maxWaitSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        var rightFlat = Vector3.ProjectOnPlane(h.right, Vector3.up).normalized;
        if (rightFlat.sqrMagnitude < 1e-6f) rightFlat = Vector3.right;

        var pos = h.position + rightFlat * distance;
        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - h.position, Vector3.up));
    }
}
