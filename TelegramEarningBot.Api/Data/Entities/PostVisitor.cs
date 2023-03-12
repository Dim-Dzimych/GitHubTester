namespace TelegramEarningBot.Api.Data.Entities;

public class PostVisitor
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public SendingPost? SendingPost { get; set; }
    public long? SendingPostId { get; set; }
}