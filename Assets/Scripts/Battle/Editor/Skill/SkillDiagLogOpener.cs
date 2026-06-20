#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Battle.Editor
{
    /// <summary>
    /// 打开技能诊断日志所在文件夹的编辑器菜单。
    /// 日志文件由 <see cref="SkillDiagLogger"/> 在每次 Play 时创建于 <see cref="Application.persistentDataPath"/>。
    /// </summary>
    public static class SkillDiagLogOpener
    {
        /// <summary>在系统文件管理器中打开 persistentDataPath 文件夹。</summary>
        [MenuItem("Tools/Battle/Open SkillDiag Log Folder")]
        public static void Open()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
    }
}
#endif
