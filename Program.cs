using Final_Project;
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotWiki
{
    class Program
    {
        private static readonly TelegramBotClient Bot = new("7868047916:AAE7nMJPFz_OpjdrRXzazz72wut5_kJJ_y8");
        private static readonly Information WikiInfo = new();
        private static readonly Dictionary<string, string> TopicsCache = new();

        static async Task Main(string[] args)
        {
            using var cts = new CancellationTokenSource();

            Bot.StartReceiving(
                updateHandler: HandleUpdate,
                errorHandler: HandleError,
                cancellationToken: cts.Token
            );

            Console.WriteLine("Bot is up and running. Press Enter to stop.");
            Console.ReadLine();

            cts.Cancel();
        }

        private static async Task HandleUpdate(ITelegramBotClient _, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                await HandleMessage(update.Message);
            }
            else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
            {
                var query = update.CallbackQuery;
                if (TopicsCache.ContainsKey(query.Data))
                {
                    await HandleButton(query);
                }
                else
                {
                    await HandleSectionRequest(query);
                }
            }
        }


        private static Task HandleError(ITelegramBotClient _, Exception exception, CancellationToken cancellationToken)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return Task.CompletedTask;
        }

        private static async Task HandleMessage(Message message)
        {
            var userId = message.From?.Id ?? 0;
            var text = message.Text.Trim();

            if (text.StartsWith("/start"))
            {
                await Bot.SendTextMessageAsync(userId, "Добро пожаловать! Введите тему для поиска информации в Википедии.");
            }
            else
            {
                var topics = await WikiInfo.SearchTopicsAsync(text);

                if (topics.Count == 0)
                {
                    await Bot.SendTextMessageAsync(userId, "К сожалению, ничего не найдено по вашему запросу.");
                }
                else
                {
                    // Создаем кнопки с укороченными данными
                    var buttons = new List<InlineKeyboardButton[]>();
                    TopicsCache.Clear();

                    for (int i = 0; i < topics.Count; i++)
                    {
                        var topicId = $"topic_{i}";
                        TopicsCache[topicId] = topics[i]; // Сохраняем полное название темы
                        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData(topics[i], topicId) });
                    }

                    await Bot.SendTextMessageAsync(
                        userId,
                        "Вот что удалось найти:",
                        replyMarkup: new InlineKeyboardMarkup(buttons)
                    );
                }
            }
        }

        private static async Task HandleButton(CallbackQuery query)
        {
            var userId = query.From.Id;
            var topicId = query.Data;

            if (string.IsNullOrEmpty(topicId) || !TopicsCache.TryGetValue(topicId, out var selectedTopic)) return;

            var sections = await WikiInfo.GetArticleSectionsAsync(selectedTopic);

            if (sections.Count == 0)
            {
                await Bot.SendTextMessageAsync(userId, "К сожалению, не удалось найти разделы для данной темы.");
            }
            else
            {
                var buttons = sections.Select((section, index) => InlineKeyboardButton.WithCallbackData(section, index.ToString()))
                                      .Select(button => new[] { button });

                await Bot.SendTextMessageAsync(
                    userId,
                    $"Вы выбрали тему: ({selectedTopic}.) Выберите раздел для получения информации:",
                    replyMarkup: new InlineKeyboardMarkup(buttons)
                );
            }
        }

        // Метод для отправки текста раздела по частям
        private static async Task HandleSectionRequest(CallbackQuery query)
        {
            var userId = query.From.Id;
            var sectionIndex = 0;

            // Попробуем получить индекс секции
            if (!int.TryParse(query.Data, out sectionIndex))
            {
                await Bot.SendTextMessageAsync(userId, "Ошибка: некорректный индекс секции.");
                return;
            }

            var title = query.Message?.Text;
            var firstIndex = title.IndexOf('(');
            var secondIndex = title.IndexOf(')');
            var part = title.Substring(firstIndex + 1, secondIndex - firstIndex - 2);
            title = part;

            if (string.IsNullOrEmpty(title))
            {
                await Bot.SendTextMessageAsync(userId, "Ошибка: не удалось определить тему.");
                return;
            }

            Console.WriteLine(title);
            Console.WriteLine(sectionIndex);

            // Получаем текст секции по частям
            var contentChunks = await WikiInfo.GetSectionContentAsync(title, sectionIndex + 1);

            foreach (var chunk in contentChunks)
            {
                await Bot.SendTextMessageAsync(userId, chunk);
            }
        }


    }
}
