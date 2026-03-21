// using UnityEngine;
// using UnityEngine.Android;
// using UnityEngine.SceneManagement;
//
// public static class VoIPInterface {
//     
//     private static AndroidJavaClass AudioRecord;
//     // private static AndroidJavaClass AudioFormat;
//     // private static AndroidJavaClass AudioSource;
//
//     private static bool active;
//     private static bool recording;
//     private static AndroidJavaObject audioRecord;
//     public const int sampleRate = 16000;
//     private static int audioSource;
//     private static int channelConfig;
//     private static int audioFormat;
//     private static int readMode;
//     private static int bufferSize;
//
//     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
//     public static void init() {
//         AudioRecord = new AndroidJavaClass("android.media.AudioRecord");
//         // AudioFormat = new AndroidJavaClass("android.media.AudioFormat");
//         // AudioSource = new AndroidJavaClass("android.media.MediaRecorder$AudioSource");
//
//         audioSource = 7; // AudioSource.GetStatic<int>("VOICE_COMMUNICATION");
//         channelConfig = 16; // AudioFormat.GetStatic<int>("CHANNEL_IN_MONO");
//         audioFormat = 2; // AudioFormat.GetStatic<int>("ENCODING_PCM_16_BIT");
//         readMode = 1; // AudioRecord.GetStatic<int>("READ_NON_BLOCKING");
//         bufferSize = 2 * AudioRecord.CallStatic<int>("getMinBufferSize", sampleRate, channelConfig, audioFormat);
//         
//         if (active) {
//             return;
//         }
//         if (!Permission.HasUserAuthorizedPermission(Permission.Microphone)) {
//             Permission.RequestUserPermission(Permission.Microphone);
//         }
//         audioRecord = new AndroidJavaObject("android.media.AudioRecord", audioSource, sampleRate, channelConfig, audioFormat, bufferSize);
//         active = true;
//         SceneManager.sceneUnloaded += destroy;
//     }
//     
//     public static void start() {
//         if (!active) {
//             return;
//         }
//         audioRecord.Call("startRecording");
//         recording = true;
//     }
//     
//     public static int read(short[] buffer, int offset, int count) {
//         if (!active || !recording) {
//             return -1;
//         }
//         return audioRecord.Call<int>("read", buffer, offset, count, readMode);
//     }
//     
//     public static void stop() {
//         if (!active) {
//             return;
//         }
//         audioRecord.Call("stop");
//         recording = false;
//     }
//     
//     public static void destroy(Scene scene) {
//         if (!active) {
//             return;
//         }
//         if (recording) {
//             stop();
//         }
//         audioRecord.Call("release");
//         active = false;
//     }
// }