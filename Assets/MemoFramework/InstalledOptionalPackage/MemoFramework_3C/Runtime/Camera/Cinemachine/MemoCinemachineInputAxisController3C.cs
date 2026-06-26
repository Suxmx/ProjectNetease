using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace MemoFramework.ThreeC.Cinemachine
{
    /// <summary>
    /// 用 3C 共享 Look 输入驱动 Cinemachine v3 的输入轴组件。
    /// </summary>
    [DisallowMultipleComponent]
    public class MemoCinemachineInputAxisController3C : MonoBehaviour
    {
        private readonly List<IInputAxisOwner> _axisOwners = new List<IInputAxisOwner>();
        private readonly List<IInputAxisOwner.AxisDescriptor> _axes = new List<IInputAxisOwner.AxisDescriptor>();

        [SerializeField] private bool _scanChildren = true;
        [SerializeField] private Vector2 _axisScale = Vector2.one;
        [SerializeField] private bool _invertY;

        /// <summary>
        /// 组件启用时扫描可驱动的 Cinemachine 输入轴。
        /// </summary>
        protected virtual void OnEnable()
        {
            SynchronizeAxes();
        }

        /// <summary>
        /// Unity 每帧更新入口。
        /// </summary>
        protected virtual void Update()
        {
            if (_axes.Count == 0)
                SynchronizeAxes();

            // 读取共享 Look 输入并应用局部缩放。
            Vector2 lookInput = Vector2.Scale(MemoThreeCInputData.LookInput, _axisScale);
            if (_invertY)
                lookInput.y = -lookInput.y;

            // 只驱动 Look X/Y 轴，避免把鼠标 Y 写入 OrbitalFollow 的缩放轴。
            for (int i = 0; i < _axes.Count; i++)
            {
                IInputAxisOwner.AxisDescriptor axisDescriptor = _axes[i];
                if (!ShouldDriveAxis(axisDescriptor))
                    continue;

                ref InputAxis axis = ref axisDescriptor.DrivenAxis();
                float delta = ResolveAxisDelta(axisDescriptor.Hint, lookInput);
                axis.Value = axis.ClampValue(axis.Value + delta);
            }
        }

        /// <summary>
        /// 重新扫描当前物体上的 Cinemachine 输入轴组件。
        /// </summary>
        public void SynchronizeAxes()
        {
            _axisOwners.Clear();
            _axes.Clear();

            if (_scanChildren)
                GetComponentsInChildren(_axisOwners);
            else
                GetComponents(_axisOwners);

            for (int i = 0; i < _axisOwners.Count; i++)
                _axisOwners[i].GetInputAxes(_axes);
        }

        /// <summary>
        /// 按 Cinemachine 轴提示选择 X 或 Y 输入。
        /// </summary>
        /// <param name="hint">Cinemachine 输入轴提示。</param>
        /// <param name="lookInput">当前 Look 输入。</param>
        /// <returns>该轴本帧增量。</returns>
        private float ResolveAxisDelta(IInputAxisOwner.AxisDescriptor.Hints hint, Vector2 lookInput)
        {
            return hint == IInputAxisOwner.AxisDescriptor.Hints.Y ? lookInput.y : lookInput.x;
        }

        /// <summary>
        /// 判断 Cinemachine 暴露的轴是否应该由 3C Look 输入驱动。
        /// </summary>
        /// <param name="axisDescriptor">Cinemachine 输入轴描述。</param>
        /// <returns>是否驱动该轴。</returns>
        private static bool ShouldDriveAxis(IInputAxisOwner.AxisDescriptor axisDescriptor)
        {
            if (axisDescriptor.Hint != IInputAxisOwner.AxisDescriptor.Hints.X &&
                axisDescriptor.Hint != IInputAxisOwner.AxisDescriptor.Hints.Y)
                return false;

            string axisName = axisDescriptor.Name;
            return !string.IsNullOrEmpty(axisName) &&
                   axisName.Contains("Look") &&
                   !axisName.Contains("Scale") &&
                   !axisName.Contains("Radial");
        }
    }
}
