using System.Collections.Generic;

namespace TelegramEarningBot.Entities;

/// <summary>
/// Пост для рассылки
/// </summary>
public class SendingPost
{
    public long Id { get; set; }
    public string Link { get; set; }
    public string Message { get; set; }
    public List<PostVisitor> PostVisitors { get; set; }
}