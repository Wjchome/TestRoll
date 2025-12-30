using System;

namespace Frame.Core
{
    public class Singleton
{
    private static Singleton _instance;
    private static readonly object _lock = new object();

    public static Singleton Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new Singleton();
                    }
                }
            }
            return _instance;
        }
    }

    private Singleton() { }

    public void DoSomething()
    {
        Console.WriteLine("Singleton method called.");
    }
    }
}