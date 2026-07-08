using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class DatasetChipExpansion : MonoBehaviour, IPointerExitHandler
{
    private const float TextPad = 12f;
    private const float MaxWidth = 320f;

    private UIButton.Handle _pill;
    private RectTransform _rt;
    private HintTrigger _hint;
    private System.Action<int> _onClick;
    private int _index = -1;

    public static DatasetChipExpansion Create(Transform canvas, TMP_FontAsset font,
        System.Action<int> onClick, System.Func<bool> hintLatched)
    {
        UIButton.Handle pill = UIButton.Create(canvas, "DatasetExpansion", "",
            height: MetaTokens.ButtonHeight, fontSize: MetaTokens.Body1,
            alignment: TextAlignmentOptions.Center, padLeft: TextPad, padRight: TextPad);

        LayoutElement le = pill.Root.GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = true;

        DatasetChipExpansion c = pill.Root.AddComponent<DatasetChipExpansion>();
        c._pill = pill;
        c._rt = pill.Root.GetComponent<RectTransform>();
        c._onClick = onClick;
        c._hint = HintTrigger.AttachShared(pill.Root, "", "", hintLatched);
        if (font != null && pill.Text != null) pill.Text.font = font;

        c._rt.anchorMin = c._rt.anchorMax = new Vector2(0f, 1f);
        c._rt.pivot = new Vector2(0f, 1f);
        pill.Button.onClick.AddListener(() => { if (c._index >= 0) c._onClick?.Invoke(c._index); });

        pill.Root.SetActive(false);
        return c;
    }

    public void Show(RectTransform chip, string label, int index, bool selected)
    {
        if (chip == null || _pill == null) return;

        _index = index;
        _pill.Text.text = label;
        UIButton.SetSelected(_pill, selected);
        if (_hint != null)
        {
            _hint.title = label;
            _hint.body = $"This button opens the Data Panel for {label}.";
        }
        _pill.Root.SetActive(true);

        float textW = _pill.Text.GetPreferredValues(label).x + TextPad * 2f;
        float w = Mathf.Clamp(Mathf.Max(chip.rect.width, textW), chip.rect.width, MaxWidth);
        _rt.sizeDelta = new Vector2(w, chip.rect.height);

        Vector3[] corners = new Vector3[4];
        chip.GetWorldCorners(corners);
        _rt.position = corners[1];
        _rt.SetAsLastSibling();
    }

    public void Hide()
    {
        _index = -1;
        if (_pill != null && _pill.Root != null) _pill.Root.SetActive(false);
    }

    public void OnPointerExit(PointerEventData eventData) => Hide();
}
