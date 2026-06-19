using System.Collections.Generic;
using System.Linq;
using EnhancedUI.EnhancedScroller;
using UnityEngine;

namespace MemoFramework.Debugger
{
    public class RefPoolTab : TabBase, IEnhancedScrollerDelegate
    {
        [SerializeField] private EnhancedScroller _scroller;
        [SerializeField] private RefInfoView _cellViewPrefab;
        private List<ReferencePoolInfo> _refInfo = new List<ReferencePoolInfo>();
        private float _timer = 0;

        public override void OnRegister()
        {
            _scroller.Delegate = this;
        }

        public override void OnUnregister()
        {
            _refInfo.Clear();
        }

        public override void OnSelect()
        {
            UpdateData();
            _scroller.ReloadData(1);
        }

        public override void OnDeselect()
        {
            _refInfo.Clear();
        }

        public override void OnUpdate()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer > 1)
            {
                _timer = 0;
                UpdateData();
                _scroller.ReloadData(_scroller.NormalizedScrollPosition);
            }
        }

        private void UpdateData()
        {
            _refInfo = MFRefPool.GetAllReferencePoolInfos().ToList();
        }

        public int GetNumberOfCells(EnhancedScroller scroller)
        {
            return _refInfo.Count;
        }

        public float GetCellViewSize(EnhancedScroller scroller, int dataIndex)
        {
            return 80;
        }

        public EnhancedScrollerCellView GetCellView(EnhancedScroller scroller, int dataIndex, int cellIndex)
        {
            RefInfoView cellView = scroller.GetCellView(_cellViewPrefab) as RefInfoView;
            cellView.SetData(_refInfo[dataIndex]);
            return cellView;
        }
    }
}