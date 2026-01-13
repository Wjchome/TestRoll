using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DataStructure
{
    /// <summary>
    /// 通用环形缓冲区（实现IDictionary<TKey, TValue>，满了自动删除最早添加的元素）
    /// 1.增删改查 O(1)
    /// 2.到达最大容量实现删除最开始增加元素 O(1)
    /// 3.可以看成Dic + 有最大容量的 环形缓冲区
    /// </summary>
    /// <typeparam name="TKey">键类型（需唯一，如帧号）</typeparam>
    /// <typeparam name="TValue">值类型（如快照对象）</typeparam>
    public class CircularBuffer<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly int _capacity; // 缓冲区最大容量
        private readonly Dictionary<TKey, TValue> _innerDict; // 核心键值存储（O(1)查找）
        private readonly Queue<TKey> _keyQueue; // 记录Key的插入顺序（保证删除最早元素）

        #region 构造函数
        /// <summary>
        /// 初始化环形缓冲区
        /// </summary>
        /// <param name="capacity">最大容量（必须>0）</param>
        public CircularBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "缓冲区容量必须大于0");

            _capacity = capacity;
            _innerDict = new Dictionary<TKey, TValue>(capacity);
            _keyQueue = new Queue<TKey>(capacity);
        }

        /// <summary>
        /// 初始化环形缓冲区（指定键比较器）
        /// </summary>
        /// <param name="capacity">最大容量</param>
        /// <param name="comparer">键的比较器（如忽略大小写的字符串比较）</param>
        public CircularBuffer(int capacity, IEqualityComparer<TKey> comparer)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "缓冲区容量必须大于0");

            _capacity = capacity;
            _innerDict = new Dictionary<TKey, TValue>(capacity, comparer);
            _keyQueue = new Queue<TKey>(capacity);
        }
        #endregion

        #region 核心属性（IDictionary实现）
        /// <summary>
        /// 当前缓冲区元素数量
        /// </summary>
        public int Count => _innerDict.Count;

        /// <summary>
        /// 是否只读（固定为false，支持修改）
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// 所有键的集合
        /// </summary>
        public ICollection<TKey> Keys => _innerDict.Keys;

        /// <summary>
        /// 所有值的集合
        /// </summary>
        public ICollection<TValue> Values => _innerDict.Values;

        /// <summary>
        /// 索引器：按Key访问/设置值（设置时自动处理容量）
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>对应的值</returns>
        /// <exception cref="KeyNotFoundException">键不存在时抛出</exception>
        public TValue this[TKey key]
        {
            get => _innerDict[key];
            set
            {
                // 1. 如果Key已存在，先移除旧的（保证顺序正确）
                if (_innerDict.ContainsKey(key))
                {
                    Remove(key);
                }
                // 2. 添加新值（自动处理容量）
                Add(key, value);
            }
        }
        #endregion

        #region 核心方法（IDictionary实现 + 环形逻辑）
        /// <summary>
        /// 添加键值对（核心：满了自动删除最早元素）
        /// </summary>
        /// <param name="key">键（必须唯一）</param>
        /// <param name="value">值</param>
        /// <exception cref="ArgumentNullException">键为null时抛出</exception>
        /// <exception cref="ArgumentException">键已存在时抛出（如需覆盖用索引器）</exception>
        public void Add(TKey key, TValue value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key), "键不能为null");

            // 检查键是否已存在（IDictionary规范：Add不允许重复键，如需覆盖用索引器）
            if (_innerDict.ContainsKey(key))
                throw new ArgumentException($"键 {key} 已存在于缓冲区中", nameof(key));

            // 核心：缓冲区满了，删除最早添加的元素
            while (_innerDict.Count >= _capacity)
            {
                var oldestKey = _keyQueue.Dequeue(); // 取出最早的Key
                _innerDict.Remove(oldestKey); // 从字典移除对应键值
                // 可选：触发元素被移除的事件（如需监听）
                // OnElementRemoved?.Invoke(oldestKey, removedValue);
            }

            // 添加新元素到字典和队列
            _innerDict.Add(key, value);
            _keyQueue.Enqueue(key);
        }

        /// <summary>
        /// 添加KeyValuePair类型的键值对（适配IDictionary接口）
        /// </summary>
        /// <param name="item">键值对</param>
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        /// <summary>
        /// 清空缓冲区
        /// </summary>
        public void Clear()
        {
            _innerDict.Clear();
            _keyQueue.Clear();
        }

        /// <summary>
        /// 检查是否包含指定的键值对
        /// </summary>
        /// <param name="item">键值对</param>
        /// <returns>是否包含</returns>
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ((IDictionary<TKey, TValue>)_innerDict).Contains(item);
        }

        /// <summary>
        /// 检查是否包含指定键
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>是否包含</returns>
        public bool ContainsKey(TKey key)
        {
            return _innerDict.ContainsKey(key);
        }

        /// <summary>
        /// 复制缓冲区元素到数组
        /// </summary>
        /// <param name="array">目标数组</param>
        /// <param name="arrayIndex">数组起始索引</param>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((IDictionary<TKey, TValue>)_innerDict).CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// 移除指定键的元素
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>是否移除成功</returns>
        public bool Remove(TKey key)
        {
            if (key == null)
                return false;

            // 先从字典移除，再从队列移除（队列需遍历找到对应Key）
            var removed = _innerDict.Remove(key);
            if (removed)
            {
                // 重建队列（Queue不支持直接移除指定元素，效率可接受的场景下用此方式）
                var newQueue = new Queue<TKey>(_keyQueue.Where(k => !k.Equals(key)));
                _keyQueue.Clear();
                foreach (var k in newQueue)
                {
                    _keyQueue.Enqueue(k);
                }
            }
            return removed;
        }

        /// <summary>
        /// 移除指定的键值对（适配IDictionary接口）
        /// </summary>
        /// <param name="item">键值对</param>
        /// <returns>是否移除成功</returns>
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (Contains(item) && Remove(item.Key))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 尝试获取指定键的值（O(1)复杂度）
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">输出值（未找到则为默认值）</param>
        /// <returns>是否找到</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            return _innerDict.TryGetValue(key, out value);
        }
        #endregion

        #region 枚举器（IEnumerable实现）
        /// <summary>
        /// 获取枚举器（按插入顺序遍历）
        /// </summary>
        /// <returns>枚举器</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            // 按队列的Key顺序遍历，保证和插入顺序一致
            foreach (var key in _keyQueue)
            {
                if (_innerDict.TryGetValue(key, out var value))
                {
                    yield return new KeyValuePair<TKey, TValue>(key, value);
                }
            }
        }

        /// <summary>
        /// 非泛型枚举器（适配IEnumerable接口）
        /// </summary>
        /// <returns>非泛型枚举器</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion

        #region 扩展方法（可选）
        /// <summary>
        /// 获取最早添加的元素（未找到返回默认值）
        /// </summary>
        /// <returns>最早的键值对</returns>
        public KeyValuePair<TKey, TValue>? GetOldestElement()
        {
            if (_keyQueue.Count == 0)
                return null;

            var oldestKey = _keyQueue.Peek();
            if (_innerDict.TryGetValue(oldestKey, out var value))
            {
                return new KeyValuePair<TKey, TValue>(oldestKey, value);
            }
            return null;
        }

        /// <summary>
        /// 获取缓冲区最大容量
        /// </summary>
        public int Capacity => _capacity;
        #endregion
    }
}