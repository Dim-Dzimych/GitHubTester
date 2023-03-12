namespace TelegramEarningBot.Entities;

public class Task
{
    public long Id { get; set; }
    public string Link { get; set; }
    public string Name { get; set; }
    public bool IsVisited { get; set; }
    
    /// <summary>
    /// Code guid
    /// </summary>
    public string Code { get; set; }
}
