namespace BiliLearn.CSharp.Plugin.Models;

public enum ProgressLevel
{
    /// <summary>仅记录日志（工作台可见），不推送聊天窗口</summary>
    LogOnly,
    /// <summary>记录日志并推送到聊天窗口（用户可见的重要信息）</summary>
    LogAndPush
}
