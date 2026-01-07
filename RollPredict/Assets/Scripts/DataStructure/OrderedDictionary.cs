using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 有序字典：结合 Dictionary 的 O(1) 访问和 LinkedList 的有序遍历
/// 用于帧同步系统，确保遍历顺序的一致性
/// 
/// 实现原理：
/// - Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> 提供 O(1) 访问
/// - LinkedList<KeyValuePair<TKey, TValue>> 保持插入顺序
/// </summary>
[Serializable]
public class OrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue>
{
    private Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _dictionary;
    private LinkedList<KeyValuePair<TKey, TValue>> _linkedList;

    public OrderedDictionary()
    {
        _dictionary = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>();
        _linkedList = new LinkedList<KeyValuePair<TKey, TValue>>();
    }

    public OrderedDictionary(int capacity)
    {
        _dictionary = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(capacity);
        _linkedList = new LinkedList<KeyValuePair<TKey, TValue>>();
    }

    public OrderedDictionary(IEqualityComparer<TKey> comparer)
    {
        _dictionary = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(comparer);
        _linkedList = new LinkedList<KeyValuePair<TKey, TValue>>();
    }

    public OrderedDictionary(int capacity, IEqualityComparer<TKey> comparer)
    {
        _dictionary = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(capacity, comparer);
        _linkedList = new LinkedList<KeyValuePair<TKey, TValue>>();
    }

    // IDictionary 实现
    public TValue this[TKey key]
    {
        get
        {
            if (_dictionary.TryGetValue(key, out var node))
            {
                return node.Value.Value;
            }
            throw new KeyNotFoundException($"The key '{key}' was not found in the dictionary.");
        }
        set
        {
            if (_dictionary.TryGetValue(key, out var node))
            {
                // 更新现有值，保持位置不变
                node.Value = new KeyValuePair<TKey, TValue>(key, value);
            }
            else
            {
                // 添加新项
                Add(key, value);
            }
        }
    }

    public ICollection<TKey> Keys => _linkedList.Select(kvp => kvp.Key).ToList();
    public ICollection<TValue> Values => _linkedList.Select(kvp => kvp.Value).ToList();
    public int Count => _dictionary.Count;
    public bool IsReadOnly => false;

    public void Add(TKey key, TValue value)
    {
        if (_dictionary.ContainsKey(key))
        {
            return;
        }

        var node = _linkedList.AddLast(new KeyValuePair<TKey, TValue>(key, value));
        _dictionary[key] = node;
    }

    public void Add(KeyValuePair<TKey, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    public void Clear()
    {
        _dictionary.Clear();
        _linkedList.Clear();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        if (_dictionary.TryGetValue(item.Key, out var node))
        {
            return EqualityComparer<TValue>.Default.Equals(node.Value.Value, item.Value);
        }
        return false;
    }

    public bool ContainsKey(TKey key)
    {
        return _dictionary.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0 || arrayIndex >= array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < Count)
            throw new ArgumentException("The destination array is not large enough.");

        int index = arrayIndex;
        foreach (var kvp in _linkedList)
        {
            array[index++] = kvp;
        }
    }

    public bool Remove(TKey key)
    {
        if (_dictionary.TryGetValue(key, out var node))
        {
            _dictionary.Remove(key);
            _linkedList.Remove(node);
            return true;
        }
        return false;
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        if (_dictionary.TryGetValue(item.Key, out var node))
        {
            if (EqualityComparer<TValue>.Default.Equals(node.Value.Value, item.Value))
            {
                return Remove(item.Key);
            }
        }
        return false;
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (_dictionary.TryGetValue(key, out var node))
        {
            value = node.Value.Value;
            return true;
        }
        value = default(TValue);
        return false;
    }

    // IEnumerable 实现
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return _linkedList.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    
    public OrderedDictionary(OrderedDictionary <TKey, TValue> dictionary)
    {
        var temp = dictionary.Clone();
        // 直接复用temp的底层集合（全新实例，无引用共享）
        _dictionary = temp._dictionary;
        _linkedList = temp._linkedList;
    }
    
    /// <summary>
    /// 深拷贝 OrderedDictionary
    /// 注意：TValue 必须是可克隆的类型（实现 Clone 方法）或值类型
    /// </summary>
    public OrderedDictionary<TKey, TValue> Clone()
    {
        var cloned = new OrderedDictionary<TKey, TValue>(_dictionary.Comparer);
        foreach (var kvp in _linkedList)
        {
            // 如果 TValue 实现了 ICloneable，使用 Clone
            if (kvp.Value is ICloneable cloneable)
            {
                cloned.Add(kvp.Key, (TValue)cloneable.Clone());
            }
            else
            {
                // 对于值类型 （浅拷贝）或引用类型 （非常不建议）
                cloned.Add(kvp.Key, kvp.Value);
            }
        }
        return cloned;
    }
}

