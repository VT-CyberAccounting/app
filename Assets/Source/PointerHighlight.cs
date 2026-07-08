using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class PointerHighlight : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private const float LiftScale = 1.03f;
    private const float LiftDuration = 0.08f;

    public Graphic target;
    public Color restColor;
    public Color pressColor;

    public Transform liftTarget;

    public Graphic textTarget;
    public Color textRestColor;
    public Color textPressColor;

    private bool _hovered;
    private bool _pressed;
    private bool _suppressed;
    private Vector3 _baseScale = Vector3.one;
    private float _liftT;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    public void SetLiftTarget(Transform t)
    {
        liftTarget = t;
        _baseScale = (t != null ? t : transform).localScale;
    }

    public static PointerHighlight Attach(GameObject go, Color press)
    {
        Button btn = go.GetComponent<Button>();
        Graphic graphic;
        Color rest;

        if (btn != null && btn.transition == Selectable.Transition.ColorTint && btn.targetGraphic != null)
        {
            graphic = btn.targetGraphic;
            rest = btn.colors.normalColor;
            graphic.canvasRenderer.SetColor(Color.white);
        }
        else
        {
            Transform innerT = go.transform.Find("Inner");
            graphic = innerT != null ? innerT.GetComponent<Graphic>() : go.GetComponent<Graphic>();
            rest = graphic != null ? graphic.color : Color.clear;
        }

        if (btn != null) btn.transition = Selectable.Transition.None;
        if (graphic == null) return null;

        PointerHighlight hl = go.GetComponent<PointerHighlight>();
        if (hl == null) hl = go.AddComponent<PointerHighlight>();
        hl.target = graphic;
        hl.SetColors(rest, press);

        Transform parent = go.transform.parent;
        if (parent != null && parent.name.EndsWith("_Border"))
            hl.SetLiftTarget(parent);
        return hl;
    }

    public static PointerHighlight AttachButtonFeedback(Transform buttonT, Color textActive, bool wireText = true)
    {
        PointerHighlight hl = Attach(buttonT.gameObject, MetaTokens.Alpha(MetaTokens.Blue, 0.30f));
        if (hl == null) return null;

        if (buttonT.parent != null && buttonT.parent.name.EndsWith("_Border"))
        {
            Image border = buttonT.parent.GetComponent<Image>();
            if (border != null) border.color = MetaTokens.SheetAlt;
            RectTransform brt = buttonT as RectTransform;
            if (brt != null) { brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero; }
        }

        if (wireText)
        {
            Transform textT = buttonT.Find("Text");
            TextMeshProUGUI text = textT != null
                ? textT.GetComponent<TextMeshProUGUI>()
                : buttonT.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null) hl.SetTextColors(text, text.color, textActive);
        }
        return hl;
    }

    public void SetColors(Color rest, Color press)
    {
        restColor = rest;
        pressColor = press;
        Apply();
    }

    public void SetSuppressed(bool suppressed)
    {
        _suppressed = suppressed;
        if (suppressed)
        {
            _pressed = false;
            _hovered = false;
        }
        Apply();
    }

    public void SetTextColors(Graphic text, Color rest, Color press)
    {
        textTarget = text;
        textRestColor = rest;
        textPressColor = press;
        Apply();
    }

    public void SetRest(Color rest)
    {
        restColor = rest;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_suppressed) return;
        _hovered = true;
        Apply();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        _pressed = false;
        Apply();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_suppressed) return;
        _pressed = true;
        Apply();
        UIPressFeedback.Play();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pressed = false;
        Apply();
    }

    private void Update()
    {
        float goal = (_hovered && !_suppressed) ? 1f : 0f;
        if (Mathf.Approximately(_liftT, goal)) return;

        _liftT = Mathf.MoveTowards(_liftT, goal, Time.unscaledDeltaTime / LiftDuration);
        Transform xform = liftTarget != null ? liftTarget : transform;
        xform.localScale = _baseScale * Mathf.Lerp(1f, LiftScale, _liftT);
    }

    private void Apply()
    {
        bool pressed = _pressed && !_suppressed;
        bool hovered = _hovered && !_suppressed;
        if (target != null)
            target.color = pressed ? pressColor : (hovered ? Brighten(restColor) : restColor);
        if (textTarget != null)
            textTarget.color = pressed ? textPressColor : textRestColor;
    }

    private static Color Brighten(Color c)
    {
        Color hovered = Color.Lerp(c, Color.white, 0.12f);
        hovered.a = Mathf.Clamp01(c.a + 0.06f);
        return hovered;
    }
}
