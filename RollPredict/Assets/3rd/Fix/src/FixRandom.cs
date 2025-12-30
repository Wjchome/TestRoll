using System;
using System.Collections.Generic;
using Frame.FixMath;

namespace Frame.FixMath
{
    /// <summary>
    /// 确定性随机数生成器（用于帧同步）
    /// 使用线性同余生成器（LCG）算法，确保在不同平台上产生相同的随机数序列
    /// </summary>
    public class FixRandom
    {
        private long _seed;
        private const long MODULUS = 2147483647L; // 2^31 - 1 (质数)
        private const long MULTIPLIER = 1103515245L; // LCG乘数
        private const long INCREMENT = 12345L; // LCG增量

        /// <summary>
        /// 当前种子值
        /// </summary>
        public long Seed => _seed;
        

        /// <summary>
        /// 构造函数（使用指定种子）
        /// </summary>
        /// <param name="seed">随机数种子</param>
        public FixRandom(long seed)
        {
            _seed = seed % MODULUS;
            if (_seed < 0)
            {
                _seed += MODULUS;
            }
        }

        /// <summary>
        /// 生成下一个随机数（0 到 MODULUS-1 之间的整数）
        /// 使用线性同余生成器（LCG）算法
        /// 公式：seed = (multiplier * seed + increment) % modulus
        /// </summary>
        private long Next()
        {
            // 使用长整型避免溢出
            _seed = ((MULTIPLIER * _seed + INCREMENT) % MODULUS + MODULUS) % MODULUS;
            return _seed;
        }

        /// <summary>
        /// 返回一个 0.0 到 1.0 之间的随机浮点数（Fix64）
        /// </summary>
        /// <returns>0.0 到 1.0 之间的随机数</returns>
        public Fix64 Next(Fix64 minValue, Fix64 maxValue)
        {
            if (minValue >= maxValue)
            {
                throw new ArgumentException("minValue必须小于maxValue");
            }

            Fix64 range = maxValue - minValue;
            Fix64 randomValue = Fix64.FromRaw( Next());
            return minValue + (randomValue % range);
        }

        /// <summary>
        /// 返回一个指定范围内的随机整数
        /// </summary>
        /// <param name="minValue">最小值（包含）</param>
        /// <param name="maxValue">最大值（不包含）</param>
        /// <returns>minValue 到 maxValue-1 之间的随机整数</returns>
        public int Next(int minValue, int maxValue)
        {
            if (minValue >= maxValue)
            {
                throw new ArgumentException("minValue必须小于maxValue");
            }

            long range = (long)maxValue - minValue;
            long randomValue = Next();
            return minValue + (int)(randomValue % range);
        }

        /// <summary>
        /// 返回一个 0 到 maxValue-1 之间的随机整数
        /// </summary>
        /// <param name="maxValue">最大值（不包含）</param>
        /// <returns>0 到 maxValue-1 之间的随机整数</returns>
        public int Next(int maxValue)
        {
            if (maxValue <= 0)
            {
                throw new ArgumentException("maxValue必须大于0");
            }

            long randomValue = Next();
            return (int)(randomValue % maxValue);
        }



        /// <summary>
        /// 返回一个 0 到 maxValue 之间的随机Fix64
        /// </summary>
        /// <param name="maxValue">最大值（不包含）</param>
        /// <returns>0 到 maxValue 之间的随机数</returns>
        public Fix64 NextFix64(Fix64 maxValue)
        {
            return Fix64.FromRaw( Next()) * maxValue;
        }



  
        /// <summary>
        /// 从列表中随机选择一个元素
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <param name="list">列表</param>
        /// <returns>随机选择的元素</returns>
        public T Choice<T>(System.Collections.Generic.List<T> list)
        {
            if (list == null || list.Count == 0)
            {
                throw new ArgumentException("列表不能为空");
            }

            int index = Next(list.Count);
            return list[index];
        }



        /// <summary>
        /// 打乱列表（Fisher-Yates洗牌算法）
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <param name="list">要打乱的列表</param>
        public void Shuffle<T>(System.Collections.Generic.List<T> list)
        {
            if (list == null || list.Count <= 1)
            {
                return;
            }

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

   
    }
}

