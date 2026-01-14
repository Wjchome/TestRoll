using System;
using Frame.FixMath;

namespace Frame.ECS
{
    /// <summary>
    /// 网格地图组件：存储网格地图信息
    /// </summary>
    [Serializable]
    public struct GridMapComponent : IComponent
    {
        // 地图宽度和高度（网格数）
        public int width;
        public int height;
        
        // 网格大小（世界单位）
        public Fix64 cellSize;
        
        // 障碍物集合（使用OrderedHashSet确保确定性）
        public OrderedHashSet<GridNode> obstacles;
        
        public GridMapComponent(int width, int height, Fix64 cellSize)
        {
            this.width = width;
            this.height = height;
            this.cellSize = cellSize;
            this.obstacles = new OrderedHashSet<GridNode>();
        }
        
        /// <summary>
        /// 检查节点是否可通行
        /// 
        /// 地图范围：(-width, -height) 到 (width, height)
        /// 网格坐标范围：-width <= x < width, -height <= y < height
        /// </summary>
        public bool IsWalkable(GridNode node)
        {
            // 检查边界：支持负数坐标，范围从 -width 到 width-1
            if (node.x < -width || node.x >= width || node.y < -height || node.y >= height)
                return false;
            
            // 检查障碍物
            return !obstacles.Contains(node);
        }
        
        /// <summary>
        /// 世界坐标转网格坐标
        /// 
        /// 使用向下取整（Floor），确保负数坐标正确处理
        /// 例如：worldPos.x = -10.5, cellSize = 1 → gridX = -11（向下取整）
        /// </summary>
        public GridNode WorldToGrid(FixVector2 worldPos)
        {
            // 使用向下取整，确保负数坐标正确处理
            int x = (int)Fix64.Floor(worldPos.x / cellSize);
            int y = (int)Fix64.Floor(worldPos.y / cellSize);
            return new GridNode(x, y);
        }
        
        /// <summary>
        /// 网格坐标转世界坐标（返回网格中心点）
        /// </summary>
        public FixVector2 GridToWorld(GridNode gridPos)
        {
            Fix64 worldX = (Fix64)gridPos.x * cellSize + cellSize / Fix64.Two;
            Fix64 worldY = (Fix64)gridPos.y * cellSize + cellSize / Fix64.Two;
            return new FixVector2(worldX, worldY);
        }
        
        public object Clone()
        {
            return new GridMapComponent
            {
                width = this.width,
                height = this.height,
                cellSize = this.cellSize,
                obstacles = new OrderedHashSet<GridNode>(this.obstacles)
            };
        }
        
        public override string ToString()
        {
            return $"{GetType().Name}: size=({width}, {height}), cellSize={cellSize}, obstacles={obstacles.Count}";
        }
    }
}

