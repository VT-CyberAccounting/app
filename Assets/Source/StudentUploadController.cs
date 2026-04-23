using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class StudentUploadController : MonoBehaviour
{
    public CSVDataSource dataSource;
    public CSVDataSource studentSource;
    public CSVDataSource solutionSource;

    public string savedFileName = "student.csv";

    public bool IsBusy { get; private set; }

    public event System.Action<string> OnStatus;

    public void LoadFromUrl(string url)
    {
        if (IsBusy)
        {
            Report("Upload already in progress.");
            return;
        }
        if (string.IsNullOrWhiteSpace(url))
        {
            Report("No URL provided.");
            return;
        }
        StartCoroutine(LoadFromUrlCoroutine(url.Trim()));
    }

    public void LoadFromLocalCsvText(string csvText)
    {
        if (string.IsNullOrEmpty(csvText))
        {
            Report("Empty CSV text.");
            return;
        }
        PersistAndApply(csvText);
    }

    private IEnumerator LoadFromUrlCoroutine(string url)
    {
        IsBusy = true;
        Report($"Fetching {url}...");

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Report($"Fetch failed: {www.error}");
                IsBusy = false;
                yield break;
            }

            string csvText = www.downloadHandler.text;
            PersistAndApply(csvText);
        }

        IsBusy = false;
    }

    private void PersistAndApply(string csvText)
    {
        string savedPath = Path.Combine(Application.persistentDataPath, savedFileName);
        try
        {
            File.WriteAllText(savedPath, csvText);
            Report($"Saved to {savedPath}");
        }
        catch (System.Exception e)
        {
            Report($"Failed to save: {e.Message}");
        }

        if (studentSource != null)
            studentSource.LoadFromCsvText(csvText);

        string industry = SolutionSurfaceBuilder.DetectIndustryFromStudentCsv(csvText);
        if (string.IsNullOrEmpty(industry))
        {
            Report("Could not detect industry column in uploaded CSV.");
            return;
        }
        Report($"Detected industry: {industry}");

        if (dataSource == null)
        {
            Report("Data source reference not set.");
            return;
        }
        if (!dataSource.IsLoaded)
        {
            Report("Data source not loaded yet; retrying when ready.");
            dataSource.OnDataLoaded += () => ApplySolution(industry);
            return;
        }

        ApplySolution(industry);
    }

    private void ApplySolution(string industry)
    {
        string solutionCsv = SolutionSurfaceBuilder.BuildSolutionCsv(dataSource, industry);
        if (solutionSource != null)
            solutionSource.LoadFromCsvText(solutionCsv);
        Report($"Solution surface rebuilt for industry '{industry}'.");
    }

    private void Report(string msg)
    {
        Debug.Log($"[StudentUpload] {msg}");
        OnStatus?.Invoke(msg);
    }
}
