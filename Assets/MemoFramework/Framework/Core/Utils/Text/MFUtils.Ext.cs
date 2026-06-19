using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MemoFramework
{
    public partial class MFUtils
    {
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            if (go is null || !go) return null;
            T comp = null;
            if (go.TryGetComponent<T>(out comp))
            {
                return comp;
            }

            return go.AddComponent<T>();
        }
        public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> items, Func<T, TKey> property)
        {
            return items.GroupBy(property).Select(x => x.First());
        }
        
        public static bool IsDestroyed(this Object target)
        {
            // Checks whether a Unity object is not actually a null reference,
            // but a rather destroyed native instance.

            return !ReferenceEquals(target, null) && target == null;
        }
    }
}