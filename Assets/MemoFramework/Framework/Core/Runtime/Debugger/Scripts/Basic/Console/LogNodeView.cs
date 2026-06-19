using System;
using EnhancedUI.EnhancedScroller;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MemoFramework.Debugger
{
    public class LogNodeView : EnhancedScrollerCellView,IPointerClickHandler
    {
        [SerializeField] private Image _bg;
        [SerializeField] private Image _blob;
        [SerializeField] private Text _logText;
        private static Color _chosenColor = new Color(0.172549f, 0.3647059f, 0.5294118f);
        private ConsoleTab _owner;
        private Color _originalColor;

        private void Awake()
        {
            _originalColor = _bg.color;
        }

        public void SetData(string logStr,Color blobColor,ConsoleTab owner,bool choose)
        {
            _owner = owner;
            _blob.color = blobColor;
            _logText.text = logStr;
            _bg.color = choose ? _chosenColor : _originalColor;
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            _owner.ChooseLogNode(dataIndex);
        }
    }
}