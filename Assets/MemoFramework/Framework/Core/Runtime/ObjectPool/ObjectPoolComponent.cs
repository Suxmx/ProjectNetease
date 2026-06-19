using System;
using System.Collections.Generic;
using MemoFramework.ObjectPool;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MemoFramework
{
    public class ObjectPoolComponent : MemoFrameworkComponent
    {
        private Dictionary<string, ObjectPool.ObjectPool>
            _objectPools = new Dictionary<string, ObjectPool.ObjectPool>();

        public Transform Spawn(string poolName, object userData = null)
        {
            return Spawn(poolName, Vector3.zero, Quaternion.identity, null, userData);
        }

        public Transform Spawn(string poolName, Transform parent, object userData = null)
        {
            return Spawn(poolName, Vector3.zero, Quaternion.identity, parent, userData);
        }

        public Transform Spawn(string poolName, Vector3 pos, Quaternion rot, object userData = null)
        {
            return Spawn(poolName, pos, rot, null, userData);
        }

        public Transform Spawn(string poolName, Vector3 pos, Quaternion rot, Transform parent, object userData = null)
        {
            if (!_objectPools.ContainsKey(poolName))
            {
                Debug.LogError("未注册名为 " + poolName + "的对象池");
                return null;
            }

            IObject iobj = _objectPools[poolName].SpawnInstance(pos, rot);

            if (iobj is null)
            {
                return null;
            }

            iobj.Name = poolName;
            iobj.OnSpawned(userData);

            if (parent != null)
            {
                iobj.transform.parent = parent;
            }


            return iobj.transform;
        }

        public bool Despawn(IObject go)
        {
            if (go.transform.gameObject.IsDestroyed()) return false;
            go.OnDespawned();
            go.transform.parent = null;
            if (_objectPools.ContainsKey(go.Name))
                _objectPools[go.Name].DespawnInstance(go);
            else Destroy(go.transform.gameObject);
            return true;
        }

        public void DespawnAll(string prefabName)
        {
            if (_objectPools.ContainsKey(prefabName))
            {
                _objectPools[prefabName].DespawnAll();
            }
            else
            {
                Debug.LogError("未注册名为 " + prefabName + "的对象池");
            }
        }

        public ObjectPool.ObjectPool CreateObjectPool(string prefabName, GameObject prefab)
        {
            if (_objectPools.ContainsKey(prefabName))
            {
                Debug.LogError("Prefab already exists: " + prefabName);
                return null;
            }

            ObjectPool.ObjectPool pool = new ObjectPool.ObjectPool(prefabName, prefab, transform);
            _objectPools.Add(prefabName, pool);
            return pool;
        }

        public ObjectPool.ObjectPool CreateObjectPool(string prefabName, Func<GameObject> spawnFunc)
        {
            if (_objectPools.ContainsKey(prefabName))
            {
                Debug.LogError("Prefab already exists: " + prefabName);
                return null;
            }

            ObjectPool.ObjectPool pool = new ObjectPool.ObjectPool(prefabName, spawnFunc, transform);
            _objectPools.Add(prefabName, pool);
            return pool;
        }

        public void RemoveObjectPool(string prefabName)
        {
            if (_objectPools.ContainsKey(prefabName))
            {
                _objectPools[prefabName].DestroyAll();
                Destroy(_objectPools[prefabName].Transform.gameObject);
                _objectPools.Remove(prefabName);
            }
            else
            {
                Debug.LogError("未注册名为 " + prefabName + "的对象池");
            }
        }

        public ObjectPool.ObjectPool GetObjectPool(string prefabName)
        {
            if (_objectPools.ContainsKey(prefabName))
            {
                return _objectPools[prefabName];
            }
            else
            {
                Debug.LogError("未注册名为 " + prefabName + "的对象池");
                return null;
            }
        }
    }
}