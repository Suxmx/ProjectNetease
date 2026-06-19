using UnityEngine;
using UnityEngine.UI;

namespace MemoFramework.Debugger
{
    public class TitleTabEntry : TabEntryBase
    {
        [SerializeField] private Text _entryText;
        [SerializeField] private Image _maskImage;
        public override void OnRegister()
        {
            _entryText.text = TabInfo.Title;
            _maskImage.enabled = false;
        }

        public override void OnUnregister()
        {
            
        }

        public override void OnSelect()
        {
            _maskImage.enabled = true;
        }

        public override void OnDeselect()
        {
            _maskImage.enabled = false;
        }

        public override void OnUpdate()
        {
            
        }
    }
}