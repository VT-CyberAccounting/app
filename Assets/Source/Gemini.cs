using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;
using UnityEngine;

public static class Gemini {

    private static AsyncSession session;
    private static Client client;
    private static Blob blob;
    
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
            blob = new Blob
            {
                MimeType = "audio/pcm;rate=16000",
                Data = null
            };
            voip.turret += sendTick;
        } 
        catch (Exception e) {
            Debug.LogError(e);
        }
    }
    
    public static async void destroy() {
        try {
            await session.CloseAsync();
            spkr.destroy();
            voip.destroy();
            voip.turret -= sendTick;
        }
        catch (Exception e) {
            Debug.LogError(e);
        }
    }
    
    private static async void sendTick(byte[] data) {
        Debug.Log("[Gemini] streaming audio");
        try {
            blob.Data = data;
            await session.SendRealtimeInputAsync(new LiveSendRealtimeInputParameters
            {
                Audio = blob
            });
        }
        catch (Exception e) {
            Debug.LogError(e);
        }
    }

    private static async Task receiveLoop() {
        try {
            while (true) {
                LiveServerMessage response = await session.ReceiveAsync();
                if (response == null) {
                    Debug.Log("session closed");
                    break;
                }
                receiveTick(response);
                // await Task.Delay(200);
            }
        }
        catch (Exception e) {
            Debug.LogError($"receiveLoop fatal: {e}");
        }
    } 
    
    private static void receiveTick(LiveServerMessage response) {
        // --- Top-level message type triage ---
        if (response.SetupComplete != null) {
            Debug.Log("[Gemini] SetupComplete received — session is ready");
            return;
        }

        if (response.ToolCall != null) {
            Debug.Log($"[Gemini] ToolCall received — {response.ToolCall.FunctionCalls?.Count ?? 0} function call(s)");
            return;
        }

        // --- ServerContent block ---
        var serverContent = response.ServerContent;
        if (serverContent == null) {
            Debug.LogWarning("[Gemini] receiveTick: ServerContent is null (unknown message type?)");
            return;
        }

        if (serverContent.Interrupted == true) {
            Debug.Log("[Gemini] interrupt signal");
        }

        // --- InputTranscription (model heard you speak) ---
        if (serverContent.InputTranscription != null) {
            Debug.Log($"[Gemini] InputTranscription (model heard): \"{serverContent.InputTranscription.Text}\"");
        }

        // --- OutputTranscription (what the model is saying) ---
        if (serverContent.OutputTranscription != null) {
            Debug.Log($"[Gemini] OutputTranscription: \"{serverContent.OutputTranscription.Text}\"");
        }

        // --- ModelTurn parts ---
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
    
    // private static void receiveTick(LiveServerMessage response) {
    //     Debug.Log("received server response");
    //     List<Part> parts = response?.ServerContent?.ModelTurn?.Parts;
    //     if (parts == null) {
    //         return;
    //     }
    //     parts.ForEach(
    //         (part) => {
    //             if (part.InlineData?.MimeType?.StartsWith("audio/pcm") == true) {
    //                 spkr.write(part.InlineData.Data);
    //             }
    //         }
    //     );
    // }
    
    public static void start() {
        spkr.start();
        voip.start();
        _ = receiveLoop();
    }
    
    public static void stop() {
        spkr.stop();
        voip.stop();
    }
    
}