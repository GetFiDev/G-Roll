using System;
using System.Collections.Generic;

public class EventHub<TKey, TPayload>
{
    private readonly Dictionary<TKey, Action<TPayload>> _eventTable = new();

    public void Subscribe(TKey key, Action<TPayload> callback)
    {
        if (_eventTable.ContainsKey(key))
            _eventTable[key] += callback;
        else
            _eventTable[key] = callback;
    }

    public void Unsubscribe(TKey key, Action<TPayload> callback)
    {
        if (_eventTable.TryGetValue(key, out var existing))
        {
            existing -= callback;
            if (existing == null)
                _eventTable.Remove(key);
            else
                _eventTable[key] = existing;
        }
    }

    public void Invoke(TKey key, TPayload payload)
    {
        if (_eventTable.TryGetValue(key, out var callback))
        {
            callback?.Invoke(payload);
        }
    }

    public void ClearAll() => _eventTable.Clear();
}