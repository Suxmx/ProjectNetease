using System;

namespace MemoFramework
{
    public static class MemoFrameworkEntry
    {
        private static MFLinkedList<MemoFrameworkComponent> s_MemoFrameworkComponents =
            new MFLinkedList<MemoFrameworkComponent>();

        /// <summary>
        /// 获取一个组件
        /// </summary>
        /// <typeparam name="T">组件种类</typeparam>
        /// <returns>组件</returns>
        public static T GetComponent<T>() where T : MemoFrameworkComponent
        {
            return (T) GetComponent(typeof(T));
        }
        
        /// <summary>
        /// 获取一个组件
        /// </summary>
        /// <param name="type">组件种类</param>
        /// <returns>组件</returns>
        public static MemoFrameworkComponent GetComponent(Type type)
        {
            foreach (var component in s_MemoFrameworkComponents)
            {
                if (component.GetType() == type)
                {
                    return component;
                }
            }

            return null;
        }
        
        /// <summary>
        /// 添加组件
        /// </summary>
        /// <param name="component">组件</param>
        /// <exception cref="MFException">组件重复</exception>
        public static void RegisterComponent(MemoFrameworkComponent component)
        {
            if (s_MemoFrameworkComponents.Contains(component))
            {
                throw new MFException("已存在Type为" + component.GetType().FullName + "的组件");
            }

            s_MemoFrameworkComponents.AddLast(component);
        }

        /// <summary>
        /// 移除组件
        /// </summary>
        /// <param name="component">组件</param>
        /// <exception cref="MFException">组件不存在</exception>
        public static void RemoveComponent(MemoFrameworkComponent component)
        {
            if (!s_MemoFrameworkComponents.Remove(component))
            {
                throw new MFException("不存在Type为" + component.GetType().FullName + "的组件");
            }
        }
    }
}