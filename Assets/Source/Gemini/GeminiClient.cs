using UnityEngine;

public class GeminiClient : MonoBehaviour {
    
    async void Start() {
        await Gemini.init();
        Debug.Log("GClient init");
        Gemini.start();
        Debug.Log("GClient start");
    }

    // void OnEnable() {
    //     Debug.Log("GClient OnEnable");
    //     Gemini.start();
    // }

    void OnDisable() {
        Gemini.stop();
        Debug.Log("GClient OnDisable");
    }

    void OnDestroy() {
        Gemini.destroy();
        Debug.Log("GClient OnDestroy");
    }
}
