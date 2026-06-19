using UnityEngine;

namespace MemoFramework.Extension
{
    public static class InputData
    {
        private static InputEvent m_CurEvents;
        private static InputEvent m_StartEvents;
        private static InputEvent m_EndEvents;

        private static UIInputEvent _curUIEvents;
        private static UIInputEvent _uiStartEvents;
        private static UIInputEvent _uiEndEvents;
        public static Vector2 MoveInput;
        public static Vector2 RawInput;
        public static Vector2 MouseScreenPosition;

        public static void Clear()
        {
            m_CurEvents = InputEvent.None;
            m_StartEvents = InputEvent.None;
            m_EndEvents = InputEvent.None;
            _curUIEvents = UIInputEvent.None;
            _uiStartEvents = UIInputEvent.None;
            _uiEndEvents = UIInputEvent.None;
            MoveInput = Vector2.zero;
            RawInput = Vector2.zero;
            MouseScreenPosition = Vector2.zero;
        }

        #region NormalInputEvent

        public static bool HasEvent(InputEvent e)
        {
            if (e == InputEvent.Move) return MoveInput != Vector2.zero;
            return (m_CurEvents & e) != 0;
        }

        public static bool HasEventStart(InputEvent e)
        {
            return (m_StartEvents & e) != 0;
        }

        public static bool HasEventEnd(InputEvent e)
        {
            return (m_EndEvents & e) != 0;
        }

        public static void AddEvent(InputEvent e)
        {
            m_CurEvents |= e;
        }

        public static void AddEventStart(InputEvent e)
        {
            m_StartEvents |= e;
        }

        public static void AddEventEnd(InputEvent e)
        {
            m_EndEvents |= e;
        }

        #endregion

        #region UIInputEvents

        public static bool HasEvent(UIInputEvent e)
        {
            return (_curUIEvents & e) != 0;
        }

        public static bool HasEventStart(UIInputEvent e)
        {
            return (_uiStartEvents & e) != 0;
        }

        public static void AddEvent(UIInputEvent e)
        {
            _curUIEvents |= e;
        }

        public static void AddEventStart(UIInputEvent e)
        {
            _uiStartEvents |= e;
        }

        public static void AddEventEnd(UIInputEvent e)
        {
            _uiEndEvents |= e;
        }

        public static bool HasEventEnd(UIInputEvent e)
        {
            return (_uiEndEvents & e) != 0;
        }

        #endregion
    }
}