using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 诊断日志 flush 驱动。运行时自动创建，每帧末尾调用 <see cref="SkillDiagLogger.Flush"/>，
    /// 把缓冲区累积的日志一次性写入文件，避免 tick 内同步 IO 卡顿。
    /// </summary>
    public class SkillDiagFlushDriver : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            GameObject go = new GameObject(nameof(SkillDiagFlushDriver));
            go.AddComponent<SkillDiagFlushDriver>();
            Object.DontDestroyOnLoad(go);
        }

        private void Update()
        {
            SkillDiagLogger.Flush();
        }
    }
}
