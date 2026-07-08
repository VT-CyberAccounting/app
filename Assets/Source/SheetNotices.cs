using System.Collections;
using UnityEngine;

public static class SheetNotices
{
    private const float NoticeSeconds = 3f;

    private static string _pendingForAssistant;
    private static int _lastToastFrame = -1;
    private static bool _persistent;
    private static Tooltip _tooltip;

    public static string ConsumeForAssistant()
    {
        string s = _pendingForAssistant;
        _pendingForAssistant = null;
        return s;
    }

    public static void EditsDropped(MonoBehaviour host, string message) =>
        Show(host, "Edits Cleared", message);

    public static void Show(MonoBehaviour host, string title, string message)
    {
        _pendingForAssistant = message;
        _persistent = false;

        if (Time.frameCount == _lastToastFrame) return;
        _lastToastFrame = Time.frameCount;

        if (_tooltip == null) _tooltip = Object.FindAnyObjectByType<Tooltip>();
        if (_tooltip == null || host == null || !host.isActiveAndEnabled) return;

        Transform cam = CameraRig.MainTransform;
        Vector3 point = cam != null ? cam.position + cam.forward : Vector3.zero;
        _tooltip.ShowHint(point, title, message);
        host.StartCoroutine(HideAfterDelay());
    }

    public static void ShowPersistent(string title, string message)
    {
        _pendingForAssistant = message;
        _persistent = true;

        if (_tooltip == null) _tooltip = Object.FindAnyObjectByType<Tooltip>();
        if (_tooltip == null) return;

        Transform cam = CameraRig.MainTransform;
        Vector3 point = cam != null ? cam.position + cam.forward : Vector3.zero;
        _tooltip.ShowHint(point, title, message);
    }

    public static void HideNotice()
    {
        _persistent = false;
        if (_tooltip != null) _tooltip.Hide();
    }

    private static IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(NoticeSeconds);
        if (!_persistent && _tooltip != null) _tooltip.Hide();
    }
}
