using System;
using System.Collections.Generic;
using Frame.FixMath;
using UnityEngine;

namespace Frame.ECS
{
    /// <summary>
    /// 流场寻路算法（Flow Field Pathfinding）
    /// 
    /// 优势：
    /// - 只需要计算一次流场（O(m)，m是地图大小）
    /// - 所有单位只需要查询流场方向（O(1)每个单位）
    /// - 总复杂度：O(m + n)，而不是O(n*m)（n是单位数量）
    /// 
    /// 算法步骤：
    /// 1. 使用Dijkstra算法从目标点计算距离场
    /// 2. 计算梯度场（每个格子指向最近目标的方向）
    /// 3. 单位只需要查询当前位置的梯度方向即可移动
    /// </summary>
    public static class FlowFieldPathfinding
    {
        /// <summary>
        /// 计算流场（从目标点开始）
        /// </summary>
        /// <param name="map">地图组件</param>
        /// <param name="target">目标点（世界坐标）</param>
        /// <returns>流场数据，如果目标不可达则返回null</returns>
        public static Dictionary<GridNode, FixVector2> ComputeFlowField(GridMapComponent map, List<FixVector2> targets)
        {
            List<GridNode> targetNodes = new List<GridNode>(targets.Count);
            foreach (var target in targets)
            {
                var temp = map.WorldToGrid(target);
                // if (!map.IsWalkable(temp))
                // {
                //     continue;
                // }

                targetNodes.Add(temp);
            }

            if (targetNodes.Count == 0)
            {
                Debug.LogError("Flow field could not be calculated.");
                return null;
                
            }


            // 检查目标是否可通行


            var distanceField = new Dictionary<GridNode, Fix64>();
            var gradientField = new Dictionary<GridNode, FixVector2>();

            // 使用Dijkstra算法计算距离场
            var openSet = new SortedSet<(GridNode node, Fix64 distance)>(new DistanceComparer());
            var closedSet = new HashSet<GridNode>();

            foreach (var node in targetNodes)
            {
                // 初始化：目标点距离为0
                distanceField[node] = Fix64.Zero;
                openSet.Add((node, Fix64.Zero));
            }


            // Dijkstra算法：从目标点向外扩展
            while (openSet.Count > 0)
            {
                var current = openSet.Min;
                openSet.Remove(current);

                if (closedSet.Contains(current.node))
                    continue;

                closedSet.Add(current.node);

                // 检查所有邻居
                foreach (var neighbor in GetNeighbors(current.node, map))
                {
                    if (closedSet.Contains(neighbor))
                        continue;

                    // 计算到邻居的距离
                    Fix64 distanceToNeighbor = GetDistance(current.node, neighbor);
                    Fix64 newDistance = distanceField[current.node] + distanceToNeighbor;

                    // 如果找到更短的路径，更新距离
                    if (!distanceField.ContainsKey(neighbor) || newDistance < distanceField[neighbor])
                    {
                        distanceField[neighbor] = newDistance;
                        openSet.Add((neighbor, newDistance));
                    }
                }
            }

            // 计算梯度场：每个格子指向距离最小的邻居方向
            foreach (var kvp in distanceField)
            {
                GridNode node = kvp.Key;
                Fix64 distance = kvp.Value;

                // 如果距离是最大值（不可达），跳过
                if (distance >= Fix64.MaxValue - Fix64.One)
                {
                    continue;
                }

                // 找到距离最小的邻居
                GridNode bestNeighbor = node;
                Fix64 minNeighborDistance = distance;

                foreach (var neighbor in GetNeighbors(node, map))
                {
                    if (distanceField.TryGetValue(neighbor, out var neighborDistance))
                    {
                        if (neighborDistance < minNeighborDistance)
                        {
                            minNeighborDistance = neighborDistance;
                            bestNeighbor = neighbor;
                        }
                    }
                }

                // 计算方向向量（从当前节点指向最佳邻居）
                if (bestNeighbor != node && minNeighborDistance < distance)
                {
                    FixVector2 direction = new FixVector2(
                        (Fix64)(bestNeighbor.x - node.x),
                        (Fix64)(bestNeighbor.y - node.y)
                    );

                    // 归一化方向向量
                    Fix64 magnitude = Fix64.Sqrt(direction.x * direction.x + direction.y * direction.y);
                    if (magnitude > Fix64.Zero)
                    {
                        direction = direction / magnitude;
                    }

                    gradientField[node] = direction;
                }
                else
                {
                    // 如果当前节点就是目标或没有更好的邻居，方向为零
                    gradientField[node] = FixVector2.Zero;
                }
            }


            return gradientField;
        }

        /// <summary>
        /// 获取流场方向（从当前位置）
        /// </summary>
        /// <param name="flowField">流场数据</param>
        /// <param name="map">地图组件</param>
        /// <param name="currentPos">当前位置（世界坐标）</param>
        /// <returns>移动方向（归一化向量），如果不可达则返回零向量</returns>
        public static FixVector2 GetDirection(Dictionary<GridNode, FixVector2> flowField, GridMapComponent map, FixVector2 currentPos)
        {
            GridNode currentNode = map.WorldToGrid(currentPos);

            // 查询梯度场
            if (flowField.TryGetValue(currentNode, out var direction))
            {
                return direction;
            }

            // 如果当前格子不在流场中，尝试查找最近的可用格子
            // 或者返回零向量（表示无法移动）
            return FixVector2.Zero;
        }


        /// <summary>
        /// 获取邻居节点（8方向）
        /// </summary>
        private static List<GridNode> GetNeighbors(GridNode node, GridMapComponent map)
        {
            var neighbors = new List<GridNode>();

            // 8个方向的偏移量
            int[] dx = { -1, 0, 1, 0, 1, 1, -1, -1 };
            int[] dy = { 0, -1, 0, 1, 1, -1, 1, -1 };

            // 斜向方向对应的两个正交方向索引
            var diagonalChecks = new Dictionary<int, (int ortho1, int ortho2)>
            {
                { 4, (2, 3) }, // 右上
                { 5, (2, 1) }, // 右下
                { 6, (0, 3) }, // 左上
                { 7, (0, 1) } // 左下
            };

            for (int i = 0; i < dx.Length; i++)
            {
                int newX = node.x + dx[i];
                int newY = node.y + dy[i];
                GridNode neighbor = new GridNode(newX, newY);

                // 检查基础可走性
                if (!map.IsWalkable(neighbor))
                {
                    continue;
                }

                // 检查斜向方向
                bool isDiagonal = diagonalChecks.ContainsKey(i);
                if (isDiagonal)
                {
                    var (orthoIdx1, orthoIdx2) = diagonalChecks[i];
                    GridNode orthoNode1 = new GridNode(node.x + dx[orthoIdx1], node.y + dy[orthoIdx1]);
                    GridNode orthoNode2 = new GridNode(node.x + dx[orthoIdx2], node.y + dy[orthoIdx2]);

                    if (!map.IsWalkable(orthoNode1) || !map.IsWalkable(orthoNode2))
                    {
                        continue;
                    }
                }

                neighbors.Add(neighbor);
            }

            return neighbors;
        }

        /// <summary>
        /// 计算两个节点之间的距离
        /// </summary>
        private static Fix64 GetDistance(GridNode a, GridNode b)
        {
            int dx = Math.Abs(a.x - b.x);
            int dy = Math.Abs(a.y - b.y);
            Fix64 dxFix = (Fix64)dx;
            Fix64 dyFix = (Fix64)dy;
            return Fix64.Sqrt(dxFix * dxFix + dyFix * dyFix);
        }

        /// <summary>
        /// 距离比较器（用于SortedSet）
        /// </summary>
        private class DistanceComparer : IComparer<(GridNode node, Fix64 distance)>
        {
            public int Compare((GridNode node, Fix64 distance) x, (GridNode node, Fix64 distance) y)
            {
                int distanceCompare = x.distance.CompareTo(y.distance);
                if (distanceCompare != 0)
                    return distanceCompare;

                // 距离相同时，使用位置作为tie-breaker（确保确定性）
                int xCompare = x.node.x.CompareTo(y.node.x);
                if (xCompare != 0)
                    return xCompare;

                return x.node.y.CompareTo(y.node.y);
            }
        }
    }
}