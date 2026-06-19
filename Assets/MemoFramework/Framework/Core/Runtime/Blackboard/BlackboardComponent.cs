using System.Collections.Generic;
using UnityEngine;

namespace MemoFramework
{
    public class BlackboardComponent : MemoFrameworkComponent
    {
        private Dictionary<string, int> _intDict = new Dictionary<string, int>();
        private Dictionary<string, float> _floatDict = new Dictionary<string, float>();
        private Dictionary<string, string> _stringDict = new Dictionary<string, string>();
        private Dictionary<string, bool> _boolDict = new Dictionary<string, bool>();

        #region Int

        public int GetInt(string key)
        {
            if (_intDict.TryGetValue(key, out int value))
            {
                return value;
            }
            else
            {
                Debug.LogError("BlackboardComponent: 未找到键 " + key);
                return 0;
            }
        }

        public bool HasInt(string key)
        {
            return _intDict.ContainsKey(key);
        }

        public void SetInt(string key, int value)
        {
            if (_intDict.ContainsKey(key))
            {
                _intDict[key] = value;
            }
            else
            {
                _intDict.Add(key, value);
            }
        }

        #endregion

        #region Float

        public float GetFloat(string key)
        {
            if (_floatDict.TryGetValue(key, out float value))
            {
                return value;
            }
            else
            {
                Debug.LogError("BlackboardComponent: 未找到键 " + key);
                return 0f;
            }
        }

        public bool HasFloat(string key)
        {
            return _floatDict.ContainsKey(key);
        }

        public void SetFloat(string key, float value)
        {
            if (_floatDict.ContainsKey(key))
            {
                _floatDict[key] = value;
            }
            else
            {
                _floatDict.Add(key, value);
            }
        }

        #endregion

        #region String

        public string GetString(string key)
        {
            if (_stringDict.TryGetValue(key, out string value))
            {
                return value;
            }
            else
            {
                Debug.LogError("BlackboardComponent: 未找到键 " + key);
                return string.Empty;
            }
        }

        public bool HasString(string key)
        {
            return _stringDict.ContainsKey(key);
        }

        public void SetString(string key, string value)
        {
            if (_stringDict.ContainsKey(key))
            {
                _stringDict[key] = value;
            }
            else
            {
                _stringDict.Add(key, value);
            }
        }

        #endregion

        #region Bool

        public bool GetBool(string key)
        {
            if (_boolDict.TryGetValue(key, out bool value))
            {
                return value;
            }
            else
            {
                Debug.LogError("BlackboardComponent: 未找到键 " + key);
                return false;
            }
        }

        public bool HasBool(string key)
        {
            return _boolDict.ContainsKey(key);
        }

        public void SetBool(string key, bool value)
        {
            if (_boolDict.ContainsKey(key))
            {
                _boolDict[key] = value;
            }
            else
            {
                _boolDict.Add(key, value);
            }
        }
        #endregion
    }
}