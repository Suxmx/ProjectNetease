using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace MemoFramework
{
    public class ObservableList<T> : IList<T>
    {
        private StringBuilder _sb = null;

        public delegate void ValueChangedHandler(List<T> oldValue, List<T> newValue);

        public ValueChangedHandler OnReferenceChanged;

        public delegate void ListChangeHandler(List<T> list);

        public ListChangeHandler OnListChanged;

        public delegate void AddHandler(T instance);

        public AddHandler OnAdd;

        public delegate void InsertHandler(int index, T instance);

        public InsertHandler OnInsert;

        public delegate void RemoveHandler(T instance);

        public RemoveHandler OnRemove;

        //预先初始化，防止空异常
        private List<T> _value = new List<T>();

        public List<T> Value
        {
            get { return _value; }
            set
            {
                if (!Equals(_value, value))
                {
                    var old = _value;
                    _value = value;
                    ValueChanged(old, _value);
                }
            }
        }

        private void ValueChanged(List<T> oldValue, List<T> newValue)
        {
            if (OnReferenceChanged != null)
            {
                OnReferenceChanged(oldValue, newValue);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _value.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            _value.Add(item);
            OnListChanged?.Invoke(_value);
            if (OnAdd != null)
            {
                OnAdd(item);
            }
        }

        public void Clear()
        {
            _value.Clear();
            OnListChanged?.Invoke(_value);
        }

        public bool Contains(T item)
        {
            return _value.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _value.CopyTo(array, arrayIndex);
        }

        public void CopyFrom(List<T> array)
        {
            _value.Clear();
            _value.AddRange(array);
            OnListChanged?.Invoke(_value);
        }

        public bool Remove(T item)
        {
            if (_value.Remove(item))
            {
                OnListChanged?.Invoke(_value);
                if (OnRemove != null)
                {
                    OnRemove(item);
                }

                return true;
            }

            return false;
        }

        public int Count
        {
            get { return _value.Count; }
        }

        public bool IsReadOnly { get; private set; }

        public int IndexOf(T item)
        {
            return _value.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            _value.Insert(index, item);
            OnListChanged?.Invoke(_value);
            if (OnInsert != null)
            {
                OnInsert(index, item);
            }
        }

        public void RemoveAt(int index)
        {
            _value.RemoveAt(index);
            OnListChanged?.Invoke(_value);
        }

        public T this[int index]
        {
            get { return _value[index]; }
            set { _value[index] = value; }
        }

        public override string ToString()
        {
            _sb ??= new StringBuilder();
            _sb.Clear();
            _sb.Append("[");
            for (int i = 0; i < _value.Count; i++)
            {
                _sb.Append(_value[i]);
                if (i != _value.Count - 1)
                {
                    _sb.Append(", ");
                }
            }

            _sb.Append("]");
            return _sb.ToString();
        }
    }
}