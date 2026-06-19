using System;
using System.Collections.Generic;
using System.Text;
using EnhancedUI.EnhancedScroller;
using UnityEngine;
using UnityEngine.UI;

namespace MemoFramework.Debugger
{
    public class ConsoleTab : TabBase, IEnhancedScrollerDelegate
    {
        private readonly List<LogNode> m_LogNodes = new List<LogNode>();
        private List<LogNode> _showLogNodes = new List<LogNode>();

        private int m_InfoCount = 0;
        private int m_WarningCount = 0;
        private int m_ErrorCount = 0;
        private int _chooseIndex = -1;

        // [SerializeField] private int m_MaxLine = 100;
        private bool m_InfoFilter = true;

        private bool m_WarningFilter = true;

        private bool m_ErrorFilter = true;

        [Header("设置")] [SerializeField] private Color32 m_InfoColor = Color.white;

        [SerializeField] private Color32 m_WarningColor = Color.yellow;

        [SerializeField] private Color32 m_ErrorColor = Color.red;
        [SerializeField] private EnhancedScroller _scroller;
        [Header("预制体")] [SerializeField] private LogNodeView _evenLogNodePrefab;
        [SerializeField] private LogNodeView _oddLogNodePrefab;
        [Header("UI")] [SerializeField] private Button _infoFilterBtn;
        [SerializeField] private Button _warningFilterBtn;
        [SerializeField] private Button _errorFilterBtn;
        [SerializeField] private Text _stackTraceText;
        [SerializeField] private Button _clearBtn;
        private CanvasGroup _infoFilterCg;
        private CanvasGroup _warningFilterCg;
        private CanvasGroup _errorFilterCg;
        private Text _infoFilterText;
        private Text _warningFilterText;
        private Text _errorFilterText;

        private StringBuilder _cachedSb = new();

        public bool InfoFilter
        {
            get => m_InfoFilter;
            set { m_InfoFilter = value; }
        }

        public bool WarningFilter
        {
            get => m_WarningFilter;
            set { m_WarningFilter = value; }
        }

        public bool ErrorFilter
        {
            get => m_ErrorFilter;
            set { m_ErrorFilter = value; }
        }


        public override void OnRegister()
        {
            _scroller.Delegate = this;
            // 获取Filter按钮组件
            _infoFilterCg = _infoFilterBtn.GetComponent<CanvasGroup>();
            _warningFilterCg = _warningFilterBtn.GetComponent<CanvasGroup>();
            _errorFilterCg = _errorFilterBtn.GetComponent<CanvasGroup>();
            _infoFilterText = _infoFilterBtn.GetComponentInChildren<Text>();
            _warningFilterText = _warningFilterBtn.GetComponentInChildren<Text>();
            _errorFilterText = _errorFilterBtn.GetComponentInChildren<Text>();
            // 注册按钮事件
            _infoFilterBtn.onClick.AddListener(() =>
            {
                InfoFilter = !InfoFilter;
                _infoFilterCg.alpha = (!InfoFilter ? 0.1f : 1);
                CollectFilterNodes();
                SetStackTraceText(_chooseIndex);
            });
            _warningFilterBtn.onClick.AddListener(() =>
            {
                WarningFilter = !WarningFilter;
                _warningFilterCg.alpha = (!WarningFilter ? 0.1f : 1);
                CollectFilterNodes();
                SetStackTraceText(_chooseIndex);
            });
            _errorFilterBtn.onClick.AddListener(() =>
            {
                ErrorFilter = !ErrorFilter;
                _errorFilterCg.alpha = (!ErrorFilter ? 0.1f : 1);
                CollectFilterNodes();
                SetStackTraceText(_chooseIndex);
            });
            _clearBtn.onClick.AddListener(ClearLog);
            // 初始化按钮状态
            _infoFilterCg.alpha = (!InfoFilter ? 0.1f : 1);
            _warningFilterCg.alpha = (!WarningFilter ? 0.1f : 1);
            _errorFilterCg.alpha = (!ErrorFilter ? 0.1f : 1);
            
            _scroller.ReloadData(1);
            Application.logMessageReceived += OnLogMessageReceived;
        }


        public override void OnUnregister()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
        }

        public override void OnSelect()
        {
        }

        public override void OnDeselect()
        {
        }

        public override void OnUpdate()
        {
            // if (Input.GetKeyDown(KeyCode.T))
            // {
            //     MFLogger.LogInfo("Test");
            // }
            //
            // if (Input.GetKeyDown(KeyCode.Y))
            // {
            //     MFLogger.LogWarning("Test");
            // }
            //
            // if (Input.GetKeyDown(KeyCode.U))
            // {
            //     MFLogger.LogError("Test");
            // }
        }

        private void OnLogMessageReceived(string logMessage, string stackTrace, LogType logType)
        {
            stackTrace = FilterLogStackTrace(stackTrace);
            if (logType == LogType.Assert)
            {
                logType = LogType.Error;
            }

            if (logType == LogType.Log)
            {
                m_InfoCount++;
                
                _infoFilterText.text = m_InfoCount.ToString();
            }
            else if (logType == LogType.Warning)
            {
                m_WarningCount++;
                _warningFilterText.text = m_WarningCount.ToString();
            }
            else if (logType == LogType.Error)
            {
                m_ErrorCount++;
                _errorFilterText.text = m_ErrorCount.ToString();
            }

            m_LogNodes.Add(LogNode.Create(logType, logMessage, stackTrace));
            AddShowLogNode(m_LogNodes[^1]);
            _scroller.ReloadData(1);
        }

        private string GetLogString(LogNode logNode)
        {
            Color32 color = GetLogStringColor(logNode.LogType);
            return MFUtils.Text.Format("[{0:HH:mm:ss}]{1}", logNode.LogTime.ToLocalTime(), logNode.LogMessage);
        }

        private Color32 GetLogStringColor(LogType logType)
        {
            Color32 color = Color.white;
            switch (logType)
            {
                case LogType.Log:
                    color = m_InfoColor;
                    break;

                case LogType.Warning:
                    color = m_WarningColor;
                    break;

                case LogType.Error:
                    color = m_ErrorColor;
                    break;
            }

            return color;
        }

        private void AddShowLogNode(LogNode node)
        {
            if (node.LogType == LogType.Log && !m_InfoFilter)
            {
                return;
            }

            if (node.LogType == LogType.Warning && !m_WarningFilter)
            {
                return;
            }

            if (node.LogType == LogType.Error && !m_ErrorFilter)
            {
                return;
            }

            _showLogNodes.Add(node);
        }

        private string FilterLogStackTrace(string stackTrace)
        {
            _cachedSb.Clear();
            // 分割每一行
            string[] lines = stackTrace.Split(new[] { "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("UnityEngine.Debug:Log") && !lines[i].Contains("MemoFramework.MFLogger") &&
                    !lines[i].Contains("(MemoFramework.MFLogLevel,object)"))
                {
                    _cachedSb.AppendLine(lines[i]);
                }
            }

            return _cachedSb.ToString();
        }

        private void ClearLog()
        {
            m_LogNodes.Clear();
            _showLogNodes.Clear();
            m_InfoCount = 0;
            m_WarningCount = 0;
            m_ErrorCount = 0;
            _infoFilterText.text = "0";
            _warningFilterText.text = "0";
            _errorFilterText.text = "0";
            _chooseIndex = -1;
            _stackTraceText.text = "";
            _scroller.ReloadData(1);
        }

        private void SetStackTraceText(int index)
        {
            if (index < 0 || index >= _showLogNodes.Count)
            {
                _stackTraceText.text = "";
                return;
            }

            _stackTraceText.text = MFUtils.Text.Format("{0}\n{1}", _showLogNodes[index].LogMessage,
                _showLogNodes[index].StackTrack);
        }

        private void CollectFilterNodes()
        {
            _showLogNodes.Clear();
            foreach (var node in m_LogNodes)
            {
                AddShowLogNode(node);
            }

            _scroller.ReloadData(1);
        }

        public int GetNumberOfCells(EnhancedScroller scroller)
        {
            return _showLogNodes.Count;
        }

        public float GetCellViewSize(EnhancedScroller scroller, int dataIndex)
        {
            return 20;
        }

        public EnhancedScrollerCellView GetCellView(EnhancedScroller scroller, int dataIndex, int cellIndex)
        {
            LogNodeView logNodeViewPrefab = dataIndex % 2 == 0
                ? _evenLogNodePrefab
                : _oddLogNodePrefab;
            LogNodeView logNodeView = scroller.GetCellView(logNodeViewPrefab) as LogNodeView;
            logNodeView.SetData(GetLogString(_showLogNodes[dataIndex]),
                GetLogStringColor(_showLogNodes[dataIndex].LogType), this, dataIndex == _chooseIndex);
            return logNodeView;
        }

        public void ChooseLogNode(int index)
        {
            _chooseIndex = index;
            SetStackTraceText(index);
            _scroller.ReloadData(_scroller.NormalizedScrollPosition);
        }
    }
}