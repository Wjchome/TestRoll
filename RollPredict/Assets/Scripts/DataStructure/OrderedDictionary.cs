using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 有序字典：结合 Dictionary 的 O(1) 访问和 LinkedList 的有序遍历
/// 用于帧同步系统，确保遍历顺序的一致性
/// 1. 有序遍历
/// 2. 尾增 随机删 随机改 随机查找 O(1)
/// 3. 可以看作是一个 Dictionary + 有序遍历 
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
    
    public OrderedDictionary(OrderedDictionary<TKey, TValue> dictionary)
    {
        // 修复原克隆构造函数的逻辑问题：原代码调用temp.Clone()会重复创建，改为直接克隆底层数据
        _dictionary = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(dictionary._dictionary.Comparer);
        _linkedList = new LinkedList<KeyValuePair<TKey, TValue>>();
        
        foreach (var kvp in dictionary._linkedList)
        {
            TValue clonedValue = kvp.Value;
            // 兼容ICloneable的深拷贝
            if (kvp.Value is ICloneable cloneable)
            {
                clonedValue = (TValue)cloneable.Clone();
            }
            var newNode = _linkedList.AddLast(new KeyValuePair<TKey, TValue>(kvp.Key, clonedValue));
            _dictionary[kvp.Key] = newNode;
        }
    }
    
    /// <summary>
    /// 深拷贝 OrderedDictionary
    /// 注意：TValue 必须是可克隆的类型（实现 Clone 方法）或值类型
    /// </summary>
    public OrderedDictionary<TKey, TValue> Clone()
    {
        return new OrderedDictionary<TKey, TValue>(this);
    }
    public override string ToString()
    {
        // 处理空字典
        if (Count == 0)
        {
            return $"OrderedDictionary<{typeof(TKey).Name}, {typeof(TValue).Name}> [Count=0] {{ }}";
        }

        // 拼接有序键值对（保持插入顺序，符合帧同步遍历逻辑）
        var kvpStrings = new List<string>();
        foreach (var kvp in _linkedList)
        {
            string keyStr = kvp.Key?.ToString() ?? "null";
            string valueStr = kvp.Value?.ToString() ?? "null";
            // 对复杂类型（如结构体Component），保证Value的ToString能显示关键字段
            kvpStrings.Add($"{keyStr}: {valueStr}");
        }

        // 最终格式：类型[数量] { 键值对列表 }
        return $"OrderedDictionary<{typeof(TKey).Name}, {typeof(TValue).Name}> [Count={Count}] {{ {string.Join(", ", kvpStrings)} }}";
    }
}

