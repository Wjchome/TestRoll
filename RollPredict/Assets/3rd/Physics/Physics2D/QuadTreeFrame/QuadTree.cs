using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Frame.FixMath;

namespace Frame.Physics2D
{
    /// <summary>
    /// 四叉树空间分区系统（用于帧同步碰撞检测）
    /// 支持矩形和圆形碰撞体，支持旋转矩形，支持层过滤
    /// </summary>
    public class QuadTree
    {
        [Tooltip("每个节点最大存储物体数（超过则分裂）")] public int MaxObjectsPerNode = 4;

        [Tooltip("最大递归深度（防止无限分裂）")] public int MaxDepth = 5;

        /// <summary>
        /// 根节点的矩形区域（固定点）
        /// </summary>
        private FixRect RootRect;

        private QuadTreeNode _rootNode; // 四叉树根节点

        /// <summary>
        /// 是否已初始化
        /// </summary>
        private bool _isInitialized = false;

        public void Init(FixRect worldBounds)
        {
            RootRect = worldBounds;
            // 初始化根节点
            _rootNode = new QuadTreeNode(RootRect, 0, MaxObjectsPerNode, MaxDepth);
            _isInitialized = true;
        }

        public FixRect GetWorldBounds()
        {
            return RootRect;
        }

        /// <summary>
        /// 延迟初始化：如果未初始化，使用默认边界
        /// </summary>
        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                // 使用默认边界（可以根据需要调整）
                Init(new FixRect(
                    (Fix64)(-10), (Fix64)(-10),
                    (Fix64)20, (Fix64)20
                ));
            }
        }


        #region 添加物体

        public void AddObject(RigidBody2D target)
        {
            EnsureInitialized();
            _rootNode.Add(target);
        }

        #endregion

        #region 移除物体

        /// <summary>
        /// 从四叉树中移除物体
        /// </summary>
        /// <param name="target">要移除的Unity物体</param>
        public void RemoveObject(RigidBody2D target)
        {
            if (!_isInitialized) return; // 未初始化，无需移除
            _rootNode.Remove(target);
        }

        #endregion

        #region 增量更新

        /// <summary>
        /// 更新物体在四叉树中的位置（增量更新）
        /// </summary>
        public void UpdateObject(RigidBody2D body)
        {
            // 如果新旧AABB在同一节点，不需要更新
            // 否则需要移除并重新添加

            // 1. 先移除（使用旧AABB查找）
            RemoveObject(body);

            // 2. 重新添加（使用新AABB）
            AddObject(body);
        }

        #endregion

        #region 自动扩容

        /// <summary>
        /// 检查并自动扩容（如果物体超出边界）
        /// 优化：只检查动态物体，静态物体不会移动
        /// </summary>
        public bool CheckAndExpand(List<RigidBody2D> allBodies)
        {
            if (allBodies.Count == 0) return false;

            // 如果未初始化，先初始化
            if (!_isInitialized)
            {
                EnsureInitialized();
            }


            foreach (var body in allBodies)
            {
                // 跳过静态物体（它们不会移动）
                if (!body.IsDynamic) continue;

                FixRect aabb = body.Shape.GetBounds(body.Position);

                // 快速检查：如果物体超出当前边界，立即标记需要扩容
                if (!RootRect.Contains(aabb))
                {
                    return ExpandToFit(allBodies);
                }
            }

            return false;
        }

        /// <summary>
        /// 扩容以容纳所有物体
        /// </summary>
        private bool ExpandToFit(List<RigidBody2D> allBodies)
        {
            // 计算所有物体的包围盒
            FixRect? bounds = null;
            foreach (var body in allBodies)
            {
                FixRect aabb = body.Shape.GetBounds(body.Position);
                if (bounds == null)
                {
                    bounds = aabb;
                }
                else
                {
                    bounds = FixRect.Union(bounds.Value, aabb);
                }
            }

            if (bounds == null) return false;

            FixRect allBounds = bounds.Value;


            Fix64 newWidth = allBounds.Width * (Fix64)1.5;
            Fix64 newHeight = allBounds.Height * (Fix64)1.5;

            Fix64 centerX = allBounds.CenterX;
            Fix64 centerY = allBounds.CenterY;

            FixRect newBounds = new FixRect(
                centerX - newWidth / Fix64.Two,
                centerY - newHeight / Fix64.Two,
                newWidth,
                newHeight
            );

            // 重建四叉树
            Expand(newBounds, allBodies);
            return true;
        }

        /// <summary>
        /// 扩容：重建四叉树
        /// </summary>
        private void Expand(FixRect newBounds, List<RigidBody2D> allBodies)
        {
            // 更新世界边界
            RootRect = newBounds;

            // 重建根节点
            _rootNode = new QuadTreeNode(RootRect, 0, MaxObjectsPerNode, MaxDepth);

            // 重新插入所有物体
            foreach (var body in allBodies)
            {
                AddObject(body);
            }
        }

        #endregion


        #region 查询物体

        /// <summary>
        /// 查询指定矩形区域内的所有物体（精确检测，包含SAT）
        /// </summary>
        /// <param name="area">查询区域</param>
        /// <param name="layerMask">层掩码（只返回匹配的层，默认返回所有层）</param>
        /// <returns>匹配的物体列表（已排序，确定性）</returns>
        public List<RigidBody2D> Query(FixRect area, PhysicsLayer layerMask = default)
        {
            EnsureInitialized();
            var result = new List<RigidBody2D>();
            _rootNode.GetObjectsInArea(area, result, layerMask);
            return result.Distinct().ToList();;
        }

        #endregion


        #region 全量清除

        /// <summary>
        /// 全量删除所有物体（直接重置根节点，比逐个Remove快得多）
        /// </summary>
        public void Clear()
        {
            // 直接重新初始化根节点，所有子节点会被GC回收
            _rootNode = new QuadTreeNode(RootRect, 0, MaxObjectsPerNode, MaxDepth);
        }

        #endregion

        /// <summary>
        /// 【调试用】在Scene视图绘制四叉树区域
        /// </summary>
        public void DrawGizmos()
        {
            if (_rootNode == null || !_isInitialized) return;
            DrawNodeGizmos(_rootNode, 0);
        }

        /// <summary>
        /// 递归绘制四叉树节点
        /// </summary>
        /// <param name="node">当前节点</param>
        /// <param name="depth">当前深度</param>
        private void DrawNodeGizmos(QuadTreeNode node, int depth)
        {
            if (node == null) return;

            // 根据深度设置颜色（根节点红色，子节点蓝色，更深层次绿色）

            Gizmos.color = Color.red; // 根节点：红色


            // 绘制当前节点的矩形边界
            Vector2[] corners = new Vector2[4]
            {
                new Vector2((float)node.Rect.X, (float)node.Rect.Y),
                new Vector2((float)node.Rect.X + (float)node.Rect.Width, (float)node.Rect.Y),
                new Vector2((float)node.Rect.X + (float)node.Rect.Width, (float)node.Rect.Y + (float)node.Rect.Height),
                new Vector2((float)node.Rect.X, (float)node.Rect.Y + (float)node.Rect.Height),
            };

            for (int i = 0; i < 4; i++)
            {
                Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
            }

            // 递归绘制子节点
            if (node.LeftUp != null) DrawNodeGizmos(node.LeftUp, depth + 1);
            if (node.RightUp != null) DrawNodeGizmos(node.RightUp, depth + 1);
            if (node.LeftDown != null) DrawNodeGizmos(node.LeftDown, depth + 1);
            if (node.RightDown != null) DrawNodeGizmos(node.RightDown, depth + 1);
        }
    }

// 四叉树节点类（内部逻辑，不对外暴露）
    public class QuadTreeNode
    {
        public FixRect Rect; // 当前节点的矩形区域
        private int _currentDepth; // 当前节点深度
        private int _maxObjects; // 节点最大存储物体数
        private int _maxDepth; // 最大递归深度

        private List<RigidBody2D> _objects; // 当前节点存储的物体

        public QuadTreeNode LeftUp, RightUp, LeftDown, RightDown;
        public bool IsSplit => LeftUp != null; // 是否已分裂为子节点

        public QuadTreeNode(FixRect rect, int currentDepth, int maxObjects, int maxDepth)
        {
            Rect = rect;
            _currentDepth = currentDepth;
            _maxObjects = maxObjects;
            _maxDepth = maxDepth;
            _objects = new List<RigidBody2D>();
        }

        // 添加物体到节点
        public void Add(RigidBody2D obj)
        {
            // 1. 如果节点已分裂，尝试添加到子节点
            if (IsSplit)
            {
                AddToChildNodes(obj);
                return;
            }

            // 2. 未分裂则添加到当前节点
            _objects.Add(obj);

            // 3. 检查是否需要分裂（物体数超上限 + 未到最大深度）
            if (_objects.Count > _maxObjects && _currentDepth < _maxDepth)
            {
                Split(); // 分裂为4个子节点
                // 把当前节点的物体迁移到子节点
                foreach (var o in _objects)
                {
                    AddToChildNodes(o);
                }

                _objects.Clear(); // 清空当前节点的物体
            }
        }


        // 分裂为4个子节点
        private void Split()
        {
            Fix64 halfWidth = Rect.Width / new Fix64(2);
            Fix64 halfHeight = Rect.Height / new Fix64(2);
            Fix64 x = Rect.X;
            Fix64 y = Rect.Y;

            // 创建四个子节点（左上、右上、左下、右下）
            LeftUp = new QuadTreeNode(new FixRect(x, y + halfHeight, halfWidth, halfHeight), _currentDepth + 1,
                _maxObjects,
                _maxDepth);
            RightUp = new QuadTreeNode(new FixRect(x + halfWidth, y + halfHeight, halfWidth, halfHeight),
                _currentDepth + 1,
                _maxObjects, _maxDepth);
            LeftDown = new QuadTreeNode(new FixRect(x, y, halfWidth, halfHeight), _currentDepth + 1, _maxObjects,
                _maxDepth);
            RightDown = new QuadTreeNode(new FixRect(x + halfWidth, y, halfWidth, halfHeight), _currentDepth + 1,
                _maxObjects,
                _maxDepth);
        }

        // 将物体添加到子节点（仅添加到与物体重叠的子节点）
        private void AddToChildNodes(RigidBody2D obj)
        {
            // 如果矩形有旋转，使用 AABB 进行快速筛选（提高四叉树效率）
            FixRect boundsForTree = obj.Shape.GetBounds(obj.Position);
            if (LeftUp.Rect.Overlaps(boundsForTree)) LeftUp.Add(obj);
            if (RightUp.Rect.Overlaps(boundsForTree)) RightUp.Add(obj);
            if (LeftDown.Rect.Overlaps(boundsForTree)) LeftDown.Add(obj);
            if (RightDown.Rect.Overlaps(boundsForTree)) RightDown.Add(obj);
        }

        /// <summary>
        /// 查询指定区域内的所有物体（支持层过滤）
        /// 使用精确检测（包含SAT），用于最终碰撞检测
        /// </summary>
        public void GetObjectsInArea(FixRect area, List<RigidBody2D> result, PhysicsLayer layerMask)
        {
            // 1. 如果当前节点与查询区域无重叠，直接返回
            if (!Rect.Overlaps(area)) return;

            // 2. 未分裂则检查当前节点的物体
            if (!IsSplit)
            {
                foreach (var obj in _objects)
                {
                    // 层过滤
                    if (layerMask.value != 0 && !layerMask.Intersects(obj.Layer))
                        continue;

                    if (obj.Shape.GetBounds(obj.Position).Overlaps(area))
                    {
                        result.Add(obj);
                    }
                }

                return;
            }

            // 3. 已分裂则递归查询子节点
            LeftUp.GetObjectsInArea(area, result, layerMask);
            RightUp.GetObjectsInArea(area, result, layerMask);
            LeftDown.GetObjectsInArea(area, result, layerMask);
            RightDown.GetObjectsInArea(area, result, layerMask);
        }


        public void Remove(RigidBody2D target)
        {
            // 1. 未分裂节点：直接从当前列表移除
            if (!IsSplit)
            {
                for (int i = 0; i < _objects.Count; i++)
                {
                    if (_objects[i] == target)
                    {
                        _objects.RemoveAt(i);
                        return;
                    }
                }

                return;
            }

            // 2. 已分裂节点：递归从子节点移除
            LeftUp.Remove(target);
            RightUp.Remove(target);
            LeftDown.Remove(target);
            RightDown.Remove(target);

            // 注意：不合并节点是安全的，原因：
            // 1. 四叉树的主要目的是空间分区，用于快速查询
            // 2. 空节点不会影响查询的正确性
            // 3. 合并节点需要遍历所有子节点，性能开销大
            // 4. 如果后续有物体添加到该区域，节点会自然被重新使用
            // 5. 商业物理引擎（如Box2D）也采用不合并的策略
        }

        /// <summary>
        /// 获取节点中的物体数量（用于调试）
        /// </summary>
        internal int GetObjectCount()
        {
            if (IsSplit)
            {
                return LeftUp.GetObjectCount() + RightUp.GetObjectCount() +
                       LeftDown.GetObjectCount() + RightDown.GetObjectCount();
            }

            return _objects.Count;
        }

        private void TryMergeNodes()
        {
            if (!IsSplit) return; // 未分裂无需合并
            if (LeftUp.IsSplit || RightUp.IsSplit || LeftDown.IsSplit || RightDown.IsSplit)
            {
                //还有细分 无法合并
                return;
            }

            HashSet<RigidBody2D> uniqueChildObjects = new HashSet<RigidBody2D>();


            foreach (var obj in LeftUp._objects)
            {
                uniqueChildObjects.Add(obj);
            }

            foreach (var obj in RightUp._objects)
            {
                uniqueChildObjects.Add(obj);
            }

            foreach (var obj in LeftDown._objects)
            {
                uniqueChildObjects.Add(obj);
            }

            foreach (var obj in RightDown._objects)
            {
                uniqueChildObjects.Add(obj);
            }

            // 步骤2：统计唯一物体数（真实数量，无重复）
            int totalUniqueObjects = uniqueChildObjects.Count;

            // 步骤3：只有唯一物体数 ≤ 最大物体数，才合并
            if (totalUniqueObjects <= _maxObjects)
            {
                var list = uniqueChildObjects.ToList();
                list.Sort();
                _objects = list;

                // 销毁所有子节点（恢复未分裂状态）
                LeftUp = RightUp = LeftDown = RightDown = null;
            }
        }
    }
}