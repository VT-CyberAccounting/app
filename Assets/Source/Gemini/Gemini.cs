using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Networking;

public enum GeminiStatus {
    Off,
    Connecting,
    Live,
    Reconnecting,
    Failed,
    MicDenied
}

public static class Gemini {

    private const string ModelId = "gemini-3.1-flash-live-preview";

    private static Client client;
    private static LiveConnectConfig config;
    private static string basePrompt;
    private static readonly ConcurrentQueue<byte[]> sendQueue = new ConcurrentQueue<byte[]>();
    private static SemaphoreSlim sendSignal;
    private static CancellationTokenSource sessionCts;
    private static Task sessionTask;
    private static volatile AsyncSession liveSession;
    private static bool micGranted;
    private static bool _listening;
    private static volatile GeminiStatus _status = GeminiStatus.Off;
    private static int sessionGeneration;
    private static volatile string resumeHandle;
    private static long lastActivityTicks;
    private static volatile bool keepAlive;

    private const int SpeechAmplitude = 600;
    private const int MaxQueuedFrames = 250;

    public static GeminiStatus Status => _status;

    public static double SecondsSinceActivity =>
        (System.Diagnostics.Stopwatch.GetTimestamp() - Volatile.Read(ref lastActivityTicks))
        / (double)System.Diagnostics.Stopwatch.Frequency;

    private static bool Current(int gen) => gen == Volatile.Read(ref sessionGeneration);

    private static void SetStatus(int gen, GeminiStatus status) {
        if (Current(gen)) _status = status;
    }

    private static void touchActivity() {
        Volatile.Write(ref lastActivityTicks, System.Diagnostics.Stopwatch.GetTimestamp());
    }

    private static bool IsSpeech(byte[] pcm) {
        int samples = pcm.Length / 2;
        if (samples == 0) return false;
        long sum = 0;
        for (int i = 0; i + 1 < pcm.Length; i += 2) {
            short s = (short)(pcm[i] | (pcm[i + 1] << 8));
            sum += s < 0 ? -s : s;
        }
        return sum / samples > SpeechAmplitude;
    }

    private const string SystemPrompt =
        "You are a hands-free voice assistant embedded in a VR data-visualization app. " +
        "The user explores a spreadsheet-like grid called the Sheet (rows and columns of cells). " +
        "You can perform the app's actions by calling the provided tools instead of asking the user to press buttons. " +
        "Every tool result includes a state object (the active tool, whether each panel is open, the open dataset, and the lock flag). " +
        "Treat it as the current truth: the user also changes things by hand between your turns, so never assume the app is still how you left it. " +
        "You may also receive messages starting with '[state]': silent app state updates reporting what the user changed by hand. They are not the user speaking; never reply to them, just use them as the new state. " +
        "When a state object carries a notice (for example the data changed shape and edits or pins were cleared), work from that new reality, and briefly mention it the next time you speak if it affects what the user asked. " +
        "The tools mirror the app's real buttons, so an action can require its buttons to be on screen: selecting a tool needs the tool panel open, and a tool's mode buttons also need that tool selected. Both panels can be open at the same time. " +
        "When a tool returns preconditionUnmet, do not retry unchanged. If the user's request explicitly asked for the blocked action, their command is your consent: perform the missing enabling steps yourself (SetPanel, SelectTool, SetInspectMode, SetCompareMode, SetSliceAxis, SetColor), retry the action, and briefly mention what you opened or selected along the way. " +
        "If the request was ambiguous, or the enabling step is destructive or loses work (undoing edits, clearing compare entries, resetting filters), ask the user first. " +
        "Tool conventions: tools that act on a piece take 'sheet', a piece id from GetSheetInfo; omitting it when several pieces exist returns needsSheet with the piece list so you can ask which one, unless the user already named it. " +
        "Tools with a 'dataset' argument refuse when it is not the open dataset; always pass it when the user names a dataset. " +
        "A tool's mode and setting buttons require the tool panel open and that tool selected. " +
        "Rows and columns are addressed by 1-based numbers: row 1 is the first row, column 1 is the first column. " +
        "When the user refers to a row or column by name, look up its number before acting: FindRowColumn for a few names (cheap, preferred on large datasets), GetSheetInfo for the full picture. " +
        "A spoken name can also be a dataset tab, not a row or column — especially when the user says 'dataset' or 'sheet', or asks to 'show'/'open' something. If the name matches an open tab (FindRowColumn reports these as datasetMatches), use SwitchDataset instead of searching inside the current dataset. " +
        "GetSheetInfo's sheets array gives each piece's position along the columns and rows axes, so resolve references like 'the left piece' or 'the red piece' by comparing positions and colors there. " +
        "Numbers are per-dataset: several datasets can be open as tabs (GetSheetInfo lists them), and after switching datasets you must call GetSheetInfo again before using numbers. " +
        "When the user names a dataset for a visibility, sort, or reset change, pass it as the tool's dataset argument; if it is not the open dataset the tool refuses so you can offer to switch with SwitchDataset. " +
        "Each dataset keeps its own tool edits, compare pins, and undo history; switching datasets restores them, so switching is always safe. " +
        "Call GetSheetInfo whenever you are unsure of the current state beyond what the state object shows. " +
        "GetSheetInfo only describes the layout, not the numbers; to describe or analyze the data itself (values, totals, averages, what stands out), call GetSheetData. " +
        "The rows and columns may each represent a category (for example the rows are years and the columns are indicators); GetSheetInfo returns these as rowCategory and columnCategory when the data provides them, so call it and use those to explain what the data is about rather than guessing. " +
        "If a tool reports that the data panel is locked, explain that the user must undo the tool's edits first (UndoEdit, or UndoAllEdits); compare entries also lock it until they are undone. " +
        "Selecting a tool only arms it, but you can also act for the user once it is armed: Inspect points at a cell, row, or column; AddCompareEntry pins a cell or piece into the compare ledger; SliceSheet cuts along the armed axis; PaintSheet paints a piece with the chosen color; MoveSheet slides a piece in a direction. " +
        "Chain the setup yourself when the request is explicit (SelectTool, then the mode/axis/color setter, then the action). The user can always do the same things by pointing at the Sheet with their hands, so after arming a tool without acting, tell them how. " +
        "For the Inspect tool you can point for the user: call Inspect to hover a column, row, or cell. It requires the Inspect tool to be selected with the matching mode (Inspect Columns/Rows/cell); if Inspect returns preconditionUnmet and the user explicitly asked you to inspect something, set that up yourself (SelectTool, SetInspectMode) and retry. " +
        "Resolve 'the biggest/smallest sheet' from GetSheetInfo's sheets array: default a bare 'biggest sheet' to cell count, but when the user is comparing values, ask whether they mean cell count, total, or average before choosing. " +
        "For the color tool, after selecting it call SetColor to choose a palette color, then PaintSheet to paint a piece (or the user points at the Sheet to paint by hand). " +
        "All edits share one undo timeline, newest first: UndoEdit undoes the single most recent edit (slice, move, color, or compare pin) no matter which tool made it, and UndoAllEdits clears them all. " +
        "ResetFilters is different: it is the data panel's Reset All that restores row/column visibility and sorting. Do not call one 'Reset All' when you mean the other. " +
        "Before undoing all edits or resetting the filters, confirm with the user. ";

    private const string SearchPrompt =
        "You can use Google Search to answer general questions about the world that need current or factual information you are not sure of; briefly say you looked it up. " +
        "Do not search for questions about the on-screen data, the Sheet, or the app itself; use the sheet tools for those. ";

    private const string PromptTail =
        "Keep spoken replies short and conversational.";

    private static Task<bool> ensureMicPermission() {
        var tcs = new TaskCompletionSource<bool>();
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Permission.HasUserAuthorizedPermission(Permission.Microphone)) {
            tcs.SetResult(true);
            return tcs.Task;
        }
        var callbacks = new PermissionCallbacks();
        callbacks.PermissionGranted += _ => tcs.TrySetResult(true);
        callbacks.PermissionDenied += _ => tcs.TrySetResult(false);
        callbacks.PermissionDeniedAndDontAskAgain += _ => tcs.TrySetResult(false);
        Permission.RequestUserPermission(Permission.Microphone, callbacks);
#else
        tcs.SetResult(true);
#endif
        return tcs.Task;
    }

    private static Task<string> loadApiKey() {
        var path = Application.streamingAssetsPath + "/gemini.key";
        if (!path.Contains("://"))
            path = "file://" + path;

        var tcs = new TaskCompletionSource<string>();
        var req = UnityWebRequest.Get(path);
        var op = req.SendWebRequest();
        op.completed += _ => {
            if (req.result == UnityWebRequest.Result.Success) {
                tcs.SetResult(req.downloadHandler.text.Trim());
            }
            else {
                Debug.LogError($"[Gemini] Failed to load API key from {path}: {req.error}");
                tcs.SetResult("");
            }
            req.Dispose();
        };
        return tcs.Task;
    }

    public static async Task init(bool webSearch) {
        try {
            client = new Client(apiKey: await loadApiKey());

            var tools = new List<Tool>();
            if (webSearch) tools.Add(new Tool { GoogleSearch = new GoogleSearch() });
            tools.Add(new Tool {
                FunctionDeclarations = ToolTemplate.Registry.Values
                    .Where(t => t.IsAvailable())
                    .Select(t => t.Declaration).ToList()
            });

            basePrompt = SystemPrompt + (webSearch ? SearchPrompt : "") + PromptTail;
            config = new LiveConnectConfig {
                SystemInstruction = new Content {
                    Parts = new List<Part> {
                        new Part { Text = basePrompt }
                    }
                },
                ResponseModalities = new List<Modality> {
                    Modality.Audio
                },
                SpeechConfig = new SpeechConfig {
                    LanguageCode = "en-US",
                    VoiceConfig = new VoiceConfig {
                        PrebuiltVoiceConfig = new PrebuiltVoiceConfig {
                            VoiceName = "Charon"
                        }
                    }
                },
                Tools = tools
            };

            micGranted = await ensureMicPermission();
            if (!micGranted) {
                Debug.LogWarning("[Gemini] Microphone permission denied; voice input disabled.");
                _status = GeminiStatus.MicDenied;
            }

            spkr.init();
            sendSignal = new SemaphoreSlim(0);

            if (micGranted) {
                voip.init();
                voip.turret += sendTick;
            }
        }
        catch (Exception e) {
            Debug.LogError(e);
            _status = GeminiStatus.Failed;
        }
    }

    public static void destroy() {
        try {
            Interlocked.Increment(ref sessionGeneration);
            sessionCts?.Cancel();
            sessionCts = null;
            sessionTask = null;
            bool hadMic = micGranted;
            if (hadMic) voip.turret -= sendTick;
            runAudio(() => {
                if (hadMic) voip.destroy();
                spkr.destroy();
            });
            _listening = false;
            _status = GeminiStatus.Off;
        }
        catch (Exception e) {
            Debug.LogError(e);
        }
    }

    private static readonly object audioGate = new object();
    private static Task audioTail = Task.CompletedTask;

    private static void runAudio(Action action) {
        lock (audioGate) {
            audioTail = audioTail.ContinueWith(_ => {
                try { action(); }
                catch (Exception e) { Debug.LogError(e); }
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }

    private static void sendTick(byte[] data) {
        sendQueue.Enqueue(data);
        while (sendQueue.Count > MaxQueuedFrames && sendQueue.TryDequeue(out _)) { }
        if (IsSpeech(data)) touchActivity();
        sendSignal?.Release();
    }

    private static async Task SendPump(AsyncSession s, CancellationToken token) {
        while (!token.IsCancellationRequested) {
            try { await sendSignal.WaitAsync(token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            while (sendQueue.TryDequeue(out byte[] data)) {
                try {
                    await s.SendRealtimeInputAsync(new LiveSendRealtimeInputParameters {
                        Audio = new Blob {
                            MimeType = "audio/pcm;rate=16000",
                            Data = data
                        }
                    }).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception e) {
                    Debug.LogError($"[Gemini] send failed: {e.Message}");
                    return;
                }
            }
        }
    }

    private static async Task ReceivePump(AsyncSession s, CancellationToken token) {
        while (!token.IsCancellationRequested) {
            LiveServerMessage response;
            try { response = await s.ReceiveAsync().ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (Exception e) {
                Debug.LogError($"[Gemini] receive failed: {e.Message}");
                break;
            }
            if (response == null) break;
            receiveTick(s, response);
        }
    }

    private static async Task RefreshSystemInstruction() {
        if (config == null || basePrompt == null) return;
        string state = null;
        try { await MainThread.Run(() => state = SceneTool.StateSummary()).ConfigureAwait(false); }
        catch (Exception e) { Debug.LogWarning($"[Gemini] Could not gather state for session start: {e.Message}"); }

        string prompt = basePrompt;
        if (!string.IsNullOrEmpty(state))
            prompt += " App state as this session starts (may already be stale; the state object on tool results is authoritative): " + state;

        config.SystemInstruction = new Content {
            Parts = new List<Part> { new Part { Text = prompt } }
        };
    }

    private static async Task RunSessionAsync(int gen, CancellationToken token) {
        int backoffMs = 500;
        int failures = 0;

        while (!token.IsCancellationRequested) {
            AsyncSession s = null;
            try {
                await RefreshSystemInstruction().ConfigureAwait(false);
                config.SessionResumption = new SessionResumptionConfig { Handle = resumeHandle };
                s = await client.Live.ConnectAsync(model: ModelId, config: config).ConfigureAwait(false);
                if (!Current(gen)) break;
                _status = GeminiStatus.Live;
                liveSession = s;

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var send = SendPump(s, token);
                var recv = ReceivePump(s, token);
                await Task.WhenAny(send, recv).ConfigureAwait(false);
                if (ReferenceEquals(liveSession, s)) liveSession = null;
                try { await s.CloseAsync().ConfigureAwait(false); } catch { }
                try { await Task.WhenAll(send, recv).ConfigureAwait(false); } catch { }
                s = null;
                sw.Stop();

                if (sw.ElapsedMilliseconds >= 2000) {
                    failures = 0;
                    backoffMs = 500;
                }
                else {
                    failures++;
                    backoffMs = Mathf.Min(backoffMs * 2, 5000);
                    if (resumeHandle != null) {
                        Debug.LogWarning("[Gemini] session died immediately; dropping stale resume handle and starting fresh");
                        resumeHandle = null;
                    }
                }
            }
            catch (OperationCanceledException) {
                break;
            }
            catch (Exception e) {
                Debug.LogError($"[Gemini] session error: {e}");
                if (s == null) resumeHandle = null;
                failures++;
                backoffMs = Mathf.Min(backoffMs * 2, 5000);
            }
            finally {
                if (s != null) {
                    if (ReferenceEquals(liveSession, s)) liveSession = null;
                    try { await s.CloseAsync().ConfigureAwait(false); } catch { }
                }
            }

            if (token.IsCancellationRequested) break;
            if (!keepAlive) break;

            if (failures >= 5) {
                Debug.LogError("[Gemini] giving up after repeated session failures");
                SetStatus(gen, GeminiStatus.Failed);
                break;
            }

            SetStatus(gen, GeminiStatus.Reconnecting);
            try { await Task.Delay(backoffMs, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }

        if (Current(gen) && _status != GeminiStatus.Failed)
            _status = GeminiStatus.Off;
    }

    private static void receiveTick(AsyncSession s, LiveServerMessage response) {
        if (response.SetupComplete != null) return;

        if (response.SessionResumptionUpdate != null) {
            var update = response.SessionResumptionUpdate;
            if (update.Resumable == true && !string.IsNullOrEmpty(update.NewHandle))
                resumeHandle = update.NewHandle;
            return;
        }

        if (response.GoAway != null) {
            Debug.Log($"[Gemini] server GoAway (time left: {response.GoAway.TimeLeft}); reconnecting");
            _ = s.CloseAsync();
            return;
        }

        if (response.ToolCall != null) {
            touchActivity();
            var calls = response.ToolCall.FunctionCalls;
            if (calls != null)
                foreach (var call in calls)
                    _ = ToolTemplate.Run(s, call);
            return;
        }

        var content = response.ServerContent;
        if (content == null) return;

        if (content.Interrupted == true) {
            spkr.flush();
            return;
        }

        var parts = content.ModelTurn?.Parts;
        if (parts == null) return;

        for (int i = 0; i < parts.Count; i++) {
            var data = parts[i].InlineData;
            if (data?.Data != null && data.Data.Length > 0 &&
                data.MimeType != null && data.MimeType.StartsWith("audio/pcm")) {
                touchActivity();
                spkr.write(data.Data);
            }
        }
    }

    public static void pushState(string summary) {
        var s = liveSession;
        if (s == null || _status != GeminiStatus.Live || string.IsNullOrEmpty(summary)) return;
        _ = Task.Run(async () => {
            try {
                await s.SendClientContentAsync(new LiveSendClientContentParameters {
                    Turns = new List<Content> {
                        new Content {
                            Role = "user",
                            Parts = new List<Part> {
                                new Part { Text = "[state] " + summary }
                            }
                        }
                    },
                    TurnComplete = false
                }).ConfigureAwait(false);
            }
            catch (Exception e) {
                Debug.LogWarning($"[Gemini] state push failed: {e.Message}");
            }
        });
    }

    public static void setKeepAlive(bool value) {
        keepAlive = value;
    }

    public static void connect() {
        if (client == null) return;
        if (sessionTask != null && !sessionTask.IsCompleted) return;

        touchActivity();
        _status = GeminiStatus.Connecting;
        sessionCts?.Cancel();
        sessionCts = new CancellationTokenSource();
        sessionTask = RunSessionAsync(Interlocked.Increment(ref sessionGeneration), sessionCts.Token);
    }

    public static void disconnect() {
        mute();
        Interlocked.Increment(ref sessionGeneration);
        sessionCts?.Cancel();
        sessionCts = null;
        sessionTask = null;
        if (_status != GeminiStatus.MicDenied && _status != GeminiStatus.Failed)
            _status = GeminiStatus.Off;
    }

    public static void listen() {
        if (!micGranted) {
            _status = GeminiStatus.MicDenied;
            return;
        }
        if (_listening) return;
        _listening = true;
        while (sendQueue.TryDequeue(out _)) { }
        runAudio(() => {
            spkr.start();
            voip.start();
        });
    }

    public static void mute() {
        if (!_listening) return;
        _listening = false;
        while (sendQueue.TryDequeue(out _)) { }
        runAudio(() => {
            spkr.stop();
            voip.stop();
        });
    }

}
