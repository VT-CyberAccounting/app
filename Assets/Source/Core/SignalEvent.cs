using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "NewSignalEvent", menuName = "AppEvents/SignalEvent")]
public class SignalEvent : ScriptableObject {
    private event UnityAction OnEventRaised;
    private static Dictionary<string, SignalEvent> events = new Dictionary<string, SignalEvent>();

    private void OnEnable() {
        if (!events.ContainsKey(this.name)) {
            Debug.Log($"Indexing event {this.name}");
            events.Add(this.name, this);
        }
    }
    
    private void OnDisable() {
        events.Remove(this.name);
    }
    
    public void Raise() {
        OnEventRaised?.Invoke();
    }
    
    public static SignalEvent operator +(SignalEvent onEvent, UnityAction handler) {
        Debug.Log($"Adding handler {handler.Method.Name} to event {onEvent.name}");
        onEvent.OnEventRaised += handler;
        return onEvent;
    }
    
    public static SignalEvent operator -(SignalEvent onEvent, UnityAction handler) {
        onEvent.OnEventRaised -= handler;
        return onEvent;
    }
    
    public static SignalEvent Get(string eventName) {
        if (events.TryGetValue(eventName, out var appEvent)) {
            return appEvent;
        }
        Debug.LogWarning($"SignalEvent with name '{eventName}' not found.");
        return null;
    }
}
