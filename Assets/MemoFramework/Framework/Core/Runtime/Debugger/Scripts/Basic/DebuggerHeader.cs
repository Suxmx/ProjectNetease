using UnityEngine;
using UnityEngine.EventSystems;

namespace MemoFramework.Debugger
{
    public class DebuggerHeader : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [SerializeField] private RectTransform _panel;
        private Vector2 _pointerDownOffset;
        private DebuggerComponent _debuggerComponent;

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerDownOffset = (Vector2)_panel.position - eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            _debuggerComponent??=MemoFrameworkEntry.GetComponent<DebuggerComponent>();
            if (_debuggerComponent is null) return;
            if (!_debuggerComponent.AllowDrag) return;
            //目标位置
            var targetPos = eventData.position + _pointerDownOffset;
            if (!_debuggerComponent.DragProtect)
            {
                _panel.position = targetPos;
                return;
            }
            //计算是否拖出屏幕
            Vector3[] headerCorners = new Vector3[4];
            Vector3[] panelCorners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(headerCorners);
            _panel.GetWorldCorners(panelCorners);
            float headerTop = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, headerCorners[1]).y;
            float headerBottom =
                RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, headerCorners[0]).y;
            float headerHeight = headerTop - headerBottom;
            
            float panelLeft = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, panelCorners[0]).x;
            float panelRight = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, panelCorners[2]).x;
            float panelWidth = panelRight - panelLeft;
            if (targetPos.y > Screen.height)
            {
                targetPos.y = Screen.height;
                _pointerDownOffset.y = targetPos.y - eventData.position.y;
            }
            else if (targetPos.y - headerHeight < 0)
            {
                targetPos.y = headerHeight;
                _pointerDownOffset.y = targetPos.y - eventData.position.y;
            }

            if (targetPos.x < 0)
            {
                targetPos.x = 0;
                _pointerDownOffset.x = targetPos.x - eventData.position.x;
            }

            if (targetPos.x + panelWidth > Screen.width)
            {
                targetPos.x = Screen.width - panelWidth;
                _pointerDownOffset.x = targetPos.x - eventData.position.x;
            }
            
            _panel.position = targetPos;
        }
    }
}