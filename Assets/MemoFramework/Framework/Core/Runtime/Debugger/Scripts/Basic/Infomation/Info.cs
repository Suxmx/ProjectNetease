using System;
using UnityEngine;

namespace MemoFramework.Debugger.Information
{
    public class InfoEntry
    {
        public string Title { get; set; }

        public object Value
        {
            get
            {
                try
                {
                    return _valueGetter();
                }
                catch (Exception e)
                {
                    Debug.LogError(MFUtils.Text.Format("Error ({0})", e.GetType().Name));
                    return null;
                }
            }
        }


        private Func<object> _valueGetter;

        public static InfoEntry Create(string name, Func<object> getter)
        {
            return new InfoEntry
            {
                Title = name,
                _valueGetter = getter,
            };
        }

        public static InfoEntry Create(string name, object value)
        {
            return new InfoEntry
            {
                Title = name,
                _valueGetter = () => value,
            };
        }
    }
}