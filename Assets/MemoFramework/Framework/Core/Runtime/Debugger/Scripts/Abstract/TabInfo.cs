namespace MemoFramework.Debugger
{
    public class TabInfo
    {
        public string Title { get; set; }
        public TabBase Tab { get; set; }
        public TabEntryBase TabEntry { get; set; }
        public bool Selected { get; set; }
        public object Args { get; set; }
    }
}