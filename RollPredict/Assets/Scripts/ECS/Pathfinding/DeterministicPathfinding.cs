using System;
using System.Collections.Generic;
using System.Linq;
using Frame.FixMath;

namespace Frame.ECS
{
    /// <summary>
    /// 确定性A*寻路算法
    /// 使用固定点数学，确保所有客户端产生相同的路径
    /// </summary>
    public static class DeterministicPathfinding
    {
        /// <summary>
        /// A*节点（包含路径信息）
        /// </summary>
        private struct AStarNode : IComparable<AStarNode>
        {
            public GridNode position;
            public Fix64 g;  // 从起点到当前节点的实际代价
            public Fix64 h;  // 从当前节点到终点的启发式代价
            public Fix64 f=>g+h;  // f = g + h
            
            // 确定性比较：当f相同时，使用位置作为tie-breaker
            public int CompareTo(AStarNode other)
            {
                // 首先比较f值
                int fCompare = f.CompareTo(other.f);
                if (fCompare != 0)
                    return fCompare;
                
                // f值相同时，使用位置作为tie-breaker（确保确定性）
                int xCompare = position.x.CompareTo(other.position.x);
                if (xCompare != 0)
                    return xCompare;
                
                return position.y.CompareTo(other.position.y);
            }
        }
        
        /// <summary>
        /// 使用A*算法查找路径
        /// </summary>
        /// <param name="map">地图组件</param>
        /// <param name="start">起点（世界坐标）</param>
        /// <param name="end">终点（世界坐标）</param>
        /// <returns>路径（世界坐标列表），如果找不到路径返回null</returns>
        public static List<FixVector2> FindPath(GridMapComponent map, FixVector2 start, FixVector2 end)
        {
            GridNode startNode = map.WorldToGrid(start);
            GridNode endNode = map.WorldToGrid(end);
            
            // 检查起点和终点是否可通行
            if (!map.IsWalkable(startNode) || !map.IsWalkable(endNode))
            {
                return null;
            }
            
            // 如果起点和终点相同，直接返回
            if (startNode.Equals(endNode))
            {
                return new List<FixVector2> { start, end };
            }
            
            // A*算法
            // 使用Dictionary存储openSet中的节点（O(1)查找）
            var openSetDict = new Dictionary<GridNode, AStarNode>();
            // 使用SortedSet获取最小f值的节点（O(log n)插入/删除）
            var openSet = new SortedSet<AStarNode>();
            var closedSet = new HashSet<GridNode>();
            // child -> parent
            var cameFrom = new Dictionary<GridNode, GridNode>();
            var gScore = new Dictionary<GridNode, Fix64>();
            var fScore = new Dictionary<GridNode, Fix64>();
            
            // 初始化起点
            gScore[startNode] = Fix64.Zero;
            fScore[startNode] = Heuristic(startNode, endNode);
            var startAStarNode = new AStarNode
            {
                position = startNode,
                g = Fix64.Zero,
                h = fScore[startNode],
            };
            openSetDict[startNode] = startAStarNode;
            openSet.Add(startAStarNode);
            
            while (openSet.Count > 0)
            {
                // 获取f值最小的节点（SortedSet自动排序）
                var current = openSet.Min;
                openSet.Remove(current);
                openSetDict.Remove(current.position);
                
                GridNode currentPos = current.position;
                
                // 如果到达终点，重建路径
                if (currentPos.Equals(endNode))
                {
                    return ReconstructPath(cameFrom, currentPos, map);
                }
                
                closedSet.Add(currentPos);
                
                // 检查所有邻居
                foreach (var neighbor in GetNeighbors(currentPos, map))
                {
                    if (closedSet.Contains(neighbor))
                        continue;
                    
                    // 计算从起点到邻居的代价
                    Fix64 tentativeG = gScore[currentPos] + GetDistance(currentPos, neighbor);
                    
                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = currentPos;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + Heuristic(neighbor, endNode);
                        
                        // 移除旧的节点（如果存在）- O(1)查找 + O(log n)删除
                        if (openSetDict.TryGetValue(neighbor, out var oldNode))
                        {
                            openSet.Remove(oldNode);
                            openSetDict.Remove(neighbor);
                        }
                        
                        // 添加到开放集 - O(1)插入Dictionary + O(log n)插入SortedSet
                        var newNode = new AStarNode
                        {
                            position = neighbor,
                            g = tentativeG,
                            h = Heuristic(neighbor, endNode),
                        };
                        openSetDict[neighbor] = newNode;
                        openSet.Add(newNode);
                    }
                }
            }
            
            // 找不到路径
            return null;
        }
        
        /// <summary>
        /// 获取节点的邻居 平滑
        /// </summary>
        private static List<GridNode> GetNeighbors(GridNode node, GridMapComponent map)
        {
            var neighbors = new List<GridNode>();
            
            int[] dx = { -1, 0, 1, 0,1,1,-1,-1 };
            int[] dy = { 0, -1, 0, 1,1,-1,1,-1};
            
            for (int i = 0; i < dx.Length; i++)
            {
                GridNode neighbor = new GridNode(node.x + dx[i], node.y + dy[i]);
                if (map.IsWalkable(neighbor))
                {
                    neighbors.Add(neighbor);
                }
            }
            
            return neighbors;
        }
        
        /// <summary>
        /// 启发式函数：欧几里得距离
        /// </summary>
        private static Fix64 Heuristic(GridNode a, GridNode b)
        {
            int dx = Math.Abs(a.x - b.x);
            int dy = Math.Abs(a.y - b.y);
            Fix64 dxFix = (Fix64)dx;
            Fix64 dyFix = (Fix64)dy;
            return Fix64.Sqrt(dxFix * dxFix + dyFix * dyFix);
        }
        
        /// <summary>
        /// 计算两个节点之间的距离（确定性）
        /// 平滑移动（8方向，对角线距离为√2）
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
        /// 重建路径（从终点回溯到起点）
        /// </summary>
        private static List<FixVector2> ReconstructPath(
            Dictionary<GridNode, GridNode> cameFrom, 
            GridNode current, 
            GridMapComponent map)
        {
            var path = new List<FixVector2>();
            path.Add(map.GridToWorld(current));
            
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(map.GridToWorld(current));
            }
            
            // 反转路径（从起点到终点）
            path.Reverse();
            return path;
        }
    }
}

