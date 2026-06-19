using UnityEngine;
using UnityEngine.UI;

namespace MemoFramework
{
    public class DebuggerPanel : MonoBehaviour
    {
        public RectTransform TabEntriesRoot;
        public RectTransform TabsRoot;
        public Text RightHeaderTitle;
        public Button CloseBtn;
        public Button PinBtn;
        public DebuggerComponent DebuggerComponent { get; set; }
        
        
        
    }
}