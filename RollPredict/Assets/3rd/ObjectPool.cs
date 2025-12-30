using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frame.Core
{
    public class ObjectPool<T> where T : MonoBehaviour
{
    private Queue<T> objectPool = new Queue<T>();
    private T prefab; // 需要存储预制体用于实例化新对象
    
    private Action<T> onSpawn;
    private Action<T> onDespawn;

    // 构造函数，传入预制体
    public ObjectPool(T prefab,Action<T> onSpawn=null, Action<T> onDespawn=null)
    {
        this.prefab = prefab;
        this.onSpawn = onSpawn;
        this.onDespawn = onDespawn;
    }

    public T GetObject()
    {
        T obj;
        if (objectPool.Count > 0)
        {
            obj = objectPool.Dequeue();
        }
        else
        { 
            obj = GameObject.Instantiate(prefab);
        }
        onSpawn?.Invoke(obj);

        return obj;
    }


    public void ReturnObject(T obj)
    {
        
  
        
        onDespawn?.Invoke(obj);
        objectPool.Enqueue(obj);
    }
    
    
    public int GetSize()
    {
        return objectPool.Count;
    }
    }
}