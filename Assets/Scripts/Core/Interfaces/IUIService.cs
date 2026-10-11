namespace PuzzleOnline.Core
{
    /// <summary>
    /// Giao diện dịch vụ giao diện người dùng (UI Service Contract).
    /// </summary>
    public interface IUIService
    {
        bool IsModalOrChatOpen { get; }
        void ShowToast(string message);
        void AddChatMessage(string message);
    }
}
