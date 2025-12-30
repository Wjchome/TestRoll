using System.Collections.Generic;
using Frame.FixMath;

namespace Frame.Physics2D
{
    public static class Utl
    {
        public static FixVector2[] RotateAndTranslate(FixVector2[] position, FixVector2 center, Fix64 rotation)
        {
            Fix64 cos = Fix64.Cos(rotation);
            Fix64 sin = Fix64.Sin(rotation);
            FixVector2[] result = new FixVector2[position.Length];
            for (int i = 0; i < position.Length; i++)
            {
                var localVertex = position[i];
                // 旋转公式：x' = x*cos - y*sin; y' = x*sin + y*cos
                Fix64 rotatedX = localVertex.x * cos - localVertex.y * sin;
                Fix64 rotatedY = localVertex.x * sin + localVertex.y * cos;
                // 平移：加上世界位置
                result[i] = new FixVector2(rotatedX + center.x, rotatedY + center.y);
            }
           
            return result;
        }

        
        public static List<T> UniqueExcept<T>(this List<T> list, List<T> list2)
        {
            HashSet<T> uniqueList = new HashSet<T>(list2);
            List<T> result = new List<T>();
            foreach (var obj in list)
            {
                if (!uniqueList.Contains(obj))
                {
                    result.Add(obj);
                }
            }
            return result;
        }
        public static List<T> UniqueIntersect<T>(this List<T> list, List<T> list2)
        {
            HashSet<T> uniqueList = new HashSet<T>(list2);
            List<T> result = new List<T>();
            foreach (var obj in list)
            {
                if (uniqueList.Contains(obj))
                {
                    result.Add(obj);
                }
            }
            return result;
        }
    }
    
}