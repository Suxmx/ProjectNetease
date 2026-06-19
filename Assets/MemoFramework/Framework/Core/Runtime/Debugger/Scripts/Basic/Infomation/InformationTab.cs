using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace MemoFramework.Debugger.Information
{
    public class InformationTab : TabBase
    {
        private const float RefreshInterval = 1f;
        private const string kAutoRefreshKey = "MemoFramework.Debugger.InformationTab.AutoRefresh";
        
        private int _maxLength = -1;
        private bool _isInit;
        private bool _autoRefresh;
        private float _refreshTimer;
        private List<InfoEntry> _systemInfos;
        private List<InfoEntry> _unityInfos;
        private List<InfoEntry> _displayInfos;
        private List<InfoEntry> _runtimeInfos;
        private List<InfoEntry> _graphicsDeviceInfos;
        private Text _refreshText;
        

        [SerializeField] private Text _systemInfoText;
        [SerializeField] private Text _unityInfoText;
        [SerializeField] private Text _displayInfoText;
        [SerializeField] private Text _runtimeInfoText;
        [SerializeField] private Text _graphicsDeviceInfoText;
        [SerializeField] private Button _refreshBtn;
        [SerializeField] private Button _switchAutoRefreshBtn;

        public override void OnRegister()
        {
            InitInfos();
            // 初始化刷新按钮设置和文字
            _refreshText = _switchAutoRefreshBtn.GetComponentInChildren<Text>();
            _autoRefresh = PlayerPrefs.GetInt(kAutoRefreshKey, 1) == 1;
            _refreshText.text = !_autoRefresh ? "Manual Refresh" : "Auto Refresh";
            _refreshBtn.gameObject.SetActive(!_autoRefresh);
            _refreshTimer = 0;
            _refreshBtn.onClick.AddListener(Refresh);
            _switchAutoRefreshBtn.onClick.AddListener(() =>
            {
                _autoRefresh = !_autoRefresh;
                PlayerPrefs.SetInt(kAutoRefreshKey, _autoRefresh ? 1 : 0);
                _refreshText.text = !_autoRefresh ? "Manual Refresh" : "Auto Refresh";
                _refreshBtn.gameObject.SetActive(!_autoRefresh);
            });
            
            _isInit = true;
        }

        public override void OnUnregister()
        {
            _systemInfos.Clear();
            _unityInfos.Clear();
            _displayInfos.Clear();
            _runtimeInfos.Clear();
            _graphicsDeviceInfos.Clear();
        }

        public override void OnSelect()
        {
            Refresh();
        }

        public override void OnDeselect()
        {
        }

        public override void OnUpdate()
        {
            if (_autoRefresh)
            {
                _refreshTimer += Time.unscaledDeltaTime;
                if (_refreshTimer >= RefreshInterval)
                {
                    _refreshTimer = 0;
                    Refresh();
                }
            }
        }

        private void InitInfos()
        {
#if ENABLE_IL2CPP
            const string IL2CPP = "Yes";
#else
            const string IL2CPP = "No";
#endif
            _systemInfos = new()
            {
                InfoEntry.Create("Operating System", UnityEngine.SystemInfo.operatingSystem),
                InfoEntry.Create("Device Name", UnityEngine.SystemInfo.deviceName),
                InfoEntry.Create("Device Type", UnityEngine.SystemInfo.deviceType),
                InfoEntry.Create("Device Model", UnityEngine.SystemInfo.deviceModel),
                InfoEntry.Create("CPU Type", UnityEngine.SystemInfo.processorType),
                InfoEntry.Create("CPU Count", UnityEngine.SystemInfo.processorCount),
                InfoEntry.Create("System Memory",
                    GetBytesReadable(((long)UnityEngine.SystemInfo.systemMemorySize) * 1024 * 1024))
            };

            _unityInfos = new()
            {
                InfoEntry.Create("Version", Application.unityVersion),
                InfoEntry.Create("Debug", Debug.isDebugBuild),
                InfoEntry.Create("Unity Pro", Application.HasProLicense()),
                InfoEntry.Create("Genuine", MFUtils.Text.Format("{0} ({1})", Application.genuine ? "Yes" : "No",
                    Application.genuineCheckAvailable ? "Trusted" : "Untrusted")),
                InfoEntry.Create("System Language", Application.systemLanguage),
                InfoEntry.Create("Platform", Application.platform),
                InfoEntry.Create("Install Mode", Application.installMode),
                InfoEntry.Create("Sandbox", Application.sandboxType),
                InfoEntry.Create("IL2CPP", IL2CPP),
                InfoEntry.Create("Application Version", Application.version),
                InfoEntry.Create("Application Id", Application.identifier),
            };

            _displayInfos = new()
            {
                InfoEntry.Create("Resolution", () => Screen.width + "x" + Screen.height),
                InfoEntry.Create("DPI", () => Screen.dpi),
                InfoEntry.Create("Fullscreen", () => Screen.fullScreen),
                InfoEntry.Create("Fullscreen Mode", () => Screen.fullScreenMode),
                InfoEntry.Create("Orientation", () => Screen.orientation),
            };

            _runtimeInfos = new()
            {
                InfoEntry.Create("Play Time", () => Time.unscaledTime),
                InfoEntry.Create("Level Play Time", () => Time.timeSinceLevelLoad),
                InfoEntry.Create("Current Level", () =>
                {
                    var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    return MFUtils.Text.Format("{0} (Index: {1})", activeScene.name, activeScene.buildIndex);
                }),
                InfoEntry.Create("Quality Level",
                    () =>
                        QualitySettings.names[QualitySettings.GetQualityLevel()] + " (" +
                        QualitySettings.GetQualityLevel() + ")")
            };

            _graphicsDeviceInfos = new()
            {
                InfoEntry.Create("Device Name", UnityEngine.SystemInfo.graphicsDeviceName),
                InfoEntry.Create("Device Vendor", UnityEngine.SystemInfo.graphicsDeviceVendor),
                InfoEntry.Create("Device Version", UnityEngine.SystemInfo.graphicsDeviceVersion),
                InfoEntry.Create("Graphics Memory",
                    GetBytesReadable(((long)UnityEngine.SystemInfo.graphicsMemorySize) * 1024 * 1024)),
                InfoEntry.Create("Max Tex Size", UnityEngine.SystemInfo.maxTextureSize),
            };
            
            GetMaxLength();
            
            Refresh();
        }

        private void Refresh()
        {
            if (!_isInit)
            {
                return;
            }

            _systemInfoText.text = GetInfoStr(_systemInfos);
            _unityInfoText.text = GetInfoStr(_unityInfos);
            _displayInfoText.text = GetInfoStr(_displayInfos);
            _runtimeInfoText.text = GetInfoStr(_runtimeInfos);
            _graphicsDeviceInfoText.text = GetInfoStr(_graphicsDeviceInfos);
        }

        private const string NameColor = "#BCBCBC";
        private const char Tick = '\u2713';
        private const char Cross = '\u00D7';

        private void GetMaxLength()
        {
            _maxLength = -1;
            foreach (var info in _systemInfos)
            {
                if (info.Title.Length > _maxLength)
                {
                    _maxLength = info.Title.Length;
                }
            }

            foreach (var info in _unityInfos)
            {
                if (info.Title.Length > _maxLength)
                {
                    _maxLength = info.Title.Length;
                }
            }

            foreach (var info in _displayInfos)
            {
                if (info.Title.Length > _maxLength)
                {
                    _maxLength = info.Title.Length;
                }
            }

            foreach (var info in _runtimeInfos)
            {
                if (info.Title.Length > _maxLength)
                {
                    _maxLength = info.Title.Length;
                }
            }

            foreach (var info in _graphicsDeviceInfos)
            {
                if (info.Title.Length > _maxLength)
                {
                    _maxLength = info.Title.Length;
                }
            }

            _maxLength += 2;
        }

        private string GetInfoStr(List<InfoEntry> infos)
        {
            var sb = new StringBuilder();
            var first = true;
            foreach (var i in infos)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    sb.AppendLine();
                }

                sb.Append("<color=");
                sb.Append(NameColor);
                sb.Append(">");

                sb.Append(i.Title);
                sb.Append(": ");

                sb.Append("</color>");

                for (float j = i.Title.Length; j <= _maxLength; j += 1)
                {
                    sb.Append(' ');
                }

                if (i.Value is bool)
                {
                    sb.Append((bool)i.Value ? Tick : Cross);
                }
                else
                {
                    sb.Append(i.Value);
                }
            }

            return sb.ToString();
        }

        private static string GetBytesReadable(long i)
        {
            var sign = (i < 0 ? "-" : "");
            double readable = (i < 0 ? -i : i);
            string suffix;
            if (i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = i >> 50;
            }
            else if (i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = i >> 40;
            }
            else if (i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = i >> 30;
            }
            else if (i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = i >> 20;
            }
            else if (i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = i >> 10;
            }
            else if (i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString(sign + "0 B"); // Byte
            }

            readable /= 1024;

            return sign + readable.ToString("0.### ") + suffix;
        }
    }
}