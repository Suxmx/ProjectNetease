using System;
using System.Collections.Generic;
using UnityEngine.Profiling;

namespace MemoFramework
{
    public partial class EventComponent : MemoFrameworkComponent
    {
        private Dictionary<Type, object> _genericEventPoolDict = new();
        private LinkedList<Action> _genericEventUpdateList = new();

        private void Update()
        {
            var node = _genericEventUpdateList.First;
            while (node != null)
            {
                var next = node.Next;
                node.Value();
                node = next;
            }
        }

        /// <summary>
        /// 订阅事件处理函数。
        /// </summary>
        /// <param name="handler">要订阅的事件处理函数。</param>
        public void Subscribe<T>(EventHandler<T> handler) where T : MFEventArgs
        {
            EventPool<T> pool = InternalGetOrCreateEventPool<T>();
            pool.Subscribe(handler);
        }

        /// <summary>
        /// 取消订阅事件处理函数。
        /// </summary>
        /// <param name="handler">要取消订阅的事件处理函数。</param>
        public void Unsubscribe<T>(EventHandler<T> handler) where T : MFEventArgs
        {
            EventPool<T> pool = InternalGetOrCreateEventPool<T>();
            pool.Unsubscribe(handler);
        }

        public void Fire<T>(object sender, T e) where T : MFEventArgs
        {
            EventPool<T> pool = InternalGetOrCreateEventPool<T>();
            pool.Fire(sender, e);
        }

        public void FireNow<T>(object sender, T e) where T : MFEventArgs
        {
            Profiler.BeginSample("TestEventGet");
            EventPool<T> pool = InternalGetOrCreateEventPool<T>();
            Profiler.EndSample();
            pool.FireNow(sender, e);
        }

        public bool Check<T>(EventHandler<T> handler) where T : MFEventArgs
        {
            EventPool<T> pool = InternalGetOrCreateEventPool<T>();
            return pool.Check(handler);
        }

        /// <summary>
        /// 从事件池字典中取出或创建一个事件池。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>事件池</returns>
        private EventPool<T> InternalGetOrCreateEventPool<T>() where T : MFEventArgs
        {
            Type type = typeof(T);
            if (!_genericEventPoolDict.ContainsKey(type))
            {
                var pool = new EventPool<T>();
                _genericEventPoolDict.Add(type, pool);
                _genericEventUpdateList.AddLast(pool.Update);
            }

            return _genericEventPoolDict[type] as EventPool<T>;
        }
    }
}