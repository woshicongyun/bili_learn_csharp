using System;
using BiliLearn.CSharp.Plugin.Services;

namespace BiliLearn.CSharp.Plugin.Models;

public class PendingConfirmation
{
    public string Bvid { get; set; } = "";
    public KnowledgeEntry OldEntry { get; set; } = null!;
    public string UserQuery { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
