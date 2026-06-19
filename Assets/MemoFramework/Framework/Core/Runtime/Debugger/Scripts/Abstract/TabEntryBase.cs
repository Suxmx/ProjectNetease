using UnityEngine;

namespace MemoFramework.Debugger
{
    public abstract class TabEntryBase : MonoBehaviour
    {
        public TabInfo TabInfo { get; set; }
        public abstract void OnRegister();
        public abstract void OnUnregister();
        public abstract void OnSelect();
        public abstract void OnDeselect();
        public abstract void OnUpdate();
    }
}