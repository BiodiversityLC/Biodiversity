using System.Collections.Generic;

namespace Biodiversity.Util.DataStructures;

public class StateData
{
    private readonly Dictionary<string, object> _data = new();

    public void Add(string key, object value)
    {
        _data[key] = value;
    }

    public bool Remove(string key)
    {
        return _data.Remove(key);
    }

    public T Get<T>(string key)
    {
        if (_data.TryGetValue(key, out object value) && value is T typedValue)
        {
            return typedValue;
        }

        return default;
    }

    public bool TryGet<T>(string key, out T value)
    {
        if (_data.TryGetValue(key, out object objValue) && objValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    public bool ContainsKey(string key)
    {
        return _data.ContainsKey(key);
    }

    public void Clear()
    {
        _data.Clear();
    }
}