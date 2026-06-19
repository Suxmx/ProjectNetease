namespace MemoFramework
{
    public class Observable<T>
    {
        public Observable()
        {
        }

        public Observable(T value)
        {
            _value = value;
        }

        public delegate void ValueChangedHandler(T oldValue, T newValue);

        public ValueChangedHandler OnValueChanged;

        private T _value;

        public T Value
        {
            get { return _value; }
            set
            {
                if (!Equals(_value, value))
                {
                    T old = _value;
                    _value = value;
                    ValueChanged(old, _value);
                }
            }
        }
        
        public void ForceInvoke()
        {
            ValueChanged(_value, _value);
        }

        private void ValueChanged(T oldValue, T newValue)
        {
            if (OnValueChanged != null)
            {
                OnValueChanged(oldValue, newValue);
            }
        }

        public override string ToString()
        {
            return (Value != null ? Value.ToString() : "null");
        }
    }
}