using System;
using System.Collections.Generic;
using UnityEngine;

public class DatasetManager : MonoBehaviour
{
    public static DatasetManager Instance { get; private set; }
    public static DataSource ActiveSource => Instance != null ? Instance.Active : null;

    public SheetController sheetController;
    public SheetGenerator sheetGenerator;
    public SheetManager sheetManager;
    public DataPanelUI dataPanelUI;
    public ToolController toolController;
    public InspectTool inspectTool;
    public CompareTool compareTool;

    public event Action OnTabsChanged;
    public event Action<int> OnActiveTabChanged;

    public class Tab
    {
        public DataSource source;
        public string label;
        public string payload;
        public SheetManager.EditSnapshot snapshot;
        public CompareTool.PinSnapshot pins;
        public List<EditJournal.Record> journal;
        public int dataTab = -1;
    }

    public const int MaxTabs = 5;

    private readonly List<Tab> _tabs = new List<Tab>();
    private int _active = -1;
    private int _datasetsCreated;

    public IReadOnlyList<Tab> Tabs => _tabs;
    public int ActiveIndex => _active;
    public int TabCount => _tabs.Count;
    public DataSource Active => (_active >= 0 && _active < _tabs.Count) ? _tabs[_active].source : null;

    public bool IsTabLocked(int index)
    {
        if (index < 0 || index >= _tabs.Count) return false;
        if (index == _active)
            return (sheetManager != null && sheetManager.HasInvasiveEdits) ||
                   (compareTool != null && compareTool.HasEntries);
        Tab tab = _tabs[index];
        return tab.snapshot != null || (tab.pins != null && tab.pins.pins != null && tab.pins.pins.Count > 0);
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        ResolveRefs();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void ResolveRefs()
    {
        if (sheetController == null) sheetController = FindAnyObjectByType<SheetController>();
        if (sheetGenerator == null) sheetGenerator = FindAnyObjectByType<SheetGenerator>();
        if (sheetManager == null) sheetManager = FindAnyObjectByType<SheetManager>();
        if (dataPanelUI == null) dataPanelUI = FindAnyObjectByType<DataPanelUI>();
        if (toolController == null) toolController = FindAnyObjectByType<ToolController>();
        if (inspectTool == null) inspectTool = FindAnyObjectByType<InspectTool>();
        if (compareTool == null) compareTool = FindAnyObjectByType<CompareTool>();
    }

    public int AddTab(string payload, string label = null)
    {
        if (string.IsNullOrEmpty(payload)) return -1;

        for (int i = 0; i < _tabs.Count; i++)
            if (_tabs[i].payload == payload)
            {
                bool alreadyActive = i == _active;
                string openLabel = _tabs[i].label;
                SwitchTab(i);
                SheetNotices.Show(this, "Already Open",
                    alreadyActive ? $"{openLabel} is already open." : $"Switched to {openLabel}.");
                return _active;
            }

        int ordinal = _datasetsCreated++;
        GameObject host = new GameObject($"Dataset_{ordinal}");
        host.SetActive(false);
        host.transform.SetParent(transform, false);

        FileReader reader = host.AddComponent<FileReader>();
        reader.loadOnStart = false;
        reader.claimSingleton = false;
        host.SetActive(true);

        Tab tab = new Tab { source = reader, payload = payload, label = label ?? Stylize(DeriveLabel(payload, ordinal)) };
        _tabs.Add(tab);

        reader.onLoadResult = (ok, reason) => OnTabLoadResult(reader, ok, reason);
        reader.Load(payload);

        int index = IndexOfSource(reader);
        if (index < 0) return _active;

        SwitchTab(index);
        OnTabsChanged?.Invoke();
        return _active;
    }

    private int IndexOfSource(DataSource source)
    {
        for (int i = 0; i < _tabs.Count; i++)
            if (_tabs[i].source == source) return i;
        return -1;
    }

    private void OnTabLoadResult(FileReader reader, bool ok, string reason)
    {
        int index = IndexOfSource(reader);
        if (index < 0) return;

        if (ok)
        {
            EvictOldestIfOverCap();
            OnTabsChanged?.Invoke();
            return;
        }

        RemoveTab(index);
        SheetNotices.Show(this, "Scan Failed",
            reason ?? "Couldn't read a dataset from that QR code.");
    }

    public void RemoveTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;

        Tab tab = _tabs[index];
        bool wasActive = index == _active;

        _tabs.RemoveAt(index);
        if (_active > index) _active--;
        else if (wasActive) _active = -1;

        if (tab.source != null) Destroy(tab.source.gameObject);

        if (wasActive && _tabs.Count > 0)
            SwitchTab(_tabs.Count - 1);

        OnTabsChanged?.Invoke();
        if (wasActive && _tabs.Count == 0) OnActiveTabChanged?.Invoke(-1);
    }

    public DatabaseReader GetOrCreateVariableTab(string variable)
    {
        if (string.IsNullOrEmpty(variable)) return null;

        for (int i = 0; i < _tabs.Count; i++)
            if (_tabs[i].source is DatabaseReader existing && existing.Variable == variable)
            {
                SwitchTab(i);
                return existing;
            }

        GameObject host = new GameObject($"Variable_{variable}");
        host.SetActive(false);
        host.transform.SetParent(transform, false);

        DatabaseReader reader = host.AddComponent<DatabaseReader>();
        reader.Configure(variable);
        host.SetActive(true);

        Tab tab = new Tab { source = reader, label = Stylize(variable) };
        _tabs.Add(tab);

        SwitchTab(_tabs.Count - 1);
        EvictOldestIfOverCap();
        OnTabsChanged?.Invoke();
        return reader;
    }

    private void EvictOldestIfOverCap()
    {
        while (_tabs.Count > MaxTabs)
        {
            bool removingActive = _active == 0;
            DataSource src = _tabs[0].source;

            _tabs.RemoveAt(0);
            _active--;
            if (src != null) Destroy(src.gameObject);

            if (removingActive) SwitchTab(_tabs.Count - 1);
        }
    }

    public void SwitchTab(int index)
    {
        if (index < 0 || index >= _tabs.Count || index == _active) return;
        if (_tabs[index].source == null)
        {
            Debug.LogWarning($"[DatasetManager] Tab {index} has no source; ignoring switch.");
            return;
        }

        if (_active >= 0 && _active < _tabs.Count)
        {
            Tab prev = _tabs[_active];
            if (sheetManager != null) prev.snapshot = sheetManager.CaptureState();
            if (compareTool != null) prev.pins = compareTool.CapturePins();
            if (toolController != null) prev.journal = toolController.Journal.Capture();
            if (dataPanelUI != null) prev.dataTab = dataPanelUI.ActiveWindow;
        }

        _active = index;
        Tab next = _tabs[index];

        Rebind(next.source, next.snapshot);

        if (compareTool != null) compareTool.RestorePins(next.pins);
        if (toolController != null)
        {
            toolController.Journal.Restore(next.journal);
            if (compareTool != null) toolController.Journal.PrunePins(compareTool.HasPinFor);
        }
        if (dataPanelUI != null)
        {
            if (next.dataTab >= 0) dataPanelUI.ShowWindow(next.dataTab);
            else dataPanelUI.CloseWindows();
        }

        OnActiveTabChanged?.Invoke(index);
    }

    private void Rebind(DataSource source, SheetManager.EditSnapshot snapshot)
    {
        if (toolController != null)
        {
            toolController.ResetTool(ToolType.Compare);
            toolController.DeselectTool();
        }

        if (inspectTool != null) inspectTool.dataSource = source;
        if (compareTool != null) compareTool.dataSource = source;
        if (sheetController != null) sheetController.SetDataSource(source);

        TryStep("dataPanel", () => { if (dataPanelUI != null) dataPanelUI.Rebind(source); });
        TryStep("sheetManager", () => { if (sheetManager != null) sheetManager.SetDataSource(source); });
        TryStep("sheetGenerator", () => { if (sheetGenerator != null) sheetGenerator.SetDataSource(source); });
        TryStep("restore", () => { if (sheetManager != null) sheetManager.RestoreState(snapshot); });
    }

    private void TryStep(string label, System.Action step)
    {
        try { step(); }
        catch (System.Exception e) { Debug.LogError($"[DatasetManager] Rebind step '{label}' failed: {e}"); }
    }

    private static string Stylize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        string[] words = raw.Split(new[] { '_', '-', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return raw;

        for (int i = 0; i < words.Length; i++)
        {
            if (HasInnerUppercase(words[i])) continue;
            string w = words[i].ToLowerInvariant();
            bool edge = i == 0 || i == words.Length - 1;
            words[i] = (!edge && SmallWords.Contains(w))
                ? w
                : char.ToUpperInvariant(w[0]) + w.Substring(1);
        }
        return string.Join(" ", words);
    }

    private static readonly HashSet<string> SmallWords = new HashSet<string>
    {
        "a", "an", "and", "as", "at", "but", "by", "for", "if", "in", "nor", "of",
        "on", "or", "per", "so", "the", "to", "up", "via", "vs", "yet"
    };

    private static bool HasInnerUppercase(string word)
    {
        for (int i = 1; i < word.Length; i++)
            if (char.IsUpper(word[i])) return true;
        return false;
    }

    private static string DeriveLabel(string payload, int ordinal)
    {
        string fallback = $"Tab {ordinal + 1}";
        if (string.IsNullOrEmpty(payload)) return fallback;

        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return DeriveDataUriName(payload) ?? fallback;

        if (payload.IndexOf('\n') >= 0)
            return fallback;

        string trimmed = payload.Trim();
        int query = trimmed.IndexOf('?');
        if (query >= 0) trimmed = trimmed.Substring(0, query);
        trimmed = trimmed.TrimEnd('/');

        int slash = trimmed.LastIndexOf('/');
        string name = slash >= 0 ? trimmed.Substring(slash + 1) : trimmed;

        int dot = name.LastIndexOf('.');
        if (dot > 0) name = name.Substring(0, dot);

        return string.IsNullOrEmpty(name) ? fallback : name;
    }

    private static string DeriveDataUriName(string payload)
    {
        int comma = payload.IndexOf(',');
        string header = comma >= 0 ? payload.Substring(0, comma) : payload;

        int nameIdx = header.IndexOf("name=", StringComparison.OrdinalIgnoreCase);
        if (nameIdx < 0) return null;

        string name = header.Substring(nameIdx + 5);
        int semi = name.IndexOf(';');
        if (semi >= 0) name = name.Substring(0, semi);
        name = name.Trim();

        int dot = name.LastIndexOf('.');
        if (dot > 0) name = name.Substring(0, dot);

        return string.IsNullOrEmpty(name) ? null : name;
    }
}
