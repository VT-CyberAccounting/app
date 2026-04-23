using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;
using UnityEngine;

public static class Gemini {

    private static AsyncSession session;
    private static Client client;
    private static readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
    private static CancellationTokenSource loopCts;
    private static Task loopTask;
    private static bool initialized;

    public static async Task init() {
        try {
            client = new Client(apiKey: "");
            Debug.Log("gemini init");
            session = await client.Live.ConnectAsync(
                model: "gemini-2.5-flash-native-audio-preview-12-2025",
                config: new LiveConnectConfig {
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
                    }
                }
            );
            Debug.Log("session init");
            spkr.init();
            Debug.Log("spkr init");
            voip.init();
            Debug.Log("voip init");
            voip.turret += sendTick;
            initialized = true;
        }
        catch (Exception e) {
            Debug.LogError(e);
        }
    }

    public static async void destroy() {
        await destroyAsync();
    }

    public static async Task destroyAsync() {
        if (!initialized) return;
        initialized = false;

        voip.turret -= sendTick;

        try {
            loopCts?.Cancel();
        } catch (Exception e) { Debug.LogError(e); }

        try {
            if (session != null) await session.CloseAsync();
        } catch (Exception e) { Debug.LogError(e); }
        session = null;

        try {
            if (loopTask != null) await loopTask;
        } catch (Exception) { /* expected on cancel/close */ }
        loopTask = null;

        try {
            spkr.destroy();
        } catch (Exception e) { Debug.LogError(e); }

        try {
            voip.destroy();
        } catch (Exception e) { Debug.LogError(e); }

        loopCts?.Dispose();
        loopCts = null;
    }

    private static async void sendTick(byte[] data) {
        if (session == null || data == null || data.Length == 0) return;

        await sendLock.WaitAsync();
        try {
            if (session == null) return;
            Blob blob = new Blob {
                MimeType = "audio/pcm;rate=16000",
                Data = data
            };
            await session.SendRealtimeInputAsync(new LiveSendRealtimeInputParameters {
                Audio = blob
            });
        }
        catch (Exception e) {
            Debug.LogError(e);
        }
        finally {
            sendLock.Release();
        }
    }

    private static async Task receiveLoop(CancellationToken ct) {
        try {
            while (!ct.IsCancellationRequested) {
                LiveServerMessage response = await session.ReceiveAsync();
                if (response == null) {
                    Debug.Log("session closed");
                    break;
                }
                receiveTick(response);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) {
            Debug.LogError($"receiveLoop fatal: {e}");
        }
    }

    private static void receiveTick(LiveServerMessage response) {
        if (response.SetupComplete != null) {
            Debug.Log("[Gemini] SetupComplete received — session is ready");
            return;
        }

        if (response.ToolCall != null) {
            Debug.Log($"[Gemini] ToolCall received — {response.ToolCall.FunctionCalls?.Count ?? 0} function call(s)");
            return;
        }

        var serverContent = response.ServerContent;
        if (serverContent == null) {
            Debug.LogWarning("[Gemini] receiveTick: ServerContent is null (unknown message type?)");
            return;
        }

        if (serverContent.Interrupted == true) {
            Debug.Log("[Gemini] interrupt signal");
        }

        if (serverContent.InputTranscription != null) {
            Debug.Log($"[Gemini] InputTranscription (model heard): \"{serverContent.InputTranscription.Text}\"");
        }

        if (serverContent.OutputTranscription != null) {
            Debug.Log($"[Gemini] OutputTranscription: \"{serverContent.OutputTranscription.Text}\"");
        }

        var parts = serverContent.ModelTurn?.Parts;
        if (parts == null || parts.Count == 0) {
            Debug.Log("[Gemini] ServerContent.ModelTurn.Parts is null/empty — no audio/text payload this tick");
            return;
        }

        Debug.Log($"[Gemini] ModelTurn has {parts.Count} part(s)");
        for (int i = 0; i < parts.Count; i++) {
            var part = parts[i];
            string mimeType = part.InlineData?.MimeType ?? "(null)";
            int dataLen  = part.InlineData?.Data?.Length ?? 0;
            string text  = part.Text ?? "(null)";

            Debug.Log($"[Gemini]   Part[{i}]: " +
                      $"mimeType={mimeType}, " +
                      $"dataBytes={dataLen}, " +
                      $"text={text}");

            if (part.InlineData?.MimeType?.StartsWith("audio/pcm") == true) {
                if (part.InlineData.Data == null || part.InlineData.Data.Length == 0) {
                    Debug.LogWarning($"[Gemini]   Part[{i}] is audio/pcm but Data is null/empty — skipping spkr.write");
                } else {
                    Debug.Log($"[Gemini]   Part[{i}] → spkr.write({part.InlineData.Data.Length} bytes)");
                    spkr.write(part.InlineData.Data);
                }
            }
        }
    }

    public static void start() {
        if (!initialized || session == null) return;
        spkr.start();
        voip.start();
        loopCts = new CancellationTokenSource();
        loopTask = receiveLoop(loopCts.Token);
    }

    public static void stop() {
        if (!initialized) return;
        try { spkr.stop(); } catch (Exception e) { Debug.LogError(e); }
        try { voip.stop(); } catch (Exception e) { Debug.LogError(e); }
    }
}
