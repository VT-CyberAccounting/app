using UnityEngine;
using TMPro;
using Oculus.Interaction;

public abstract class PanelUI : MonoBehaviour
{
    public float zOffsetFromCamera = 0.75f;
    public float yGrabBounds = 1f;

    public TMP_FontAsset bodyFont;
    public TMP_FontAsset titleFont;

    protected Canvas _canvas;
    protected PanelGrab _panelGrab;
    private OneGrabTranslateTransformer _slideTransformer;

    public bool IsVisible => _canvas != null && _canvas.gameObject.activeSelf;

    public abstract void ShowPanel();
    public abstract void HidePanel();

    public void TogglePanel()
    {
        if (_canvas == null) return;
        if (_canvas.gameObject.activeSelf) HidePanel();
        else ShowPanel();
    }

    protected void ShowCanvas()
    {
        PlaceInFrontOfCamera();
        _canvas.gameObject.SetActive(true);
        if (_panelGrab != null) _panelGrab.SetGrabbable(true);
    }

    protected void HideCanvas()
    {
        _canvas.gameObject.SetActive(false);
        if (_panelGrab != null) _panelGrab.SetGrabbable(false);
    }

    protected void ApplyPanelSize()
    {
        if (_canvas == null) return;
        RectTransform rt = _canvas.transform as RectTransform;
        if (rt != null) rt.sizeDelta = new Vector2(MetaTokens.PanelWidth, MetaTokens.PanelHeight);

        RectTransform primaryRt = UITransformSearch.FindDeep(transform, "PrimaryPanel") as RectTransform;
        if (primaryRt != null)
            primaryRt.sizeDelta = new Vector2(primaryRt.sizeDelta.x, MetaTokens.PanelHeight);
    }

    protected void ApplyFonts()
    {
        if (bodyFont != null)
        {
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++) texts[i].font = bodyFont;
        }

        if (titleFont != null)
        {
            Transform title = UITransformSearch.FindDeep(transform, "Title");
            TextMeshProUGUI t = title != null ? title.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (t != null) t.font = titleFont;
        }
    }

    protected virtual float XOffsetFromCamera => 0f;

    protected void PlaceInFrontOfCamera()
    {
        Transform cam = CameraRig.MainTransform;
        if (cam == null) return;

        Vector3 forward = cam.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        transform.position = cam.position + forward * zOffsetFromCamera + right * XOffsetFromCamera;

        Vector3 face = transform.position - cam.position;
        face.y = 0f;
        transform.rotation = Quaternion.LookRotation(face.sqrMagnitude > 1e-6f ? face.normalized : forward);
        ApplyVerticalBounds();
    }

    protected void ApplyVerticalBounds()
    {
        if (_slideTransformer == null) _slideTransformer = GetComponent<OneGrabTranslateTransformer>();
        if (_slideTransformer == null) return;

        float restY = transform.localPosition.y;
        OneGrabTranslateTransformer.OneGrabTranslateConstraints c = _slideTransformer.Constraints;
        c.ConstraintsAreRelative = false;
        c.MinY = new FloatConstraint { Constrain = true, Value = restY - yGrabBounds };
        c.MaxY = new FloatConstraint { Constrain = true, Value = restY + yGrabBounds };
        _slideTransformer.Constraints = c;
    }
}
