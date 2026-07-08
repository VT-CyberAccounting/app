using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class FileReader : DataSource
{
    public static FileReader Instance { get; private set; }

    public bool loadOnStart = true;
    public bool claimSingleton = true;

    public Action<bool, string> onLoadResult;

    private void ReportResult(bool ok, string reason) => onLoadResult?.Invoke(ok, reason);

    private void Awake()
    {
        if (claimSingleton && Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        if (!loadOnStart) return;

        string source = ResolveSource();
        if (string.IsNullOrEmpty(source))
        {
            Debug.LogError($"[FileReader:{name}] No data source resolved (no QR payload and no .csv in StreamingAssets).");
            return;
        }

        Load(source);
    }

    private string ResolveSource()
    {
        if (DataRequest.Has) return DataRequest.Consume();
        return ResolveCsvFileName();
    }

    private enum SourceKind { StreamingAsset, Url, Inline }

    public void Load(string source)
    {
        switch (Classify(source))
        {
            case SourceKind.Url:    StartCoroutine(LoadFromUrl(source)); break;
            case SourceKind.Inline: LoadFromCsvText(StripDataUri(source)); break;
            default:                StartCoroutine(LoadFromStreamingAssets(source)); break;
        }
    }

    private static SourceKind Classify(string source)
    {
        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return SourceKind.Url;
        if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            source.IndexOf('\n') >= 0)
            return SourceKind.Inline;
        return SourceKind.StreamingAsset;
    }

    private static string StripDataUri(string source)
    {
        if (!source.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return source;
        int comma = source.IndexOf(',');
        return comma >= 0 ? source.Substring(comma + 1) : source;
    }

    private string ResolveCsvFileName()
    {
        try
        {
            if (!Directory.Exists(Application.streamingAssetsPath)) return null;

            string[] files = Directory.GetFiles(Application.streamingAssetsPath, "*.csv");
            string chosen = null;
            for (int i = 0; i < files.Length; i++)
            {
                if (!files[i].EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase)) continue;
                string candidate = Path.GetFileName(files[i]);
                if (chosen == null || string.Compare(candidate, chosen, System.StringComparison.OrdinalIgnoreCase) < 0)
                    chosen = candidate;
            }

            if (chosen != null)
                Debug.Log($"[FileReader:{name}] Auto-selected '{chosen}' from StreamingAssets.");

            return chosen;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[FileReader:{name}] Could not scan StreamingAssets ({e.Message}).");
            return null;
        }
    }

    public System.Collections.IEnumerator LoadFromUrl(string url) => LoadFromWebRequest(url);

    public System.Collections.IEnumerator LoadFromStreamingAssets(string fileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        string url = path.Contains("://") ? path : "file://" + path;
        return LoadFromWebRequest(url);
    }

    private System.Collections.IEnumerator LoadFromWebRequest(string url)
    {
        using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[FileReader:{name}] Failed to load CSV from {url}: {www.error}");
                ClearGrid();
                RaiseDataLoaded();
                ReportResult(false, "The QR code's link could not be reached.");
                yield break;
            }

            LoadFromCsvText(www.downloadHandler.text);
        }
    }

    public void LoadFromCsvText(string csvText)
    {
        ClearGrid();

        if (LooksLikeHtml(csvText))
        {
            Fail("The QR code's link returned a web page, not a dataset.");
            return;
        }

        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        List<List<string>> grid = new List<List<string>>(lines.Length);
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            grid.Add(ParseCSVLine(line));
        }

        if (grid.Count < 2)
        {
            Fail("The QR code's data has no data rows.");
            return;
        }

        List<string> header = grid[0];
        if (header.Count > 0) SetAxisTitles(header[0]);
        for (int c = 1; c < header.Count; c++)
            _columnTitles.Add(header[c].Trim());

        int rowCount = grid.Count - 1;
        int colCount = _columnTitles.Count;

        if (rowCount == 0 || colCount == 0)
        {
            ClearGrid();
            Fail("The QR code's data has no rows or columns.");
            return;
        }

        _values = new float[rowCount, colCount];

        _globalMin = float.MaxValue;
        _globalMax = float.MinValue;
        int numericCells = 0;

        for (int r = 0; r < rowCount; r++)
        {
            List<string> fields = grid[r + 1];
            _rowTitles.Add(fields.Count > 0 ? fields[0].Trim() : "");

            for (int c = 0; c < colCount; c++)
            {
                string field = (c + 1 < fields.Count) ? fields[c + 1] : null;
                if (TryParseFloat(field, out float v)) numericCells++;
                _values[r, c] = v;
                if (v < _globalMin) _globalMin = v;
                if (v > _globalMax) _globalMax = v;
            }
        }

        if (numericCells == 0)
        {
            ClearGrid();
            Fail("The QR code's data has no numeric values.");
            return;
        }

        if (Mathf.Approximately(_globalMin, _globalMax))
        {
            _globalMax = _globalMin + 1f;
        }

        for (int c = 0; c < colCount; c++) _visibleColumns.Add(c);
        for (int r = 0; r < rowCount; r++) _visibleRows.Add(r);

        RebuildFilter();

        Debug.Log($"[FileReader:{name}] Loaded grid {rowCount} rows x {colCount} cols " +
                  $"(range [{_globalMin}, {_globalMax}])");
        RaiseDataLoaded();
        ReportResult(true, null);
    }

    private void Fail(string reason)
    {
        Debug.LogWarning($"[FileReader:{name}] {reason}");
        RebuildFilter();
        RaiseDataLoaded();
        ReportResult(false, reason);
    }

    private static bool LooksLikeHtml(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        int i = 0;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        return i < text.Length && text[i] == '<';
    }

    private void SetAxisTitles(string corner)
    {
        if (string.IsNullOrWhiteSpace(corner)) return;

        int sep = corner.IndexOf('\\');
        if (sep < 0) sep = corner.IndexOf('/');

        if (sep >= 0)
        {
            _rowAxisTitle = CleanAxisTitle(corner.Substring(0, sep));
            _columnAxisTitle = CleanAxisTitle(corner.Substring(sep + 1));
        }
        else
        {
            _rowAxisTitle = CleanAxisTitle(corner);
        }
    }

    private static string CleanAxisTitle(string value)
    {
        value = value.Trim();
        return value.Length == 0 ? null : value;
    }

    private void ClearGrid()
    {
        _columnTitles.Clear();
        _rowTitles.Clear();
        _columnAxisTitle = null;
        _rowAxisTitle = null;
        _values = new float[0, 0];
        _visibleColumns.Clear();
        _visibleRows.Clear();
        _visibleColumnIndices.Clear();
        _visibleRowIndices.Clear();
        _globalMin = 0f;
        _globalMax = 1f;
    }

    private static List<string> ParseCSVLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        var sb = new System.Text.StringBuilder(64);

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }

    private static bool TryParseFloat(string value, out float result)
    {
        result = 0f;
        return !string.IsNullOrEmpty(value) &&
               float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }
}
