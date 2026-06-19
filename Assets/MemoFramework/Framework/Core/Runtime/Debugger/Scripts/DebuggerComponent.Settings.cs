using UnityEngine;

namespace MemoFramework
{
    public partial class DebuggerComponent
    {
        private const string kScaleKey = "MemoFramework.Debugger.Scale";
        private const string kEntryScaleKey = "MemoFramework.Debugger.EntryScale";
        private const string kAllowDragKey = "MemoFramework.Debugger.AllowDrag";
        private const string kDragProtectKey = "MemoFramework.Debugger.DragProtect";
        private const string kEntryAnchorKey = "MemoFramework.Debugger.EntryAnchor";
        private const string kShowFpsKey = "MemoFramework.Debugger.ShowFps";

        public float Scale
        {
            get => _scale;
            set
            {
                if (Mathf.Approximately(value, _scale)) return;
                _scale = value;
                _panel.localScale = Vector3.one * _scale;
                PlayerPrefs.SetFloat(kScaleKey, _scale);
            }
        }

        public bool AllowDrag
        {
            get => _allowDrag;
            set
            {
                if (value == _allowDrag) return;
                _allowDrag = value;
                PlayerPrefs.SetInt(kAllowDragKey, _allowDrag ? 1 : 0);
            }
        }

        public bool DragProtect
        {
            get => _dragProtect;
            set
            {
                if (value == _dragProtect) return;
                _dragProtect = value;
                PlayerPrefs.SetInt(kDragProtectKey, _dragProtect ? 1 : 0);
            }
        }

        public int EntryAnchor
        {
            get => _entryAnchor;
            set
            {
                if (value == _entryAnchor) return;
                _entryAnchor = value;
                _debuggerEntry.SetAnchor(value);
                PlayerPrefs.SetInt(kEntryAnchorKey, _entryAnchor);
            }
        }

        public bool ShowFps
        {
            get => _showFps;
            set
            {
                if (value == _showFps) return;
                _showFps = value;
                _debuggerEntry.EnableFps(_showFps);
                PlayerPrefs.SetInt(kShowFpsKey, _showFps ? 1 : 0);
            }
        }
        
        public float EntryScale
        {
            get => _entryScale;
            set
            {
                if (Mathf.Approximately(value, _entryScale)) return;
                _entryScale = value;
                _debuggerEntry.transform.localScale = Vector3.one * _entryScale;
                PlayerPrefs.SetFloat(kEntryScaleKey, _entryScale);
            }
        }

        private float _scale;
        private bool _allowDrag;
        private bool _dragProtect;
        private int _entryAnchor;
        private bool _showFps;
        private float _entryScale;

        private void LoadSettings()
        {
            _scale = PlayerPrefs.GetFloat(kScaleKey, 1);
            _allowDrag = PlayerPrefs.GetInt(kAllowDragKey, 1) == 1;
            _dragProtect = PlayerPrefs.GetInt(kDragProtectKey, 1) == 1;
            _entryAnchor = PlayerPrefs.GetInt(kEntryAnchorKey, 0);
            _showFps = PlayerPrefs.GetInt(kShowFpsKey, 1) == 1;
            _entryScale = PlayerPrefs.GetFloat(kEntryScaleKey, 1);
            // 应用
            _panel.localScale = Vector3.one * _scale;
            _debuggerEntry.SetAnchor(_entryAnchor);
            _debuggerEntry.EnableFps(_showFps);
            _debuggerEntry.transform.localScale = Vector3.one * _entryScale;
        }
    }
}