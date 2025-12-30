using UnityEngine;

namespace Frame.Core
{
    public class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new object();

    public static T Instance
    {
        get
        {
            
            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<T>();

                    if (_instance == null)
                    {
                        GameObject singletonObj = new GameObject($"{typeof(T)} (Singleton)");
                        _instance = singletonObj.AddComponent<T>();
                    }
                }
                return _instance;
            }
        }
    }
    }
}