using EnhancedUI.EnhancedScroller;
using UnityEngine;
using UnityEngine.UI;

namespace MemoFramework.Debugger
{
    public class RefInfoView : EnhancedScrollerCellView
    {
        [SerializeField] private Text _typeText;
        [SerializeField] private Text _unusedText;
        [SerializeField] private Text _usingText;
        [SerializeField] private Text _acquireText;
        [SerializeField] private Text _releaseText;
        [SerializeField] private Text _addText;
        [SerializeField] private Text _removeText;

        public void SetData(ReferencePoolInfo info)
        {
            _typeText.text = info.Type.Name;
            _unusedText.text = "未使用引用总数："+info.UnusedReferenceCount.ToString();
            _usingText.text ="正在使用引用总数："+ info.UsingReferenceCount.ToString();
            _acquireText.text ="申请引用总数："+ info.AcquireReferenceCount.ToString();
            _releaseText.text = "释放引用总数："+info.ReleaseReferenceCount.ToString();
            _addText.text = "添加引用总数："+info.AddReferenceCount.ToString();
            _removeText.text ="移除引用总数："+ info.RemoveReferenceCount.ToString();
        }
    }
}