namespace TelegramEarningBot.Api.Data.Entities;

/// <summary>
/// Пост для рассылки
/// </summary>
public class SendingPost
{
    public long Id { get; set; }
    public string? Link { get; set; }
    public string? Message { get; set; }
    public List<PostVisitor>? PostVisitors { get; set; }
    
    /// <summary>
    /// todo: пока забьем на это не будем записывать контент
    /// </summary>
    /// public byte[] Content { get; set; }
}