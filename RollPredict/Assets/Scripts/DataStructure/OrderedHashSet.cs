using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 有序哈希集合：结合 HashSet 的 O(1) 存在性检查和 LinkedList 的有序遍历
/// 用于帧同步系统，确保遍历顺序的一致性（普通HashSet遍历顺序不保证）
/// 
/// 实现原理：
/// - Dictionary<T, LinkedListNode<T>> 提供 O(1) 元素访问/存在性检查
/// - LinkedList<T> 保持元素的插入顺序
/// </summary>
[Serializable]
public class OrderedHashSet<T> : ISet<T>
{
    // 核心存储：Dictionary映射元素到链表节点（O(1)查找），LinkedList保持插入顺序
    private readonly Dictionary<T, LinkedListNode<T>> _dictionary;
    private readonly LinkedList<T> _linkedList;
    // 比较器（用于元素相等判断，适配自定义类型）
    private readonly IEqualityComparer<T> _comparer;

    #region 构造函数（对齐OrderedDictionary设计，适配帧同步容量/比较器需求）
    public OrderedHashSet() : this(EqualityComparer<T>.Default) { }

    public OrderedHashSet(int capacity) : this(capacity, EqualityComparer<T>.Default) { }

    public OrderedHashSet(IEqualityComparer<T> comparer)
    {
        _comparer = comparer ?? EqualityComparer<T>.Default;
        _dictionary = new Dictionary<T, LinkedListNode<T>>(_comparer);
        _linkedList = new LinkedList<T>();
    }

    public OrderedHashSet(int capacity, IEqualityComparer<T> comparer)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "容量不能为负数");
        
        _comparer = comparer ?? EqualityComparer<T>.Default;
        _dictionary = new Dictionary<T, LinkedListNode<T>>(capacity, _comparer);
        _linkedList = new LinkedList<T>();
    }

    // 克隆构造函数（深拷贝，适配帧同步快照需求）
    public OrderedHashSet(OrderedHashSet<T> other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));
        
        _comparer = other._comparer;
        _dictionary = new Dictionary<T, LinkedListNode<T>>(other._dictionary.Count, _comparer);
        _linkedList = new LinkedList<T>();

        // 遍历原链表（保证顺序），深拷贝元素
        foreach (var item in other._linkedList)
        {
            T clonedItem = item;
            // 兼容ICloneable接口的深拷贝（帧同步中组件/数据常需深拷贝）
            if (item is ICloneable cloneable)
            {
                clonedItem = (T)cloneable.Clone();
            }
            var newNode = _linkedList.AddLast(clonedItem);
            _dictionary[clonedItem] = newNode;
        }
    }

    // 从现有集合初始化（保持传入集合的遍历顺序）
    public OrderedHashSet(IEnumerable<T> collection) : this(collection, EqualityComparer<T>.Default) { }

    public OrderedHashSet(IEnumerable<T> collection, IEqualityComparer<T> comparer)
    {
        if (collection == null)
            throw new ArgumentNullException(nameof(collection));
        
        _comparer = comparer ?? EqualityComparer<T>.Default;
        // 预估容量，减少扩容（帧同步减少GC）
        int capacity = collection is ICollection<T> coll ? coll.Count : 0;
        _dictionary = new Dictionary<T, LinkedListNode<T>>(capacity, _comparer);
        _linkedList = new LinkedList<T>();

        // 按传入顺序添加（去重）
        foreach (var item in collection)
        {
            Add(item);
        }
    }
    #endregion

    #region 核心属性（适配帧同步状态查看）
    /// <summary>
    /// 元素数量（O(1)，直接取Dictionary.Count）
    /// </summary>
    public int Count => _dictionary.Count;

    /// <summary>
    /// 是否只读（固定为false，帧同步需修改）
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// 按插入顺序获取所有元素（帧同步遍历专用）
    /// </summary>
    public IEnumerable<T> OrderedItems => _linkedList;
    #endregion

    #region ISet<T> 核心方法（帧同步核心操作）
    /// <summary>
    /// 添加元素（已存在则忽略，保持插入顺序）
    /// </summary>
    /// <param name="item">要添加的元素</param>
    /// <returns>是否成功添加（不存在则返回true）</returns>
    public bool Add(T item)
    {
        if (_dictionary.ContainsKey(item))
            return false;

        var node = _linkedList.AddLast(item);
        _dictionary[item] = node;
        return true;
    }

    /// <summary>
    /// 显式实现ICollection<T>.Add（兼容基础集合接口）
    /// </summary>
    void ICollection<T>.Add(T item) => Add(item);

    /// <summary>
    /// 清空所有元素（O(n)，帧同步重置专用）
    /// </summary>
    public void Clear()
    {
        _dictionary.Clear();
        _linkedList.Clear();
    }

    /// <summary>
    /// 检查元素是否存在（O(1)，帧同步高频操作）
    /// </summary>
    public bool Contains(T item) => _dictionary.ContainsKey(item);

    /// <summary>
    /// 移除元素（O(1)，移除后保持剩余元素的插入顺序）
    /// </summary>
    public bool Remove(T item)
    {
        if (_dictionary.TryGetValue(item, out var node))
        {
            _dictionary.Remove(item);
            _linkedList.Remove(node);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 复制到数组（按插入顺序，适配帧同步数据快照）
    /// </summary>
    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0 || arrayIndex >= array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < Count)
            throw new ArgumentException("目标数组空间不足，无法容纳所有元素");

        int index = arrayIndex;
        foreach (var item in _linkedList)
        {
            array[index++] = item;
        }
    }
    #endregion

    #region ISet<T> 集合运算方法（可选，帧同步按需使用）
    public void UnionWith(IEnumerable<T> other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));
        
        foreach (var item in other)
        {
            Add(item);
        }
    }

    public void IntersectWith(IEnumerable<T> other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));
        
        var otherSet = new HashSet<T>(other, _comparer);
        var toRemove = _dictionary.Keys.Where(key => !otherSet.Contains(key)).ToList();
        foreach (var item in toRemove)
        {
            Remove(item);
        }
    }

    public void ExceptWith(IEnumerable<T> other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));
        
        foreach (var item in other)
        {
            Remove(item);
        }
    }

    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));
        
        var tempSet = new HashSet<T>(other, _comparer);
        foreach (var item in tempSet)
        {
            if (Contains(item))
                Remove(item);
            else
                Add(item);
        }
    }

    public bool IsSubsetOf(IEnumerable<T> other)
    {
        var otherSet = new HashSet<T>(other, _comparer);
        return _dictionary.Keys.All(otherSet.Contains);
    }

    public bool IsSupersetOf(IEnumerable<T> other)
    {
        var otherSet = new HashSet<T>(other, _comparer);
        return otherSet.All(_dictionary.ContainsKey);
    }

    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        var otherSet = new HashSet<T>(other, _comparer);
        return Count < otherSet.Count && IsSubsetOf(otherSet);
    }

    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        var otherSet = new HashSet<T>(other, _comparer);
        return Count > otherSet.Count && IsSupersetOf(otherSet);
    }

    public bool Overlaps(IEnumerable<T> other)
    {
        var otherSet = new HashSet<T>(other, _comparer);
        return _dictionary.Keys.Any(otherSet.Contains);
    }

    public bool SetEquals(IEnumerable<T> other)
    {
        var otherSet = new HashSet<T>(other, _comparer);
        return Count == otherSet.Count && IsSubsetOf(otherSet);
    }
    #endregion

    #region 遍历与克隆（帧同步核心需求）
    /// <summary>
    /// 获取按插入顺序的枚举器（帧同步遍历专用）
    /// </summary>
    public IEnumerator<T> GetEnumerator() => _linkedList.GetEnumerator();

    /// <summary>
    /// 非泛型枚举器（兼容基础集合）
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// 深拷贝 OrderedHashSet（帧同步快照/回滚专用）
    /// 注意：T 为引用类型时需实现 ICloneable 接口，否则为浅拷贝
    /// </summary>
    public OrderedHashSet<T> Clone() => new OrderedHashSet<T>(this);
    #endregion

    #region 辅助方法（调试/日志专用）
    /// <summary>
    /// 友好的字符串输出（按插入顺序，便于帧同步调试）
    /// </summary>
    public override string ToString()
    {
        if (Count == 0)
        {
            return $"OrderedHashSet<{typeof(T).Name}> [Count=0] {{ }}";
        }

        var itemStrings = _linkedList.Select(item => 
            item?.ToString() ?? "null").ToList();
        
        return $"OrderedHashSet<{typeof(T).Name}> [Count={Count}] {{ {string.Join(", ", itemStrings)} }}";
    }
    #endregion
}