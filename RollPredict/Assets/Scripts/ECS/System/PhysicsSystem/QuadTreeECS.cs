using System.Collections.Generic;
using System.Linq;
using Frame.FixMath;
using Frame.Physics2D;

namespace Frame.ECS
{
    /// <summary>
    /// ECS版本的四叉树（简化版，只支持Entity ID和AABB）
    /// 用于宽相位碰撞检测优化
    /// </summary>
    public class QuadTreeECS
    {
        private class QuadTreeNodeECS
        {
            public FixRect Rect;
            private int _currentDepth;
            private int _maxObjects;
            private int _maxDepth;

            private List<Entity> _entitys; // 存储Entity 
            private Dictionary<Entity, FixRect> _entityBounds; // Entity  -> AABB映射

            public QuadTreeNodeECS LeftUp, RightUp, LeftDown, RightDown;
            public bool IsSplit => LeftUp != null;

            public QuadTreeNodeECS(FixRect rect, int currentDepth, int maxObjects, int maxDepth)
            {
                Rect = rect;
                _currentDepth = currentDepth;
                _maxObjects = maxObjects;
                _maxDepth = maxDepth;
                _entitys = new List<Entity>();
                _entityBounds = new Dictionary<Entity, FixRect>();
            }

            public void Add(Entity entity, FixRect bounds)
            {
                if (IsSplit)
                {
                    AddToChildNodes(entity, bounds);
                    return;
                }

                _entitys.Add(entity);
                _entityBounds[entity] = bounds;

                if (_entitys.Count > _maxObjects && _currentDepth < _maxDepth)
                {
                    Split();
                    foreach (var id in _entitys)
                    {
                        AddToChildNodes(id, _entityBounds[id]);
                    }

                    _entitys.Clear();
                    _entityBounds.Clear();
                }
            }

            private void Split()
            {
                Fix64 halfWidth = Rect.Width / Fix64.Two;
                Fix64 halfHeight = Rect.Height / Fix64.Two;
                Fix64 x = Rect.X;
                Fix64 y = Rect.Y;

                LeftUp = new QuadTreeNodeECS(new FixRect(x, y + halfHeight, halfWidth, halfHeight), _currentDepth + 1,
                    _maxObjects, _maxDepth);
                RightUp = new QuadTreeNodeECS(new FixRect(x + halfWidth, y + halfHeight, halfWidth, halfHeight),
                    _currentDepth + 1, _maxObjects, _maxDepth);
                LeftDown = new QuadTreeNodeECS(new FixRect(x, y, halfWidth, halfHeight), _currentDepth + 1, _maxObjects,
                    _maxDepth);
                RightDown = new QuadTreeNodeECS(new FixRect(x + halfWidth, y, halfWidth, halfHeight), _currentDepth + 1,
                    _maxObjects, _maxDepth);
            }

            private void AddToChildNodes(Entity entity, FixRect bounds)
            {
                if (LeftUp.Rect.Overlaps(bounds)) LeftUp.Add(entity, bounds);
                if (RightUp.Rect.Overlaps(bounds)) RightUp.Add(entity, bounds);
                if (LeftDown.Rect.Overlaps(bounds)) LeftDown.Add(entity, bounds);
                if (RightDown.Rect.Overlaps(bounds)) RightDown.Add(entity, bounds);
            }

            public void Query(FixRect area, OrderedHashSet<Entity> result)
            {
                if (!Rect.Overlaps(area)) return;

                if (!IsSplit)
                {
                    foreach (var entity in _entitys)
                    {
                        if (_entityBounds.TryGetValue(entity, out var bounds) && bounds.Overlaps(area))
                        {
                            result.Add(entity);
                        }
                    }

                    return;
                }

                LeftUp?.Query(area, result);
                RightUp?.Query(area, result);
                LeftDown?.Query(area, result);
                RightDown?.Query(area, result);
            }

            public void Remove(Entity entity)
            {
                if (!IsSplit)
                {
                    _entitys.Remove(entity);
                    _entityBounds.Remove(entity);
                    return;
                }

                LeftUp?.Remove(entity);
                RightUp?.Remove(entity);
                LeftDown?.Remove(entity);
                RightDown?.Remove(entity);
            }

            public void Clear()
            {
                _entitys.Clear();
                _entityBounds.Clear();
                LeftUp = RightUp = LeftDown = RightDown = null;
            }
        }

        private FixRect _rootRect;
        private QuadTreeNodeECS _rootNode;
        private int _maxObjectsPerNode = 4;
        private int _maxDepth = 5;

        public void Init(FixRect worldBounds)
        {
            _rootRect = worldBounds;
            _rootNode = new QuadTreeNodeECS(_rootRect, 0, _maxObjectsPerNode, _maxDepth);
        }


        public void Add(Entity entity, FixRect bounds)
        {
            _rootNode.Add(entity, bounds);
        }

        public void Remove(Entity entity)
        {
            _rootNode.Remove(entity);
        }


        public List<Entity> Query(FixRect area)
        {
            var result = new OrderedHashSet<Entity>();
            _rootNode.Query(area, result);
            return result.ToList();
        }

        public void Clear()
        {
            _rootNode.Clear();
            _rootNode = new QuadTreeNodeECS(_rootRect, 0, _maxObjectsPerNode, _maxDepth);
        }
    }
}