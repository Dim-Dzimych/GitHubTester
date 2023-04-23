using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelegramEarningBot.Api.Data;
using TelegramEarningBot.Api.Data.Entities;

namespace TelegramEarningBot.Api.Controllers;

[ApiController]
public class BotController : ControllerBase
{
    private readonly EarningBotContext _context;

    public BotController(EarningBotContext context)
    {
        _context = context;
    }

    [HttpGet("c")]
    public async Task<IActionResult> ValidateRedirect(long id, long userId)
    {
        var sendingPost = _context.SendingPosts.Include(x => x.PostVisitors).FirstOrDefault(x => x.Id == id);

        if (sendingPost == null)
        {
            return BadRequest();
        }
        
        sendingPost.PostVisitors?.Add(new PostVisitor
        {
            SendingPostId = id,
            UserId = userId
        });
        
        await _context.SaveChangesAsync();

        //await Task.Delay(-1);

        return Redirect(sendingPost.Link!);
       
    }

    [HttpGet("stat")]
    public IActionResult LinkList()
    {
        var visits = _context.PostVisitors
            .Include(x => x.SendingPost)
            .GroupBy(x => x.SendingPostId)
            .Select(x => new
            {
                postId = x.Key, 
                count = x.Count(),
                pv = _context.PostVisitors.Include(pvi => pvi.SendingPost)
                    .FirstOrDefault(p => p.SendingPostId == x.Key)
            })
            .ToList();

        return Ok(visits);
    }
}
