using System.Collections.Generic;

namespace Biodiversity.Creatures.Aloe.Types;

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
    
    public bool ContainsKey(string key)
    {
        return _data.ContainsKey(key);
    }

    public void Clear()
    {
        _data.Clear();
    }
}