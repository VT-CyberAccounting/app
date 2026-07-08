using System;
using UnityEngine;

public class GeminiClient : MonoBehaviour {

    [Tooltip("Enable Google Search grounding. Requires a paid-tier (billing-enabled) Gemini API key; leave off on the free tier or the session will be rejected.")]
    public bool enableWebSearch = false;

    [Tooltip("Maximum connection time in minutes: with the assistant on but no conversation for this long, the assistant turns itself off and the session closes. A warning hint appears one minute before. 0 disables the timeout.")]
    public float maximumConnectionTime = 30f;

    private const float PreconnectExpirySeconds = 90f;

    private bool _ready;
    private bool _active;
    private bool _warned;
    private GeminiStatus _lastStatus = GeminiStatus.Off;
    private float _nextStateCheck;
    private string _lastStatePush;

    public bool IsActive => _active;
    public event Action<GeminiStatus> StatusChanged;
    public event Action<int> InactivityWarned;
    public event Action ActivityResumed;
    public event Action InactivityExpired;

    async void Start() {
        await Gemini.init(enableWebSearch);
        if (this == null) return;
        _ready = true;
        Gemini.setKeepAlive(_active);
        if (_active && isActiveAndEnabled) {
            Gemini.connect();
            Gemini.listen();
        }
    }

    public void SetActive(bool active) {
        _active = active;
        _warned = false;
        if (!_ready) return;
        Gemini.setKeepAlive(active);
        if (active) {
            Gemini.connect();
            Gemini.listen();
        }
        else {
            Gemini.mute();
            Gemini.disconnect();
        }
    }

    public void NotifyIntent() {
        if (!_ready || _active) return;
        if (Gemini.Status == GeminiStatus.Off)
            Gemini.connect();
    }

    void Update() {
        if (!_ready) return;
        var status = Gemini.Status;
        if (status != _lastStatus) {
            _lastStatus = status;
            if (status == GeminiStatus.Live) _lastStatePush = null;
            StatusChanged?.Invoke(status);
        }

        if (status != GeminiStatus.Live || Time.unscaledTime < _nextStateCheck) return;
        _nextStateCheck = Time.unscaledTime + 1f;

        float idleSeconds = (float)Gemini.SecondsSinceActivity;
        if (_active && maximumConnectionTime > 0f) {
            float limit = maximumConnectionTime * 60f;
            if (idleSeconds >= limit) {
                _warned = false;
                if (InactivityExpired != null) InactivityExpired();
                else SetActive(false);
                return;
            }
            if (idleSeconds >= limit - 60f && limit > 60f) {
                if (!_warned) {
                    _warned = true;
                    InactivityWarned?.Invoke(Mathf.Max(1, Mathf.RoundToInt(idleSeconds / 60f)));
                }
            }
            else if (_warned) {
                _warned = false;
                ActivityResumed?.Invoke();
            }
        }
        else if (!_active && idleSeconds >= PreconnectExpirySeconds) {
            Gemini.disconnect();
            return;
        }

        string state = SceneTool.StateSummary();
        if (_lastStatePush == null) {
            _lastStatePush = state;
            return;
        }
        if (state == _lastStatePush) return;
        _lastStatePush = state;
        Gemini.pushState(state);
    }

    void OnDisable() {
        if (_ready) Gemini.disconnect();
    }

    void OnDestroy() {
        Gemini.destroy();
    }

    void OnApplicationPause(bool paused) {
        if (!_ready) return;
        if (paused) {
            Gemini.disconnect();
        }
        else if (_active) {
            Gemini.connect();
            Gemini.listen();
        }
    }
}
