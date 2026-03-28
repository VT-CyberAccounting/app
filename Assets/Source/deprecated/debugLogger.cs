using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class debugLogger : MonoBehaviour {
    [DllImport("voip", EntryPoint = "logTest")]
    private static extern void logTest();

    [DllImport("voip", EntryPoint = "aaAudioSupport")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool aaAudioSupport();

    private void Start() {
        Debug.Log("Debug logger initialized");

        try {
            logTest();
            bool isAudioSupported = aaAudioSupport();
            Debug.Log($"aaAudioSupport(): {isAudioSupported}");
        }
        catch (DllNotFoundException ex) {
            Debug.LogError($"Native library not found: voip. {ex.Message}");
        }
        catch (EntryPointNotFoundException ex) {
            Debug.LogError($"Native function entry point missing. {ex.Message}");
        }
    }
}