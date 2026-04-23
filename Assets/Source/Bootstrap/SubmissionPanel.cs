using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SubmissionPanel : MonoBehaviour
{
    [Header("Endpoint")]
    [SerializeField] string endpoint = "https://cyberacc.discovery.cs.vt.edu/submission";

    [Header("Scene")]
    [SerializeField] string nextScene = "Main";

    [Header("UI")]
    [SerializeField] TMP_InputField usernameField;
    [SerializeField] Button fetchButton;
    [SerializeField] Transform listContainer;
    [SerializeField] SubmissionRow rowPrefab;
    [SerializeField] TMP_Text statusText;

    readonly List<SubmissionRow> spawnedRows = new();

    void Awake()
    {
        if (fetchButton != null)
            fetchButton.onClick.AddListener(OnFetchClicked);
    }

    void OnFetchClicked()
    {
        var username = usernameField != null ? usernameField.text?.Trim() : null;
        if (string.IsNullOrEmpty(username))
        {
            SetStatus("Enter a username.");
            return;
        }
        StartCoroutine(FetchList(username));
    }

    void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log($"[SubmissionPanel] {msg}");
    }

    void ClearRows()
    {
        foreach (var r in spawnedRows)
            if (r != null) Destroy(r.gameObject);
        spawnedRows.Clear();
    }

    System.Collections.IEnumerator FetchList(string username)
    {
        SetStatus("Fetching...");
        ClearRows();

        const string query = "query($u:String!,$n:Int!){getSubmission(username:$u,limit:$n){label createdAt}}";
        var body = BuildBody(query, $"\"u\":{Json(username)},\"n\":10");

        using var req = PostJson(endpoint, body);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            SetStatus($"Fetch failed: {req.error}");
            yield break;
        }

        var entries = ParseList(req.downloadHandler.text);
        if (entries == null)
        {
            SetStatus("Bad response.");
            yield break;
        }
        if (entries.Count == 0)
        {
            SetStatus("No submissions.");
            yield break;
        }

        foreach (var e in entries)
        {
            var row = Instantiate(rowPrefab, listContainer);
            row.Bind(e.label, e.createdAt, () => OnRowSelected(username, e.label));
            spawnedRows.Add(row);
        }
        SetStatus($"{entries.Count} submission(s).");
    }

    void OnRowSelected(string username, string label)
    {
        StartCoroutine(ResolveAndLoad(username, label));
    }

    System.Collections.IEnumerator ResolveAndLoad(string username, string label)
    {
        SetStatus($"Loading '{label}'...");

        const string query = "query($u:String!,$l:String!){getSubmission(username:$u,label:$l,limit:1){url}}";
        var body = BuildBody(query, $"\"u\":{Json(username)},\"l\":{Json(label)}");

        using var req = PostJson(endpoint, body);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            SetStatus($"Resolve failed: {req.error}");
            yield break;
        }

        var url = ParseUrl(req.downloadHandler.text);
        if (string.IsNullOrEmpty(url))
        {
            SetStatus("No URL returned.");
            yield break;
        }

        QrGateLoader.LastPayload = url;
        SceneManager.LoadScene(nextScene, LoadSceneMode.Single);
    }

    static UnityWebRequest PostJson(string url, string json)
    {
        var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Accept", "application/json");
        return req;
    }

    static string BuildBody(string query, string variablesInner)
    {
        return "{\"query\":" + Json(query) + ",\"variables\":{" + variablesInner + "}}";
    }

    static string Json(string s)
    {
        if (s == null) return "null";
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append($"\\u{(int)c:X4}");
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    public struct Entry
    {
        public string label;
        public string createdAt;
    }

    static List<Entry> ParseList(string json)
    {
        try
        {
            var wrapper = JsonUtility.FromJson<ListResponse>(WrapForUnityJson(json));
            if (wrapper?.data?.getSubmission == null) return null;
            var list = new List<Entry>(wrapper.data.getSubmission.Length);
            foreach (var s in wrapper.data.getSubmission)
                list.Add(new Entry { label = s.label, createdAt = s.createdAt });
            return list;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SubmissionPanel] ParseList: {e.Message}\n{json}");
            return null;
        }
    }

    static string ParseUrl(string json)
    {
        try
        {
            var wrapper = JsonUtility.FromJson<ListResponse>(WrapForUnityJson(json));
            var arr = wrapper?.data?.getSubmission;
            if (arr == null || arr.Length == 0) return null;
            return arr[0].url;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SubmissionPanel] ParseUrl: {e.Message}\n{json}");
            return null;
        }
    }

    static string WrapForUnityJson(string json) => json;

    [Serializable] class ListResponse { public ListData data; }
    [Serializable] class ListData { public SubmissionDto[] getSubmission; }
    [Serializable] class SubmissionDto { public string label; public string createdAt; public string url; }
}
