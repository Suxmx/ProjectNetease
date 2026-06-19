using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MemoFramework.ObjectPool
{
    public class ObjectPool
    {
        /// <summary>
        /// 利用预制体生成物体池
        /// </summary>
        /// <param name="poolName"></param>
        /// <param name="prefab"></param>
        /// <param name="parent"></param>
        public ObjectPool(string poolName, GameObject prefab, Transform parent)
        {
            Spawner = () => Object.Instantiate(prefab);
            Transform = new GameObject("ObjectPool_" + poolName).transform;
            Transform.parent = parent;
            Despawneds = new Queue<IObject>();
            Spawneds = new MFLinkedList<IObject>();
        }

        /// <summary>
        /// 利用生成函数生成物体池
        /// </summary>
        /// <param name="poolName"></param>
        /// <param name="spawner"></param>
        /// <param name="parent"></param>
        public ObjectPool(string poolName, Func<GameObject> spawner, Transform parent)
        {
            Spawner = spawner;
            Transform = new GameObject("ObjectPool_" + poolName).transform;
            Transform.parent = parent;
            Despawneds = new Queue<IObject>();
            Spawneds = new MFLinkedList<IObject>();
        }

        public Func<GameObject> Spawner { get; private set; }
        public Transform Transform { get; private set; }
        public Queue<IObject> Despawneds { get; private set; }
        public MFLinkedList<IObject> Spawneds { get; private set; }


        public IObject SpawnInstance(Vector3 pos, Quaternion rot)
        {
            if (Despawneds.Count > 0)
            {
                var io = Despawneds.Dequeue();
                if (io is null)
                {
                    Debug.LogError("要用对象池生成的物体上必须有IObject接口");
                    return null;
                }

                io.transform.position = pos;
                io.transform.rotation = rot;
                io.transform.parent = Transform;
                io.transform.gameObject.SetActive(true);
                Spawneds.AddLast(io);
                return io;
            }
            else
            {
                var go = Spawner();
                go.transform.position = pos;
                go.transform.rotation = rot;
                go.transform.parent = Transform;
                var io = go.GetComponent<IObject>();
                if (io is null)
                {
                    Object.Destroy(go);
                    Debug.LogError("要用对象池生成的物体上必须有IObject接口");
                    return null;
                }

                Spawneds.AddLast(io);

                return io;
            }
        }

        public void DespawnInstance(IObject io)
        {
            io.transform.gameObject.SetActive(false);
            io.transform.parent = Transform;
            io.transform.localScale = Vector3.one;
            Despawneds.Enqueue(io);
            Spawneds.Remove(io);
        }

        public void DespawnAll()
        {
            LinkedListNode<IObject> node = Spawneds.First;
            while (node != null)
            {
                var next = node.Next;
                node.Value.OnDespawned();
                DespawnInstance(node.Value);
                node = next;
            }
        }

        public void DestroyAll()
        {
            LinkedListNode<IObject> node = Spawneds.First;
            while (node != null)
            {
                var next = node.Next;
                node.Value.OnDespawned();
                Object.Destroy(node.Value.transform.gameObject);
                node = next;
            }
        }
    }
}