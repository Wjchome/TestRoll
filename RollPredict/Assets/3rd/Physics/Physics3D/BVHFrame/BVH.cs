using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Frame.FixMath;
using PhysicsLayer = Frame.Physics2D.PhysicsLayer;

namespace Frame.Physics3D
{
    /// <summary>
    /// BVH（Bounding Volume Hierarchy）空间分区系统（用于帧同步碰撞检测）
    /// 标准BVH实现：使用最长轴+中位数划分（median split）
    /// 每个物体只存储在一个节点中，不会重复存储
    /// </summary>
    public class BVH
    {
        [Tooltip("每个节点最大存储物体数（超过则分裂）")] 
        public int MaxObjectsPerNode = 2;

        [Tooltip("最大递归深度（防止无限分裂）")] 
        public int MaxDepth = 10;


        private BVHNode _rootNode; // BVH根节点

        /// <summary>
        /// 是否已初始化
        /// </summary>
        private bool _isInitialized = false;


        private Dictionary<RigidBody3D, BVHNode> bodyToBVHNode;

        public void Init()
        {
            bodyToBVHNode = new Dictionary<RigidBody3D, BVHNode>();
            // 初始化根节点
            _rootNode = new BVHNode(0, MaxObjectsPerNode, MaxDepth);
            _isInitialized = true;
        }



        #region 添加物体

        public void AddObject(RigidBody3D target)
        {
            if (!_isInitialized)
            {
                Init();
            }
            _rootNode.Add(target, bodyToBVHNode);
        }

        #endregion

        #region 移除物体

        /// <summary>
        /// 从BVH中移除物体
        /// 使用索引直接定位到叶子节点（O(1)查找）
        /// </summary>
        public void RemoveObject(RigidBody3D target)
        {
            if (!_isInitialized) return;
            
            // 使用索引快速找到叶子节点（索引中存储的一定是叶子节点）
            if (bodyToBVHNode.TryGetValue(target, out BVHNode leafNode))
            {
                // 直接从叶子节点移除（不需要递归查找）
                leafNode.RemoveFromLeaf(target);
                bodyToBVHNode.Remove(target);
                
                
            }
        }

        #endregion

        #region 增量更新

        /// <summary>
        /// 更新物体在BVH中的位置（增量更新，使用索引优化）
        /// 索引指向的一定是叶子节点，所以可以直接使用
        /// </summary>
        public void UpdateObject(RigidBody3D body)
        {
            if (!_isInitialized)
            {
                Init();
            }

            // 使用索引快速找到当前存储的叶子节点（索引中存储的一定是叶子节点）
            if (!bodyToBVHNode.TryGetValue(body, out BVHNode currentLeafNode))
            {
                // 如果找不到，说明物体不在BVH中，直接添加
                AddObject(body);
                return;
            }

            FixBounds newBounds = body.Shape.GetBounds(body.Position);

            // 直接判断：如果新包围盒仍在当前叶子节点的包围盒内，只需要更新包围盒
            if (currentLeafNode.Bounds.Contains(newBounds.Min) && 
                currentLeafNode.Bounds.Contains(newBounds.Max))
            {
                currentLeafNode.RecalculateBounds();
                return;
            }

            // 物体已经移出当前叶子节点，需要重新插入
            // 先从当前叶子节点移除
            RemoveObject(body);
            // 重新添加到BVH（会找到新的合适位置）
            AddObject(body);
        }



        #endregion



        #region 查询物体

        /// <summary>
        /// 查询指定区域内的所有物体
        /// </summary>
        /// <param name="area">查询区域</param>
        /// <param name="layerMask">层掩码（只返回匹配的层，默认返回所有层）</param>
        /// <returns>匹配的物体列表</returns>
        public List<RigidBody3D> Query(FixBounds area, PhysicsLayer layerMask = default)
        {
            var result = new List<RigidBody3D>();
            _rootNode.GetObjectsInArea(area, result, layerMask);
            return result;
        }

        #endregion

        #region 全量清除

        /// <summary>
        /// 全量删除所有物体
        /// </summary>
        public void Clear()
        {
            _rootNode = new BVHNode(0, MaxObjectsPerNode, MaxDepth);
            if (bodyToBVHNode != null)
            {
                bodyToBVHNode.Clear();
            }
        }

        #endregion

        /// <summary>
        /// 【调试用】在Scene视图绘制BVH区域
        /// </summary>
        public void DrawGizmos()
        {
            if (_rootNode == null || !_isInitialized) return;
            DrawNodeGizmos(_rootNode, 0);
        }

        /// <summary>
        /// 递归绘制BVH节点
        /// </summary>
        private void DrawNodeGizmos(BVHNode node, int depth)
        {
            if (node == null) return;

            // 根据深度设置颜色
            Color[] colors = { Color.red, Color.blue, Color.green, Color.yellow, Color.cyan, Color.magenta };
            Gizmos.color = colors[depth % colors.Length];

            // 绘制当前节点的边界框
            Vector3 min = new Vector3((float)node.Bounds.Min.x, (float)node.Bounds.Min.y, (float)node.Bounds.Min.z);
            Vector3 max = new Vector3((float)node.Bounds.Max.x, (float)node.Bounds.Max.y, (float)node.Bounds.Max.z);

            // 绘制AABB的12条边
            Vector3[] corners = new Vector3[]
            {
                new Vector3(min.x, min.y, min.z), // 左下后
                new Vector3(max.x, min.y, min.z), // 右下后
                new Vector3(max.x, max.y, min.z), // 右上后
                new Vector3(min.x, max.y, min.z), // 左上后
                new Vector3(min.x, min.y, max.z), // 左下前
                new Vector3(max.x, min.y, max.z), // 右下前
                new Vector3(max.x, max.y, max.z), // 右上前
                new Vector3(min.x, max.y, max.z)  // 左上前
            };

            // 后面四条边
            Gizmos.DrawLine(corners[0], corners[1]);
            Gizmos.DrawLine(corners[1], corners[2]);
            Gizmos.DrawLine(corners[2], corners[3]);
            Gizmos.DrawLine(corners[3], corners[0]);

            // 前面四条边
            Gizmos.DrawLine(corners[4], corners[5]);
            Gizmos.DrawLine(corners[5], corners[6]);
            Gizmos.DrawLine(corners[6], corners[7]);
            Gizmos.DrawLine(corners[7], corners[4]);

            // 连接前后面的四条边
            Gizmos.DrawLine(corners[0], corners[4]);
            Gizmos.DrawLine(corners[1], corners[5]);
            Gizmos.DrawLine(corners[2], corners[6]);
            Gizmos.DrawLine(corners[3], corners[7]);

            // 递归绘制子节点
            if (node.Left != null) DrawNodeGizmos(node.Left, depth + 1);
            if (node.Right != null) DrawNodeGizmos(node.Right, depth + 1);
        }
    }

    /// <summary>
    /// BVH节点类（标准BVH实现：中位数划分）
    /// </summary>
    public class BVHNode
    {
        public FixBounds Bounds; // 当前节点的边界框（包含所有子物体的包围盒）
        private int _currentDepth; // 当前节点深度
        private int _maxObjects; // 节点最大存储物体数
        private int _maxDepth; // 最大递归深度

        private List<RigidBody3D> _objects; // 当前节点存储的物体（叶子节点）

        public BVHNode Left, Right;
        public bool IsSplit => Left != null; // 是否已分裂为子节点

        // 分裂信息（用于判断新物体应该放入哪个子节点）
        internal int _splitAxis = -1; // 划分轴：0=X, 1=Y, 2=Z, -1=未分裂
        internal Fix64 _splitValue; // 划分值（中位数位置）

        public BVHNode(int currentDepth, int maxObjects, int maxDepth)
        {
            Bounds = default;
            _currentDepth = currentDepth;
            _maxObjects = maxObjects;
            _maxDepth = maxDepth;
            _objects = new List<RigidBody3D>();
        }

        // 添加物体到节点
        public void Add(RigidBody3D obj, Dictionary<RigidBody3D, BVHNode> bodyToBVHNode)
        {
            FixBounds objBounds = obj.Shape.GetBounds(obj.Position);

            // 1. 如果节点已分裂，添加到合适的子节点
            if (IsSplit)
            {
                Bounds.Encapsulate(objBounds);
                
                // 根据物体中心点在划分轴上的位置选择子节点
                FixVector3 objCenter = objBounds.Center;
                Fix64 centerValue = _splitAxis == 0 ? objCenter.x : (_splitAxis == 1 ? objCenter.y : objCenter.z);

                // 根据划分值决定放入哪个子节点
                if (centerValue < _splitValue)
                {
                    Left.Add(obj, bodyToBVHNode);
                }
                else
                {
                    Right.Add(obj, bodyToBVHNode);
                }
                return;
            }

            // 2. 未分裂则添加到当前节点（叶子节点）
            _objects.Add(obj);
            if (Bounds.Min == default && Bounds.Max == default)
            {
                Bounds = objBounds;
            }
            else
            {
                Bounds.Encapsulate(objBounds);
            }

            // 更新索引：记录物体到节点的映射
            bodyToBVHNode[obj] = this;

            // 3. 检查是否需要分裂（物体数超上限 + 未到最大深度）
            if (_objects.Count > _maxObjects && _currentDepth < _maxDepth)
            {
                Split(bodyToBVHNode); // 使用中位数划分分裂为2个子节点
            }
        }

        // 使用中位数划分（median split）分裂为2个子节点
        private void Split(Dictionary<RigidBody3D, BVHNode> bodyToBVHNode)
        {
            if (_objects.Count <= 1) return; // 无法分裂

            // 1. 计算所有物体的包围盒，找到最长轴
            FixBounds allBounds = _objects[0].Shape.GetBounds(_objects[0].Position);
            for (int i = 1; i < _objects.Count; i++)
            {
                FixBounds bounds = _objects[i].Shape.GetBounds(_objects[i].Position);
                allBounds.Encapsulate(bounds);
            }

            FixVector3 size = allBounds.Size;
            int longestAxis = 0; // 0=X, 1=Y, 2=Z
            Fix64 maxSize = size.x;
            if (size.y > maxSize)
            {
                maxSize = size.y;
                longestAxis = 1;
            }
            if (size.z > maxSize)
            {
                longestAxis = 2;
            }

            // 2. 按照物体中心点在最长轴上的位置排序
            List<RigidBody3D> sortedObjects = new List<RigidBody3D>(_objects);
            if (longestAxis == 0) // X轴
            {
                sortedObjects.Sort((a, b) =>
                {
                    FixVector3 centerA = a.Shape.GetBounds(a.Position).Center;
                    FixVector3 centerB = b.Shape.GetBounds(b.Position).Center;
                    return centerA.x.CompareTo(centerB.x);
                });
            }
            else if (longestAxis == 1) // Y轴
            {
                sortedObjects.Sort((a, b) =>
                {
                    FixVector3 centerA = a.Shape.GetBounds(a.Position).Center;
                    FixVector3 centerB = b.Shape.GetBounds(b.Position).Center;
                    return centerA.y.CompareTo(centerB.y);
                });
            }
            else // Z轴
            {
                sortedObjects.Sort((a, b) =>
                {
                    FixVector3 centerA = a.Shape.GetBounds(a.Position).Center;
                    FixVector3 centerB = b.Shape.GetBounds(b.Position).Center;
                    return centerA.z.CompareTo(centerB.z);
                });
            }

            // 3. 使用中位数划分
            int median = sortedObjects.Count / 2;

            // 4. 计算划分值（中位数位置）
            FixVector3 medianCenter = sortedObjects[median].Shape.GetBounds(sortedObjects[median].Position).Center;
            _splitValue = longestAxis == 0 ? medianCenter.x : (longestAxis == 1 ? medianCenter.y : medianCenter.z);
            _splitAxis = longestAxis;

            // 5. 创建左右子节点
            Left = new BVHNode(_currentDepth + 1, _maxObjects, _maxDepth);
            Right = new BVHNode(_currentDepth + 1, _maxObjects, _maxDepth);

            // 6. 将物体分配到子节点（每个物体只属于一个节点）
            // 注意：Add方法会自动更新bodyToBVHNode索引
            for (int i = 0; i < median; i++)
            {
                Left.Add(sortedObjects[i], bodyToBVHNode);
            }
            for (int i = median; i < sortedObjects.Count; i++)
            {
                Right.Add(sortedObjects[i], bodyToBVHNode);
            }

            // 7. 清空当前节点的物体列表（不再是叶子节点）
            _objects.Clear();
        }

        /// <summary>
        /// 查询指定区域内的所有物体（支持层过滤）
        /// </summary>
        public void GetObjectsInArea(FixBounds area, List<RigidBody3D> result, PhysicsLayer layerMask)
        {
            // 1. 如果当前节点与查询区域无重叠，直接返回
            if (!Bounds.Intersects(area)) return;

            // 2. 未分裂（叶子节点）则检查当前节点的物体
            if (!IsSplit)
            {
                foreach (var obj in _objects)
                {
                    // 层过滤
                    if (layerMask.value != 0 && !layerMask.Intersects(obj.Layer))
                        continue;

                    if (obj.Shape.GetBounds(obj.Position).Intersects(area))
                    {
                        result.Add(obj);
                    }
                }
                return;
            }

            // 3. 已分裂则递归查询子节点
            Left.GetObjectsInArea(area, result, layerMask);
            Right.GetObjectsInArea(area, result, layerMask);
        }

        /// <summary>
        /// 从叶子节点移除物体（索引指向的一定是叶子节点，所以直接移除）
        /// </summary>
        public void RemoveFromLeaf(RigidBody3D target)
        {
            // 索引指向的一定是叶子节点，所以IsSplit一定是false
            bool removed = _objects.Remove(target);
            if (removed)
            {
                // 重新计算包围盒
                RecalculateBounds();
            }
        }

        
        /// <summary>
        /// 检查节点是否为空
        /// </summary>
        public bool IsEmpty()
        {
            if (IsSplit)
            {
                return Left.IsEmpty() && Right.IsEmpty();
            }
            return _objects.Count == 0;
        }

        /// <summary>
        /// 重新计算节点的包围盒
        /// </summary>
        internal void RecalculateBounds()
        {
            if (IsSplit)
            {
                // 内部节点：合并左右子节点的包围盒
                if (!Left.IsEmpty() && !Right.IsEmpty())
                {
                    Bounds = Left.Bounds;
                    Bounds.Encapsulate(Right.Bounds);
                }
                else if (!Left.IsEmpty())
                {
                    Bounds = Left.Bounds;
                }
                else if (!Right.IsEmpty())
                {
                    Bounds = Right.Bounds;
                }
            }
            else
            {
                // 叶子节点：合并所有物体的包围盒
                if (_objects.Count == 0)
                {
                    Bounds = default;
                    return;
                }

                FixBounds? bounds = null;
                foreach (var obj in _objects)
                {
                    FixBounds objBounds = obj.Shape.GetBounds(obj.Position);
                    if (bounds == null)
                    {
                        bounds = objBounds;
                    }
                    else
                    {
                        FixBounds newBounds = bounds.Value;
                        newBounds.Encapsulate(objBounds);
                        bounds = newBounds;
                    }
                }
                if (bounds.HasValue)
                {
                    Bounds = bounds.Value;
                }
            }
        }

        /// <summary>
        /// 收集所有物体（用于重建）
        /// </summary>
        public void CollectAllBodies(List<RigidBody3D> result)
        {
            if (IsSplit)
            {
                Left.CollectAllBodies(result);
                Right.CollectAllBodies(result);
            }
            else
            {
                result.AddRange(_objects);
            }
        }
    }
}
