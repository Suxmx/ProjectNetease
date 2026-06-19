using UnityEngine;

namespace MemoFramework.ObjectPool
{
    public interface IObject
    {
        public string Name { get; set; }
        public Transform transform { get; }

        /// <summary>
        /// 被对象池生成时调用
        /// </summary>
        public void OnSpawned(object userData = null);

        /// <summary>
        /// 被对象池回收时调用
        /// </summary>
        public void OnDespawned();
    }
}