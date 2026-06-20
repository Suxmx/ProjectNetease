using System.Text;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 技能诊断日志器。每次 Play 自动新建带时间戳和进程 ID 的日志文件，
    /// 把诊断输出累积到内存缓冲区，由 <see cref="SkillDiagFlushDriver"/> 每帧末尾一次性 flush 到文件，避免 tick 内同步 IO 卡顿。
    /// 文件位于 <see cref="Application.persistentDataPath"/>，启动时 Debug.Log 路径。
    /// 可用菜单 Tools/Battle/Open SkillDiag Log Folder 快速打开文件夹。
    /// </summary>
    public static class SkillDiagLogger
    {
        private static string _path;
        private static bool _enabled;
        private static readonly StringBuilder _buffer = new(1 << 16);
        private static int _bufferedLines;
        private const int FlushThreshold = 256;

        /// <summary>当前日志文件路径，未启用时为 null。</summary>
        public static string CurrentPath => _path;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetFile()
        {
            _buffer.Clear();
            _bufferedLines = 0;

            string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            _path = System.IO.Path.Combine(Application.persistentDataPath, $"SkillDiag_{stamp}_{pid}.log");
            string header = $"=== SkillDiag session {stamp} pid={pid} ===";
            try
            {
                System.IO.File.WriteAllText(_path, header + System.Environment.NewLine);
                _enabled = true;
                Debug.Log($"[SkillDiag] log file created: {_path}");
            }
            catch (System.Exception e)
            {
                _enabled = false;
                Debug.LogWarning($"[SkillDiag] cannot create log file: {e.Message}");
            }
        }

        /// <summary>写一行诊断日志到内存缓冲区（tick 内调用，无 IO），由 SkillDiagFlushDriver 每帧 flush。</summary>
        public static void Log(string line)
        {
            if (!_enabled || string.IsNullOrEmpty(_path))
            {
                Debug.Log(line);
                return;
            }

            _buffer.Append("f").Append(Time.frameCount.ToString().PadLeft(6))
                   .Append(' ').Append(Time.time.ToString("0.000").PadLeft(8)).Append('s')
                   .Append(' ').AppendLine(line);
            _bufferedLines++;

            if (_bufferedLines >= FlushThreshold)
                Flush();
        }

        /// <summary>根据 Player 身份返回角色标签：owner/server/spectator/host-spectator。</summary>
        public static string RoleOf(Player p)
        {
            if (p == null) return "?";
            if (p.IsOwner) return "owner";
            if (p.IsServerStarted && !p.IsClientStarted) return "server";
            if (p.IsClientStarted && !p.IsServerStarted) return "spectator";
            if (p.IsServerStarted && p.IsClientStarted) return "host-spectator";
            return "?";
        }

        /// <summary>把缓冲区内容一次性写入文件并清空。由 SkillDiagFlushDriver 每帧调用，也用于阈值触发。</summary>
        public static void Flush()
        {
            if (_bufferedLines == 0)
                return;

            try
            {
                System.IO.File.AppendAllText(_path, _buffer.ToString());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SkillDiag] flush failed: {e.Message}");
                _enabled = false;
            }

            _buffer.Clear();
            _bufferedLines = 0;
        }
    }
}
