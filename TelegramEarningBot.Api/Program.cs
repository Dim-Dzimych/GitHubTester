using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TelegramEarningBot.Api;
using TelegramEarningBot.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<EarningBotContext>(opt => opt.UseSqlite(builder.Configuration.GetConnectionString("Db")));
builder.AddLogging();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
