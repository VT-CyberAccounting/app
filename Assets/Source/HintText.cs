using UnityEngine.SceneManagement;

public static class HintText
{
    public const string NoToolSelected =
        "Select a tool to evaluate or customize the parent sheet.";

    public const string NoSectionSelected =
        "Select a section to filter or sort the parent sheet.";

    public const string WindowLocked =
        "This window is locked because you edited its parent sheet using a tool. " +
        "To unlock a window, undo all edits you made to its parent sheet.";

    public static bool HasDataSource =>
        DatasetManager.ActiveSource != null && DatasetManager.ActiveSource.IsLoaded;

    public static string NoDataSource()
    {
        string scene = SceneManager.GetActiveScene().name;
        bool database = scene != null && scene.ToLowerInvariant().Contains("database");
        return database
            ? "Ask for a company's finances to build a parent sheet."
            : "Scan a QR code to add a parent sheet.";
    }
}
