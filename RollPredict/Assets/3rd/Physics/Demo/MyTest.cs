using System;
using System.Collections.Generic;
using System.Linq;
using Frame.FixMath;
using Frame.Physics2D;
using Frame.Physics3D;

using UnityEngine;

public class MyTest : MonoBehaviour
{
    public bool d2, d3;

    public List<RigidBody2DComponent> Body2d;
    public List<RigidBody3DComponent> Body3d;
    


    public void Start()
    {
        if (d3)
        {
            for (int i = 0; i < Body3d.Count; i++)
            {
                PhysicsWorld3DComponent.Instance.AddRigidBody(Body3d[i], (FixVector3)Body3d[i].transform.position,
                    PhysicsLayer.Everything);
            }
        }

        if (d2)
        {
            for (int i = 0; i < Body2d.Count; i++)
            {
                PhysicsWorld2DComponent.Instance.AddRigidBody(Body2d[i],
                    (FixVector2)(Vector2)Body2d[i].transform.position, PhysicsLayer.Everything);
            }
        }
    }

    public int Count = 0;

    private void Update()
    {
        if (Count++ % 10 == 0)
        {
            if (d2)
            {
                PhysicsWorld2DComponent.Instance.UpdateFrame();
            }

            if (d3)
            {
                PhysicsWorld3DComponent.Instance.UpdateFrame();
            }
            
        }
    }
}