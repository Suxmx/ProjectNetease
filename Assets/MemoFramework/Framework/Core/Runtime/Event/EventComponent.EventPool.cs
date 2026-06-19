using System;
using System.Collections.Generic;

namespace MemoFramework
{
    public partial class EventComponent
    {
        private sealed partial class EventPool<T> where T : MFEventArgs
        {
            private readonly MFLinkedList<EventHandler<T>> m_EventHandlers;
            private readonly Queue<Event> m_Events;
            private LinkedListNode<EventHandler<T>> m_CachedNode;

            /// <summary>
            /// 初始化事件池的新实例。
            /// </summary>
            public EventPool()
            {
                m_EventHandlers = new MFLinkedList<EventHandler<T>>();
                m_Events = new Queue<Event>();
                m_CachedNode = null;
            }

            /// <summary>
            /// 获取事件处理函数的数量。
            /// </summary>
            public int EventHandlerCount
            {
                get { return m_EventHandlers.Count; }
            }

            /// <summary>
            /// 获取事件数量。
            /// </summary>
            public int EventCount
            {
                get { return m_Events.Count; }
            }

            /// <summary>
            /// 事件池轮询。
            /// </summary>
            public void Update()
            {
                lock (m_Events)
                {
                    while (m_Events.Count > 0)
                    {
                        Event eventNode = m_Events.Dequeue();
                        HandleEvent(eventNode.Sender, eventNode.EventArgs);
                        MFRefPool.Release(eventNode);
                    }
                }
            }

            /// <summary>
            /// 关闭并清理事件池。
            /// </summary>
            public void Shutdown()
            {
                Clear();
                m_EventHandlers.Clear();
            }

            /// <summary>
            /// 清理事件。
            /// </summary>
            public void Clear()
            {
                lock (m_Events)
                {
                    m_Events.Clear();
                }
            }

            /// <summary>
            /// 获取事件处理函数的数量。
            /// </summary>
            /// <returns>事件处理函数的数量。</returns>
            public int Count()
            {
                return m_EventHandlers.Count;
            }

            /// <summary>
            /// 检查是否存在事件处理函数。
            /// </summary>
            /// <param name="handler">要检查的事件处理函数。</param>
            /// <returns>是否存在事件处理函数。</returns>
            public bool Check(EventHandler<T> handler)
            {
                if (handler == null)
                {
                    throw new MFException("Event handler is invalid.");
                }

                return m_EventHandlers.Contains(handler);
            }

            /// <summary>
            /// 订阅事件处理函数。
            /// </summary>
            /// <param name="handler">要订阅的事件处理函数。</param>
            public void Subscribe(EventHandler<T> handler)
            {
                if (handler == null)
                {
                    throw new MFException("不能订阅空的事件处理函数.");
                }

                if (Check(handler)) // 检查是否已经存在相同的事件处理函数
                {
                    throw new MFException(
                        MFUtils.Text.Format("Event '{0}' 不允许重复的事件处理函数.", typeof(T)));
                }
                else
                {
                    m_EventHandlers.AddLast(handler);
                }
            }

            /// <summary>
            /// 取消订阅事件处理函数。
            /// </summary>
            /// <param name="handler">要取消订阅的事件处理函数。</param>
            public void Unsubscribe(EventHandler<T> handler)
            {
                if (handler == null)
                {
                    throw new MFException("不能取消订阅空的事件处理函数.");
                }

                if (m_CachedNode != null)
                {
                    if (m_CachedNode.Value == handler)
                    {
                        m_CachedNode = m_CachedNode.Next;
                    }
                }

                if (!m_EventHandlers.Remove(handler))
                {
                    throw new MFException(MFUtils.Text.Format("Event '{0}' 无指定的事件处理函数.", typeof(T)));
                }
            }

            /// <summary>
            /// 抛出事件，这个操作是线程安全的，即使不在主线程中抛出，也可保证在主线程中回调事件处理函数，但事件会在抛出后的下一帧分发。
            /// </summary>
            /// <param name="sender">事件源。</param>
            /// <param name="e">事件参数。</param>
            public void Fire(object sender, T e)
            {
                if (e == null)
                {
                    throw new MFException("不能用空的参数抛出事件.");
                }

                Event eventNode = Event.Create(sender, e);
                lock (m_Events)
                {
                    m_Events.Enqueue(eventNode);
                }
            }

            /// <summary>
            /// 抛出事件立即模式，这个操作不是线程安全的，事件会立刻分发。
            /// </summary>
            /// <param name="sender">事件源。</param>
            /// <param name="e">事件参数。</param>
            public void FireNow(object sender, T e)
            {
                if (e == null)
                {
                    throw new MFException("不能用空的参数抛出事件.");
                }

                HandleEvent(sender, e);
            }

            /// <summary>
            /// 处理事件结点。
            /// </summary>
            /// <param name="sender">事件源。</param>
            /// <param name="e">事件参数。</param>
            private void HandleEvent(object sender, T e)
            {
                if (m_EventHandlers.Count > 0)
                {
                    LinkedListNode<EventHandler<T>> current = m_EventHandlers.First;
                    while (current != null)
                    {
                        m_CachedNode = current.Next;
                        current.Value(sender, e);
                        current = m_CachedNode;
                        m_CachedNode = null;
                    }
                }

                MFRefPool.Release(e);
            }
        }
    }
}