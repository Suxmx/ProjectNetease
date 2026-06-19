#if UNITY_EDITOR
using Hoshino;
using UnityEditor;
using UnityEngine;

namespace Battle.Editor
{
    /// <summary>
    /// 编辑器侧 SkillDraw 驱动。在非 playmode 下通过 EditorApplication.update
    /// 推动 SkillDraw.Tick()，使命中判定淡出绘制在技能编辑器预览时可见。
    /// </summary>
    [InitializeOnLoad]
    public static class SkillDrawEditorDriver
    {
        static SkillDrawEditorDriver()
        {
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (Application.isPlaying)
                return;

            SkillDraw.Tick();
        }
    }
}
#endif
