using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class StateEvent<T> : ScriptableObject {
    private event UnityAction<T> OnEventRaised;
    [SerializeField]
    private T defaultValue;
    private T _state;
    private static Dictionary<string, StateEvent<T>> events = new Dictionary<string, StateEvent<T>>();

    // TODO: Validate effectiveness of approach
    public void OnAfterDeserialize() {
        _state = defaultValue;
    }

    private void OnEnable() {
        if (!events.ContainsKey(this.name)) {
            Debug.Log($"Indexing event {this.name}");
            events.Add(this.name, this);
        }
    }
    
    private void OnDisable() {
        events.Remove(this.name);
    }
    
    public void Raise(T value) {
        if (!this._state.Equals(value)) {
            this._state = value;
            OnEventRaised?.Invoke(value);
        }
    }
    
    public static StateEvent<T> operator +(StateEvent<T> onEvent, UnityAction<T> handler) {
        Debug.Log($"Adding handler {handler.Method.Name} to event {onEvent.name}");
        onEvent.OnEventRaised += handler;
        return onEvent;
    }
    
    public static StateEvent<T> operator -(StateEvent<T> onEvent, UnityAction<T> handler) {
        onEvent.OnEventRaised -= handler;
        return onEvent;
    }
    
    public T Get() {
        return this._state;
    }
    
    public void Set(T value) {
        Raise(value);
    }
    
    public static StateEvent<T> Get(string eventName) {
        if (events.TryGetValue(eventName, out var stateEvent)) {
            return stateEvent;
        }
        Debug.LogWarning($"StateEvent with name '{eventName}' not found.");
        return null;
    }
}