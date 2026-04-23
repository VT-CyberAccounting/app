using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SubmissionRow : MonoBehaviour
{
    [SerializeField] TMP_Text labelText;
    [SerializeField] TMP_Text timestampText;
    [SerializeField] Button button;

    Action onClick;

    void Awake()
    {
        if (button != null) button.onClick.AddListener(() => onClick?.Invoke());
    }

    public void Bind(string label, string createdAt, Action onClick)
    {
        if (labelText != null) labelText.text = label;
        if (timestampText != null) timestampText.text = FormatTimestamp(createdAt);
        this.onClick = onClick;
    }

    static string FormatTimestamp(string iso)
    {
        if (string.IsNullOrEmpty(iso)) return "";
        if (DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        return iso;
    }
}
