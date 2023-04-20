using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using ParseMode = Telegram.Bot.Types.Enums.ParseMode;
using ReceiverOptions = Telegram.Bot.Extensions.Polling.ReceiverOptions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using TelegramEarningBot.Entities;
using Task = System.Threading.Tasks.Task;

namespace TelegramEarningBot
{
    public static class Program
    {
        private static string _adminUsername;
        private static string _botToken;
        private static string _dbConnectionString;
        private static IConfiguration _configuration;

        private static readonly CancellationToken CancellationToken = new CancellationToken();

        private static readonly ITelegramBotClient Bot =
            new TelegramBotClient("5779175744:AAEyZvZTfhEwvEjlRfS7IgUiClbyXTRmqDc");

        private static int _subscribersInDay;
        private static int _savedDay = DateTime.Today.Day;
        private static bool _isTask;
        private static string _taskLink;
        private static bool _isMailReceived;
        private static bool _isAddTask;
        private static string _taskToDelete;
        private static bool _isGuestOutput;
        private static bool _isSQLSendPosted;
        private static string _callBackData;

        public static async Task Main(string[] args)
        {
            FillFromConfig();

            Console.WriteLine("Запущен бот " + (await Bot.GetMeAsync()).FirstName);
            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            var receiverOptions = new ReceiverOptions();

            Bot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken
            );
            Console.ReadLine();
            await Task.Delay(-1);
        }

        //TODO
        // сделать рассылку как в speakBot
        //  добавить в расслыку инлайн кнопки
        // добавить в рассылку GIF

        private static void FillFromConfig()
        {
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            _dbConnectionString = _configuration.GetConnectionString("Db");
            _botToken = _configuration["BotToken"];
            _adminUsername = _configuration["AdminUsername"];
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
            CancellationToken cancellationToken)
        {
            await CheckUpdatesForPrivateGroup(botClient);
            AdminCheckSubscriberForDay();
            GuestOutPutMessage(update, botClient);
            //RefferalUpdater(update);
            try
            {
                if (update.Type != UpdateType.CallbackQuery)
                {
                    //first start
                    if (update.Message?.Text == null)
                    {
                        Console.WriteLine("Update was null");
                    }
                    else if (update.Message.Text.Contains("/start"))
                    {
                        if (SQLDbOutPut("CashById")
                                .ToList().Find(id => id[1] == update.Message.Chat.Id.ToString()) ==
                            null) // check if this person in bot early if not add his info
                        {

                            SQLDbInput("CashById", update.Message.Chat.Id, 0); // first enter in bot
                            try
                            {
                                var friendId = update.Message.Text.Split()[1]; // if null go out
                                //!SubsReferallCash.ContainsKey(Convert.ToInt64(friendId)
                                if (SQLDbOutPut("RefferalCashById").FirstOrDefault(id => id[0] == friendId) ==
                                    null) // first refferal
                                {
                                    SQLDbInput("RefferalCashById", friendId, 1); // add first refferal adder
                                }
                                else // second and etc refferal
                                {
                                    //var updateValue = SubsReferallCash[Convert.ToInt64(friendId)] += 1;
                                    var updateValue = SQLDbOutPut("RefferalCashById").Where(id => id[0] == friendId)
                                        .Select(index => index[1]).Single(number => true);
                                    SQLDbDUpdateCash("RefferalCashById", (friendId), Convert.ToInt32(updateValue) + 1);
                                }

                                await botClient.SendTextMessageAsync(friendId,
                                    "🔗 По Вашей ссылке перешел новый гость.\n💸 Вам полагается +20 руб",
                                    parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }

                            _subscribersInDay++;
                            //SubsCashById.Add(update.Message.Chat.Id,0);
                            //SubscribersInfo.Add($"{"@"+update.Message.Chat.Username + " "+ update.Message.Chat.FirstName}");
                        }

                        if (_savedDay != DateTime.Now.Day)
                        {
                            _savedDay = DateTime.Now.Day;
                            _subscribersInDay = 0;
                        } // check new date

                        if (update.Message.From?.Username == _adminUsername)
                        {
                            await HandleAdminUpdateAsync(botClient, update, cancellationToken);
                            return;
                        }

                        await HandleGuestUpdateAsync(botClient, update, cancellationToken);
                        return;
                    }

                    //admin
                    if (update.Message?.From?.Username == _adminUsername)
                    {
                        await HandleAdminUpdateAsync(botClient, update, cancellationToken);
                        return;
                    }

                    //guest
                    await HandleGuestUpdateAsync(botClient, update, cancellationToken);
                    return;
                }

                if (update.Type == UpdateType.CallbackQuery)
                {
                    await HandleTasksCallBackQuery(botClient, update.CallbackQuery);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static async Task HandleAdminUpdateAsync(ITelegramBotClient botClient, Update update,
            CancellationToken cancellationToken)
        {
            var message = update.Message;
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] {"➕ Добавить задание"},
                new KeyboardButton[] {"💵 Список заданий", "📤 Рассылка"},
                new KeyboardButton[] {"Список Пользователей"},
            })
            {
                ResizeKeyboard = true
            };

            if (message!.From!.Username == _adminUsername)
            {
                switch (message.Text)
                {
                    case "/start":
                        _isMailReceived = false;
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Господин, Добро пожаловать",
                            replyMarkup: keyboard, cancellationToken: cancellationToken);
                        break;
                    case "➕ Добавить задание":
                        _isMailReceived = false;
                        _isAddTask = true;
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Напишите ссылку для группы",
                            parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                        break;
                    case "💵 Список заданий":
                        _isMailReceived = false;
                        await AdminCheckTasks(update, botClient);
                        break;
                    case "Список Пользователей":
                        _isMailReceived = false;
                        await botClient.SendTextMessageAsync(message.Chat.Id,
                            $"Пользователей всего - {SQLDbOutPut("CashById").Count}\n\nЗа сегодня - {_subscribersInDay}",
                            parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                        break;
                    case "📤 Рассылка":
                        var index = 1;
                        var buttons = new List<InlineKeyboardButton[]>();
                       
                        foreach (var post in SQLDbOutPut("SendingPosts"))
                        {
                           //botClient.SendTextMessageAsync(update.Message.Chat.Id,$"🔗Номер - {index} Ссылка на {post[1].Split("https://")[1]} c постом \"{post[2][0..10]}...\" \n🧍‍Количество переходов - {SQLDbOutPut("PostVisitors").Where(id => id[2] == post[0]).Count()}\n\n");
                           buttons.Add(new[] {InlineKeyboardButton.WithCallbackData($"{index}--{post[1]}--🧍{SQLDbOutPut("PostVisitors").Where(id => id[2] == post[0]).Count()}")});
                            
                           index++;
                        }
                        var tasksBoard = new InlineKeyboardMarkup(buttons);
                        
                        if (index > 1)
                        {
                            botClient.SendTextMessageAsync(update.Message.Chat.Id, "Расслыки", replyMarkup: tasksBoard);
                        }
                        else
                        {
                            botClient.SendTextMessageAsync(update.Message.Chat.Id, "Нет рассылок");
                        }
                        
                        botClient.SendTextMessageAsync(update.Message.Chat.Id, "👀 Отправь фото с текстом или отдельно текст");
                        _isMailReceived = true;
                        break;
                }

                AdminMailing(update, botClient);

                if (_isAddTask)
                {
                    AdminTasksAdder(update);
                }

            }
        }

        private static async Task HandleGuestUpdateAsync(ITelegramBotClient botClient, Update update,
            CancellationToken cancellationToken)
        {
            switch (update.Message!.Text)
            {
                case "/start":
                {
                    var keyboard = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] {"💵 Поехали зарабатывать"},
                        new KeyboardButton[] {"🤩 Баланс", "❤️ Вывести"},
                        new KeyboardButton[] {"👫 Партнеры", "😱 Помогите"},
                    })
                    {
                        ResizeKeyboard = true
                    };

                    await botClient.SendTextMessageAsync(update.Message.Chat.Id,
                        "Добро пожаловать! Если ты оказался тут значит пришло время заработать!!",
                        replyMarkup: keyboard, cancellationToken: cancellationToken);
                    break;
                }
                case "💵 Поехали зарабатывать":
                {
                    if (SQLDbOutPut("Tasks").Count <= 0)
                        await botClient.SendTextMessageAsync(update.Message.Chat.Id,
                            "Задания скоро будут, ищем более выгодные ...", parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken);

                    var replyMarkUp = await TaskKeyBoardUpdater(botClient, update.Message.Chat.Id, cancellationToken);

                    if (replyMarkUp != null)
                        await botClient.SendTextMessageAsync(update.Message.Chat.Id, "Все Задания",
                            replyMarkup: replyMarkUp, cancellationToken: cancellationToken);
                    break;
                }
                case "🤩 Баланс":
                {
                    var nowTask = await TaskNowDesk(botClient, update.Message!.Chat.Id, CancellationToken);
                    var refferalCounter = 0;
                    
                    if (SQLDbOutPut("Tasks").Count != 0 && nowTask.Count == 0)
                        SQLDbDUpdateCash("CashById", update.Message.Chat.Id, SQLDbOutPut("Tasks").Count * 14);
                    ; //price for one task = 14 r.r
                    if (SQLDbOutPut("Tasks").Count != 0 && nowTask.Count != 0)
                        SQLDbDUpdateCash("CashById", update.Message.Chat.Id, 0);
                    
                    if (SQLDbOutPut("RefferalCashByID")
                            .FirstOrDefault(id => id[1] == update.Message.Chat.Id.ToString()) != null)
                    {
                        var taskMoney = SQLDbOutPut("CashById").FirstOrDefault(id => id[1] == update.Message.Chat.Id.ToString());
                        var refferalMoney = SQLDbOutPut("RefferalCashById")
                            .FirstOrDefault(id => id[1] == update.Message.Chat.Id.ToString());
                        refferalCounter = Convert.ToInt32(refferalMoney[2]);
                        var result = Convert.ToInt32(taskMoney[2]) + (refferalCounter * 20);

                        SQLDbDUpdateCash("CashById", update.Message.Chat.Id, result);
                    }
                    // if (SubsReferallCash.ContainsKey(update.Message.Chat.Id))
                    // {
                    //     SubsCashById[update.Message.Chat.Id] += SubsReferallCash[update.Message.Chat.Id] * 20;
                    //     refferalCounter = SubsReferallCash[update.Message.Chat.Id];
                    // }
                    
                        
                    var total = SQLDbOutPut("CashById").Where(id => id[1] == update.Message.Chat.Id.ToString())
                        .Single(count => Convert.ToInt16(count[2]) >= 0)[2];
                    
                    await botClient.SendTextMessageAsync(update.Message.Chat.Id,
                        $"💵 Баланс - {total}\n🔥 Для пополнения счета выполните все задания\n⛓ По Реферальной ссылке перешло {refferalCounter}",
                        parseMode: ParseMode.Html, cancellationToken: cancellationToken);

                    break;
                }
                case "❤️ Вывести":
                {
                    var cashKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        InlineKeyboardButton.WithCallbackData("QIWI", "QIWI"),
                        InlineKeyboardButton.WithCallbackData("Сбербанк", "Sber"),
                        InlineKeyboardButton.WithCallbackData("YooMoney", "YooMoney"),
                    });
                    await botClient.SendTextMessageAsync(update.Message.Chat.Id,
                        "🤔 Выберите систему для вывода средств.\n🌏 Заработанная сумма равна балансу на день отправления реквизитов(следующее действие)",
                        replyMarkup: cashKeyboard, cancellationToken: cancellationToken);
                    break;
                }
                case "👫 Партнеры":
                {
                    var linkBot = $"https://t.me/MoonCoinLove_bot?start={update.Message.Chat.Id}";
                    await botClient.SendTextMessageAsync(update.Message.Chat.Id,
                        "💪 Отправьте другу Вашу реферальную ссылку и по ней Вам начислится + 20 р." +
                        $"\n\n🔗 Ссылка - {linkBot}",
                        parseMode: ParseMode.Html, cancellationToken: cancellationToken);

                    break;
                }
                case "😱 Помогите":
                {
                    const string textForSend = "🤖 Бот предназначен для заработка путем:" +
                                               "\n✅ Подписание на группы" +
                                               "\n✅ Просмотра контента (не менее 10 постов)" +
                                               "\n\n👀 Задания обновляются каждый день, если выполнили все задания проверяйте баланс" +
                                               "\n🔗 Реферальные ссылки помогают заработать больше, помогая нам - зарабатываете больше, все честно (клавиша - 👫 Партнеры)" +
                                               "\n💳 Вывод производиться самим админом в конце месяца на отправленные Вами реквизиты" +
                                               "\n💸 Также имеются дополнительные задания которые будут приходить путем рассылки(подписание на бота,участие в конкурсе).Каждое задание + 20 р." +
                                               "\n🧔 Есть вопросы по Боту - @Meowkov";
                    await botClient.SendTextMessageAsync(update.Message.Chat.Id, $"{textForSend}",
                        parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                    break;
                }
            }
            // GuestOutPutMessage(update,botClient);
        }

        private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception,
            CancellationToken cancellationToken)
        {
            // Некоторые действия
            Console.WriteLine(JsonConvert.SerializeObject(exception));
            return Task.CompletedTask;
        }

        static async Task HandleTasksCallBackQuery(ITelegramBotClient botClient, CallbackQuery callback)
        {
            // 1 - admin
            // 2 - Guest
            if (callback.From.Username == _adminUsername)
            {
                if (callback.Data != "Нет" && callback.Data != "Да")
                {
                    _callBackData = callback.Data;
                }
                var isDeleteOnly = false;
                switch (callback.Data)
                {
                    case "Да":
                    {
                        if (_callBackData != null)
                        {
                            _callBackData = _callBackData.Split("--")[1];
                            var postID = SQLDbOutPut("SendingPosts").Where(id => id[1] == _callBackData).ToList()[0][0];
                            SQLDbDelete("PostVisitors",postID);
                            SQLDbDelete("SendingPosts",postID);
                            _isMailReceived = false;
                            _callBackData = null;
                            isDeleteOnly = true;
                            await botClient.SendTextMessageAsync(callback.Message!.Chat.Id, " ❌ Рассылка удалена");
                            break;
                        }
                        
                        isDeleteOnly = true;
                        SQLDbDelete("Tasks", _taskToDelete);
                        await botClient.SendTextMessageAsync(callback.Message!.Chat.Id, "🗑 Удален");
                        break;
                    }
                    case "Нет":
                        await botClient.DeleteMessageAsync(callback.Message.Chat.Id, callback.Message.MessageId);
                        isDeleteOnly = true;
                        break;

                }

                // TODO
                // if (callback.Data == "ReferralAdd")
                // {
                //     
                // }else if(callback.Data == "ReferralRemove"){
                //}

                if (isDeleteOnly == false)
                {
                    var keyboardSolution = new InlineKeyboardMarkup(new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Да"),
                        InlineKeyboardButton.WithCallbackData("Нет"),
                    });
                    await botClient.SendTextMessageAsync(callback.Message!.Chat.Id, $"Удаляете № - {callback.Data![0]}",
                        replyMarkup: keyboardSolution);
                    _taskToDelete = callback.Data.Split('-')[1];
                }
            }
            else
            {
                var nowTask = await TaskNowDesk(botClient, callback.Message!.Chat.Id, CancellationToken);
                if (nowTask.Count != 0)
                {
                    await botClient.AnswerCallbackQueryAsync(callback.Id,
                        "Обновите задания или выполните их, для начисления средств ", true);
                    var markUp = (InlineKeyboardMarkup) await TaskKeyBoardUpdater(botClient, callback.Message!.Chat.Id,
                        CancellationToken);
                    //var markUp =  await TaskKeyBoardUpdater(botClient, callback.Message.Chat.Id, mycancellationToken);

                    if (markUp == null)
                    {
                        await botClient.DeleteMessageAsync(callback.Message.Chat.Id, callback.Message.MessageId);
                    }

                    await botClient.EditMessageReplyMarkupAsync(callback.Message.Chat.Id, callback.Message.MessageId,
                        markUp);
                }

                if (nowTask.Count == 0)
                {
                    // if(callback.Data == )
                    //var markUp = (InlineKeyboardMarkup) await TaskKeyBoardUpdater(botClient, callback.Message!.Chat.Id, CancellationToken);
                    //var markUp =  await TaskKeyBoardUpdater(botClient, callback.Message.Chat.Id, mycancellationToken);

                    // if (markUp == null)
                    // {
                    await botClient.DeleteMessageAsync(callback.Message.Chat.Id, callback.Message.MessageId);
                    //}
                    //await botClient.EditMessageReplyMarkupAsync(callback.Message.Chat.Id, callback.Message.MessageId,markUp);
                    switch (callback.Data)
                    {
                        case "Sber":
                        {
                            await botClient.SendTextMessageAsync(callback.Message!.Chat.Id,
                                "💳 Введите номер карты сбербанк и ваш UserName.\n👱‍ Админ в ближайшее время обработает Ваш запрос и проверит соблюдение условий.\n🤘 Все переводы производятся без коммиссии\n⬇Ваш возможный UserName ⬇️️",
                                parseMode: ParseMode.Html);

                            await botClient.SendTextMessageAsync(callback.Message!.Chat.Id,
                                $"{callback.From.Username}️", parseMode: ParseMode.Html);
                            _isGuestOutput = true;
                            // MoneyOutput.Add(callback.Message.Chat.Id);
                            break;
                        }
                        case "QIWI":
                        {
                            await botClient.SendTextMessageAsync(callback.Message!.Chat.Id,
                                "💳 Введите номер QIWI кошелька и ваш UserName.\n👱‍ Админ в ближайшее время обработает Ваш запрос и проверит соблюдение условий.\n🤘 Все переводы производятся без коммиссии\n⬇Ваш возможный UserName ⬇️",
                                parseMode: ParseMode.Html);

                            await botClient.SendTextMessageAsync(callback.Message!.Chat.Id,
                                $"{callback.From.Username}️", parseMode: ParseMode.Html);
                            _isGuestOutput = true;
                            //MoneyOutput.Add(callback.Message.Chat.Id);
                            break;
                        }
                        case "YooMoney":
                        {
                            await botClient.SendTextMessageAsync(callback.Message!.Chat.Id,
                                "💳 Введите номер счета YooMoney и ваш UserName.\n👱‍ Админ в ближайшее время обработает Ваш запрос и проверит соблюдение условий.\n🤘 Все переводы производятся без коммиссии\n⬇Ваш возможный UserName ⬇️",
                                parseMode: ParseMode.Html);

                            await botClient.SendTextMessageAsync(callback.Message.Chat.Id,
                                $"{callback.From.Username}️", parseMode: ParseMode.Html);
                            _isGuestOutput = true;
                            //MoneyOutput.Add(callback.Message.Chat.Id);
                            break;
                        }
                    }


                    if (callback.Data == "check")
                    {
                        var markUp = (InlineKeyboardMarkup) await TaskKeyBoardUpdater(botClient,
                            callback.Message!.Chat.Id,
                            CancellationToken);
                        await botClient.EditMessageReplyMarkupAsync(callback.Message.Chat.Id,
                            callback.Message.MessageId,
                            markUp);
                    }
                }
            }

        }

        private static async Task<IReplyMarkup> TaskKeyBoardUpdater(ITelegramBotClient botClient, long chatId,
            CancellationToken cancellationToken)
        {
            var nowTaskForSubscriber = await TaskNowDesk(botClient, chatId, cancellationToken);

            var buttons = new List<InlineKeyboardButton[]>();
            var index = nowTaskForSubscriber.Count;
            var counter = 0;
            var taskNumber = 1;

            while (true)
            {
                if (index == 1) // equal 1
                {
                    buttons.Add(new[]
                        {InlineKeyboardButton.WithUrl($"Task{taskNumber}", $"{nowTaskForSubscriber[counter]}")});
                    buttons.Add(new[] {InlineKeyboardButton.WithCallbackData("Забрать деньги", "Check"),});
                    break;
                }

                if (index == 0)
                {
                    await botClient.SendTextMessageAsync(chatId,
                        "👍 Вы выполнили все задания следите за новостями в ленте,так как они постоянно обновляются, а также ожидайте своей выплаты",
                        parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                    return null;
                }

                if (index % 2 != 0) // odd
                {
                    buttons.Add(new[]
                        {InlineKeyboardButton.WithUrl($"Task{taskNumber}", $"{nowTaskForSubscriber[counter]}")});
                    counter++;
                    taskNumber++;
                    index--;
                    //_taskNumber++
                }

                if (index % 2 != 0) continue;
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithUrl($"Task{taskNumber++}", $"{nowTaskForSubscriber[counter++]}"),
                    InlineKeyboardButton.WithUrl($"Task{taskNumber}", $"{nowTaskForSubscriber[counter]}")
                });
                counter++;
                //_taskNumber++;
                taskNumber++;
                index -= 2;

                if (index != 0) continue;

                buttons.Add(new[] {InlineKeyboardButton.WithCallbackData("Забрать деньги", "Check"),});
                break;
            }

            return new InlineKeyboardMarkup(buttons);
        }

        private static async Task<List<string>> TaskNowDesk(ITelegramBotClient botClient, long chatId,
            CancellationToken cancellationToken)
        {
            var nowTaskForSubscriber = new List<string>();
            foreach (var task in SQLDbOutPut("Tasks"))
            {
                var username = task[1].Split("https://t.me/");
                if (username[1].Contains("+")) //for private group
                {
                    try
                    {
                        foreach (var privateGroup in SQLDbOutPut("PrivateGroup"))
                        {
                            if (privateGroup[2] == task[2])
                            {
                                var number = $"-100{privateGroup[1]}";
                                var numbertwo = chatId;
                                if (botClient.GetChatMemberAsync($"-100{privateGroup[1]}", chatId,
                                            cancellationToken: cancellationToken)
                                        .Result.Status != ChatMemberStatus.Member)
                                {
                                    nowTaskForSubscriber.Add(task[1]);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
                else // for open group
                {
                    try // only for check on null result (bot or Private group without right of verification)
                    {
                        if (botClient.GetChatMemberAsync($"@{username[1]}", chatId,
                                    cancellationToken: cancellationToken)
                                .Result.Status != ChatMemberStatus.Member)
                        {
                            nowTaskForSubscriber.Add(task[1]);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }

            }

            return nowTaskForSubscriber;
        }

        private static void AdminTasksAdder(Update update)
        {
            if (update.Message!.Text!.Contains("http"))
            {
                _taskLink = update.Message.Text;
                _isTask = true;
                Bot.SendTextMessageAsync(update.Message.Chat,
                    _taskLink.Contains("+")
                        ? "🔐 Добавляемая группа Закрытая, вставьте сюда название группы"
                        : "👐 Напишите название для добавляемой ссылки");
            }
            else if (_isTask && !update.Message.Text.Contains("название"))
            {
                //Tasks.Add(new []{$"{_taskLink}",$"{update.Message.Text}"});
                SQLDbInput("Tasks", _taskLink, update.Message.Text);
                //SQLDbInput("Tasks","111","update.Message.Text");

                Bot.SendTextMessageAsync(update.Message.Chat, $"🔥 Задание успешно создано");
                _isTask = false;
                _isAddTask = false;
            }
        }

        private static async Task AdminCheckTasks(Update update, ITelegramBotClient botClient)
        {
            if (SQLDbOutPut("Tasks").Count == 0)
            {
                await botClient.SendTextMessageAsync(update.Message!.Chat.Id, "Нет Заданий", parseMode: ParseMode.Html,
                    cancellationToken: CancellationToken);
            }
            else
            {
                string AllTask()
                {
                    var index = 1;
                    var buttons = new List<InlineKeyboardButton[]>();

                    foreach (var task in SQLDbOutPut("Tasks"))
                    {
                        buttons.Add(new[] {InlineKeyboardButton.WithCallbackData($"{index}-{task[2]}")});
                        index++;
                    }
                    // foreach (var task in Tasks)
                    // {
                    //     buttons.Add(new[] {InlineKeyboardButton.WithCallbackData($"{index} - {task[1]}")});
                    //     index++;
                    // }

                    var tasksBoard = new InlineKeyboardMarkup(buttons);

                    botClient.SendTextMessageAsync(update.Message.Chat.Id, "Все Задания", replyMarkup: tasksBoard);
                    return "Можете удалить выбранное задание";
                }

                await botClient.SendTextMessageAsync(update.Message!.Chat.Id, AllTask(), parseMode: ParseMode.Html,
                    cancellationToken: CancellationToken);
            }
        }

        private static async void AdminMailing(Update update, ITelegramBotClient botClient)
        {
            if (!_isMailReceived) return;
            _isSQLSendPosted = true;
            var taskInfo = MailTypeInfo.Empty;

            switch (update.Message!.Type)
            {
                case MessageType.Video: // remember video
                    taskInfo = MailTypeInfo.Video;
                    break;
                case MessageType.Text: // remember and check text
                    if (!update.Message.Text!.Contains("📤 Рассылка"))
                    {
                        taskInfo = MailTypeInfo.Text;
                    }

                    break;
                case MessageType.Photo: // remember photo
                    taskInfo = MailTypeInfo.Photo;
                    break;
            }

            if (taskInfo == MailTypeInfo.Empty) return;

            var count = 0;
            await Task.Run(async () =>
            {
                while (count != SQLDbOutPut("CashById").Count)
                {
                    foreach (var idNumber in SQLDbOutPut("CashById"))
                    {
                        try
                        {
                            switch (taskInfo)
                            {
                                case MailTypeInfo.Video:
                                    var videCaptionToSend = AdminRefferalMailMessage(update, MailTypeInfo.Video);
                                    //await botClient?.SendVideoAsync(Convert.ToInt64(idNumber[0]), new InputFileId(update.Message.Video!.FileId), null,null,null,null,null,videCaptionToSend,parseMode: ParseMode.Html)!;
                                    await botClient?.SendVideoAsync(Convert.ToInt64(idNumber[1]),
                                        update.Message.Video!.FileId, null, null, null, null, videCaptionToSend,
                                        ParseMode.Html)!;
                                    break;
                                case MailTypeInfo.Text:
                                    var textToSend = AdminRefferalMailMessage(update, MailTypeInfo.Text);
                                    await botClient?.SendTextMessageAsync(Convert.ToInt64(idNumber[1]), textToSend,
                                        parseMode: ParseMode.Html);
                                    break;
                                case MailTypeInfo.Photo:
                                    var photoCaptionToSend = AdminRefferalMailMessage(update, MailTypeInfo.Photo);
                                    //await botClient?.SendPhotoAsync(Convert.ToInt64(idNumber[0]),new InputFileId(update.Message!.Photo![0].FileId),null,photoCaptionToSend,parseMode:ParseMode.Html);
                                    await botClient?.SendPhotoAsync(Convert.ToInt64(idNumber[1]),
                                        update.Message!.Photo![0].FileId, photoCaptionToSend,
                                        parseMode: ParseMode.Html);
                                    break;
                            }
                        }
                        catch
                        {
                        }

                        await Task.Delay(35); // 30 person per second
                        count++;
                    }
                }

                _isMailReceived = false;
                botClient?.SendTextMessageAsync(update.Message.Chat.Id, "Рассылка завершена");
            });
            // if (IsMailReceived)
            // {
            //     //AdminOutPutReferralLinks(update,botClient);
            // }
        }

        private static void AdminCheckSubscriberForDay()
        {
            if (_savedDay != DateTime.Now.Day)
            {
                _savedDay = DateTime.Now.Day;
                _subscribersInDay = 0;
            }
        }

        private static async Task CheckUpdatesForPrivateGroup(ITelegramBotClient botClient)
        {
            var currentUpdate = await botClient.GetUpdatesAsync();
            if (!JsonConvert.SerializeObject(currentUpdate).Contains("-100")) return;
            var getIdPrivateGroup = JsonConvert.SerializeObject(currentUpdate).Split("\"id\":");
            foreach (var result in getIdPrivateGroup)
            {
                if (!result.Contains("-100")) continue;
                //var idPrivateGroup = Convert.ToInt64(result.Substring(4,10));//get private IdGroup
                var idPrivateGroup = result.Substring(4, 10); //get private IdGroup
                if (getIdPrivateGroup[3].Contains("\"status\":\"left\"") &&
                    SQLDbOutPut("PrivateGroup").Count != 0) // if bot leaves private group
                {
                    //PrivateGroup.Remove(idPrivateGroup);
                    SQLDbDelete("PrivateGroup", idPrivateGroup);
                    //Console.WriteLine($"Deleted private group with Id - {idPrivateGroup}");
                    break;
                }
            
                // if (PrivateGroup.ContainsKey(idPrivateGroup)) // if there is chatId 
                // {
                //     break;
                // }
                if (SQLDbOutPut("PrivateGroup").ToList().Where(id => id[1] == idPrivateGroup).Count() > 0)
                {
                    var ff = SQLDbOutPut("PrivateGroup").ToList().Where(id => id[1] == idPrivateGroup).Count();
                    break;
                }
                
            
            
                var getTitlePrivateGroup = result.Split("\"title\":");
                var indexOfTitleEnd = getTitlePrivateGroup[1].IndexOf('}');
                var titlePrivateGroup = getTitlePrivateGroup[1].Substring(1, indexOfTitleEnd - 2);
                //PrivateGroup.Add(idPrivateGroup,titlePrivateGroup);
                SQLDbInput("PrivateGroup", idPrivateGroup, titlePrivateGroup);
                break;
            }
        }

        private async static void GuestOutPutMessage(Update update, ITelegramBotClient botClient)
        {
            // var nowTask = await TaskNowDesk(botClient, update.Message!.Chat.Id, CancellationToken);
            //if (nowTask.Count != 0) return;
            //if (!MoneyOutput.Contains(update.Message!.Chat.Id) || update.Message.Text!.Contains("❤️ Вывести")) return;

            if (_isGuestOutput)
            {
                if (int.TryParse(update.Message.Text.ToCharArray(0, 5), out int _))
                {
                    await botClient.SendTextMessageAsync(update.Message.Chat.Id,
                        "☀️ Ваши реквизиты отправленны на обработку 🤘",
                        parseMode: ParseMode.Html);
                }

                _isGuestOutput = false;
                //MoneyOutput.RemoveAll(personId => personId == update.Message.Chat.Id);
                //Console.WriteLine(update.Message.Text); 
            }
            //Console.WriteLine(update.Message.Text); 
            // MoneyOutput.RemoveAll(personId => personId == update.Message.Chat.Id);
        }

        private static void RefferalUpdater(Update update)
        {
            Console.WriteLine(JsonConvert.SerializeObject(update));
        }

        // private static void AdminOutPutReferralLinks(Update update,ITelegramBotClient botClient)
        // {
        //     var refMessage = "https://t.me/MoonCoinLove_bot?start=001";
        //     
        //     if (RefferalLinks.Count == 0)
        //     {
        //         refMessage = "Нет рассылок";
        //     }
        //     else
        //     {
        //         refMessage += "🔗 Реферальные ссылки\n";
        //         
        //         foreach (var link in RefferalLinks)
        //         {
        //             refMessage += link.Key + " " + link.Value;
        //         }
        //     }
        //     var keyboardMarkup = new InlineKeyboardMarkup(new[]
        //     {
        //         InlineKeyboardButton.WithCallbackData("➕ Добавить","ReferralAdd"),
        //         InlineKeyboardButton.WithCallbackData("🗑 Удалить","ReferralRemove"),
        //     });
        //     botClient.SendTextMessageAsync(update.Message!.Chat.Id, refMessage,replyMarkup:keyboardMarkup);
        //
        // }

        private static string AdminRefferalMailMessage(Update update, MailTypeInfo typeInfo)
        {
            if (update.Message?.Entities != null || update.Message?.CaptionEntities != null)
            {
                switch (typeInfo)
                {
                    case MailTypeInfo.Text:
                    {
                        // var messageText = update.Message.Text;
                        var messageToSend = update.Message.Text;
                        foreach (var entity in update.Message.Entities!)
                        {
                            var post = new SendingPost {Link = entity.Url, Message = update.Message.Text};
                            if (_isSQLSendPosted)
                            {
                                SQLDbInput("SendingPosts", post.Link, post.Message);
                                _isSQLSendPosted = false;
                            }
                           
                            // todo: получить id добавленной записи(например через получения самой последней записи)
                            var lastid = SQLDbOutPut("SendingPosts").Max(id => id[0]).Split(',');
                            post.Id = Convert.ToInt16(lastid[0]);
                            var wordToReplace = update.Message.Text?.Substring(entity.Offset, entity.Length);
                            var redirectUrl = $"https://hyper-llink.ru:7202/c?id={post.Id}&userId={update.Message.From?.Id}";
                            var linkText = $"<a href=\"{redirectUrl}\">{wordToReplace}</a>";
                            //messageText = messageText?.Replace(wordToReplace, linkText);
                            var editedmessage = messageToSend;
                            messageToSend = editedmessage.Replace(wordToReplace, linkText);
                            // return messageText;
                        }

                        return messageToSend;
                        break;
                    }
                    default:
                    {
                        // var messageCaption = update.Message.Caption;
                        // var messageToSend = messageCaption;

                        // foreach (var entity in update.Message.CaptionEntities!)
                        // {
                        //     var wordToReplace = update.Message.Caption?.Substring(entity.Offset, entity.Length);
                        //     var linkText = $"<a href=\"{entity.Url}\">{wordToReplace}</a>";
                        //     
                        //     
                        //     var editedMessage = messageToSend;
                        //     messageToSend = editedMessage.Replace(wordToReplace, linkText);
                        //     // return update.Message.Caption?.Replace(wordToReplace!, linkText);
                        // }
                        // return messageToSend;
                        
                        
                        
                        var messageToSend = update.Message.Caption;
                        foreach (var entity in update.Message.CaptionEntities!)
                        {
                            var post = new SendingPost {Link = entity.Url, Message = update.Message.Caption};
                            if (_isSQLSendPosted)
                            {
                                SQLDbInput("SendingPosts", post.Link, update.Message.Caption);
                                _isSQLSendPosted = false;
                            }
                           
                            // todo: получить id добавленной записи(например через получения самой последней записи)
                            var lastid = SQLDbOutPut("SendingPosts").Max(id => id[0]).Split(',');
                            post.Id = Convert.ToInt16(lastid[0]);
                            var wordToReplace = update.Message.Caption?.Substring(entity.Offset, entity.Length);
                            var redirectUrl = $"https://hyper-llink.ru:7202/c?id={post.Id}&userId={update.Message.From?.Id}";
                            var linkText = $"<a href=\"{redirectUrl}\">{wordToReplace}</a>";
                            //messageText = messageText?.Replace(wordToReplace, linkText);
                            var editedmessage = messageToSend;
                            messageToSend = editedmessage.Replace(wordToReplace, linkText);
                            // return messageText;
                        }
                        return messageToSend;
                        
                        break;
                    }

                }
            }
            else
            {
                return typeInfo switch
                {
                    MailTypeInfo.Text => update.Message?.Text,
                    _ => update.Message?.Caption
                };
            }

            return "No Info";
        }

        enum MailTypeInfo
        {
            Empty,
            Video,
            Text,
            Photo
        }

        public static List<string[]> SQLDbOutPut(string tableName)
        {
            List<string[]> Taskresult = new List<string[]>();
            string sqlTaskExspression = $"SELECT * FROM '{tableName}'";
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                connection.Open();
                SqliteCommand command = new SqliteCommand(sqlTaskExspression, connection);
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            var id = reader.GetValue(0);
                            var link = reader.GetValue(1);
                            var name = reader.GetValue(2);
                            Taskresult.Add(new[] {$"{id}",$"{link}", $"{name}"});                            
                        }
                    }
                }
            }

            return Taskresult;
        }

        public static void SQLDbInput(string tableName, object firstInput, object secondInput)
        {
            switch (tableName)
            {
                case "Tasks":
                    using (var connection = new SqliteConnection(_dbConnectionString))
                    {
                        connection.Open();
                        var command = new SqliteCommand();
                        command.Connection = connection;
                        command.CommandText =
                            $"INSERT INTO '{tableName}'(Link,Name) VALUES ('{firstInput}','{secondInput}')";
                        //command.CommandText = $"INSERT INTO {tableName} (GroupID,GroupName) VALUES ({firstInput},{secondInput})";
                        command.ExecuteNonQuery();
                    }

                    break;
                case "CashById":
                    using (var connection = new SqliteConnection(_dbConnectionString))
                    {
                        connection.Open();
                        SqliteCommand command = new SqliteCommand();
                        command.Connection = connection;
                        command.CommandText =
                            $"INSERT INTO '{tableName}'(SubscriberID,CashAmount) VALUES ('{firstInput}','{secondInput}')";
                        //command.CommandText = $"INSERT INTO {tableName} (GroupID,GroupName) VALUES ({firstInput},{secondInput})";
                        command.ExecuteNonQuery();
                    }

                    break;
                case "PrivateGroup":
                    using (var connection = new SqliteConnection(_dbConnectionString))
                    {
                        connection.Open();
                        SqliteCommand command = new SqliteCommand();
                        command.Connection = connection;
                        command.CommandText =
                            $"INSERT INTO '{tableName}'(GroupID,GroupName) VALUES ('{firstInput}','{secondInput}')";
                        command.ExecuteNonQuery();
                        Console.WriteLine($"INPUT PRIVATE GROUP {firstInput + " " + secondInput}");
                    }

                    break;
                case "RefferalCashById":
                    using (var connection = new SqliteConnection(_dbConnectionString))
                    {
                        connection.Open();
                        SqliteCommand command = new SqliteCommand();
                        command.Connection = connection;
                        command.CommandText =
                            $"INSERT INTO '{tableName}'(Link,RefferalCount) VALUES ('{firstInput}','{secondInput}')";
                        command.ExecuteNonQuery();
                    }

                    break;
                case "SendingPosts":
                    using (var connection = new SqliteConnection(_dbConnectionString))
                    {
                        connection.Open();
                        SqliteCommand command = new SqliteCommand();
                        command.Connection = connection;
                        command.CommandText =
                            $"INSERT INTO '{tableName}'(Link,Message) VALUES ('{firstInput}','{secondInput}')";
                        command.ExecuteNonQuery();
                    }

                    break;
            }

        }

        public static void SQLDbDUpdateCash(string tableName, object keyInput, object valueInput)
        {
            switch (tableName)
            {
                case "RefferalCashById":
                    string sqlExpression = $"UPDATE '{tableName}' SET RefferalCount={valueInput} WHERE Link={keyInput}";

                    using (var connection = new SqliteConnection(_dbConnectionString))
                    {
                        connection.Open();
                        SqliteCommand command = new SqliteCommand(sqlExpression, connection);
                        command.ExecuteNonQuery();
                        break;
                    }
                case "CashById":
                    string sqlCashExspressions =
                        $"UPDATE '{tableName}' SET CashAmount={valueInput} WHERE SubscriberID={keyInput}";

                    using (var connection = new SqliteConnection(_dbConnectionString))
                    {
                        connection.Open();
                        SqliteCommand command = new SqliteCommand(sqlCashExspressions, connection);
                        command.ExecuteNonQuery();
                        break;
                    }
            }
        }

        public static void SQLDbDelete(string tableName, object deleteValue)
        {
            switch (tableName)
            {
                case "Tasks":
                    string sqlTasksExpression = $"DELETE FROM '{tableName}' WHERE Name ='{deleteValue}'";
                    using (var connection = new SqliteConnection(_dbConnectionString))
                    {
                        connection.Open();
                        SqliteCommand command = new SqliteCommand(sqlTasksExpression, connection);
                        command.ExecuteNonQuery();
                    }

                    break;
                case "CashById":
                    string sqlCashExpression = $"DELETE FROM '{tableName}' WHERE SubscriberID ='{deleteValue}'";
                    using (var connection = new SqliteConnection(_dbConnectionString))
                    {
                        connection.Open();
                        SqliteCommand command = new SqliteCommand(sqlCashExpression, connection);
                        command.ExecuteNonQuery();
                    }

                    break;
                case "PrivateGroup":
                    string sqlPrivateExpression = $"DELETE FROM '{tableName}' WHERE GroupID ='{deleteValue}'";
                    using (var connection = new SqliteConnection(_dbConnectionString))
                    {
                        connection.Open();
                        SqliteCommand command = new SqliteCommand(sqlPrivateExpression, connection);
                        command.ExecuteNonQuery();
                       
                    }

                    break;
                case "RefferalCashById":
                    string sqlRefferalExspression = $"DELETE FROM '{tableName}' WHERE Link ='{deleteValue}'";
                    using (var connection = new SqliteConnection(_dbConnectionString))
                    {
                        connection.Open();
                        SqliteCommand command = new SqliteCommand(sqlRefferalExspression, connection);
                        command.ExecuteNonQuery();
                    }
                    
                    break;
                case "PostVisitors":
                    string sqlPostVisitorslExspression = $"DELETE FROM '{tableName}' WHERE SendingPostId ='{deleteValue}'";
                    using (var connection = new SqliteConnection(_dbConnectionString))
                    {
                        connection.Open();
                        SqliteCommand command = new SqliteCommand(sqlPostVisitorslExspression, connection);
                        command.ExecuteNonQuery();
                    }
                    break;
                case "SendingPosts":
                    string sqlSendingPostsExspression = $"DELETE FROM '{tableName}' WHERE Id ='{deleteValue}'";
                    using (var connection = new SqliteConnection(_dbConnectionString))
                    {
                        connection.Open();
                        SqliteCommand command = new SqliteCommand(sqlSendingPostsExspression, connection);
                        command.ExecuteNonQuery();
                    }
                    break;
            }
        }
       
    }
}
