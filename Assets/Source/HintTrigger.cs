using UnityEngine;
using UnityEngine.EventSystems;

public class HintTrigger : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public string title;
    public string body;

    private static Tooltip _tooltip;
    private bool _clicked;
    private bool _showing;
    private bool _selfLatchOnClick = true;
    private System.Func<bool> _externalLatched;

    private System.Func<bool> _lockOverride;
    private string _lockTitle;
    private string _lockBody;

    private bool IsLatched => _externalLatched != null ? _externalLatched() : _clicked;

    public static HintTrigger Attach(GameObject go, string title, string body)
    {
        HintTrigger trigger = go.GetComponent<HintTrigger>();
        if (trigger == null) trigger = go.AddComponent<HintTrigger>();
        trigger.title = title;
        trigger.body = body;
        return trigger;
    }

    public static HintTrigger AttachShared(GameObject go, string title, string body, System.Func<bool> latched)
    {
        HintTrigger trigger = Attach(go, title, body);
        trigger._selfLatchOnClick = false;
        trigger._externalLatched = latched;
        return trigger;
    }

    public HintTrigger SetLockOverride(System.Func<bool> active, string title, string body)
    {
        _lockOverride = active;
        _lockTitle = title;
        _lockBody = body;
        return this;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_lockOverride != null && _lockOverride()) return;
        Tooltip tip = Resolve();
        if (tip == null) return;
        if (IsLatched) return;
        tip.ShowHint(HitPoint(eventData), title, body);
        _showing = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_showing) return;
        _showing = false;
        if (_tooltip != null) _tooltip.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_lockOverride != null && _lockOverride())
        {
            Tooltip tip = Resolve();
            if (tip != null)
            {
                tip.ShowHint(HitPoint(eventData), _lockTitle, _lockBody);
                _showing = true;
            }
            return;
        }
        if (_selfLatchOnClick) _clicked = true;
        if (!_showing) return;
        _showing = false;
        if (_tooltip != null) _tooltip.Hide();
    }

    private static Tooltip Resolve()
    {
        if (_tooltip == null) _tooltip = FindAnyObjectByType<Tooltip>();
        return _tooltip;
    }

    private static Vector3 HitPoint(PointerEventData eventData)
    {
        if (eventData != null && eventData.pointerCurrentRaycast.isValid)
            return eventData.pointerCurrentRaycast.worldPosition;
        return CameraRig.MainTransform != null ? CameraRig.MainTransform.position : Vector3.zero;
    }
}
