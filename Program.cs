using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

var bot = new TelegramBotClient("8744734435:AAGU8mu8EkJhTYlQTJAtnSaQBxWm2hOj6hc");

var repo = new FileRepository(); // твой класс для хранения данных
var httpClient = new HttpClient();
var cts = new CancellationTokenSource();

// Список подписанных пользователей
HashSet<long> subscribedChatIds = new HashSet<long>();

// ========================== Запуск бота ==========================
var receiverOptions = new ReceiverOptions { AllowedUpdates = { } };

var mainMenu = new ReplyKeyboardMarkup(new[]
{
    new KeyboardButton[] { "/add", "/list" },
    new KeyboardButton[] { "/delete", "/suppliers" }
})
{
  ResizeKeyboard = true // подгоняет клавиатуру под экран
};
// Загружаем справочник поставщиков
var suppliersDictionary = JsonSerializer.Deserialize<List<Supplier>>(System.IO.File.ReadAllText("data/suppliers.json"))
    .ToDictionary(s => s.id, s => s); // ключ = id, значение = объект Supplier


bot.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandleErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

Console.WriteLine("Бот запущен...");

// ========================== Обработчик сообщений ==========================
async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct)
{
  var message = update.Message;
  if (message == null || message.Text == null) return;

  long chatId = message.Chat.Id;

  // Подписываем пользователя на уведомления
  subscribedChatIds.Add(chatId);

  string text = message.Text.Trim();
  if (text == "/start")
  {
    await client.SendTextMessageAsync(chatId,
        "Привет! Я бот для мониторинга поступления товаров.\nВыбери действие:",
        replyMarkup: mainMenu);
    return;
  }
  if (text.StartsWith("/add"))
  {
    var parts = text.Split(" ");
    if (parts.Length < 3)
    {
      await client.SendTextMessageAsync(chatId, "Используй: /add supplierId productId");
      return;
    }

    int supplierId = int.Parse(parts[1]);
    int productId = int.Parse(parts[2]);

    repo.AddProduct(supplierId, productId);
    await client.SendTextMessageAsync(chatId, $"Товар {productId} добавлен для поставщика {supplierId} ✅");
  }
  else if (text.StartsWith("/suppliers"))
  {
    var suppliers = repo.LoadData();
    if (!suppliers.Any())
    {
      await client.SendTextMessageAsync(chatId, "Список поставщиков пуст");
      return;
    }

    // Формируем список в виде "ID - Название (Город)"
    string list = string.Join("\n", suppliers.Select(s =>
    {
      string name = suppliersDictionary.ContainsKey(s.SupplierId)
          ? suppliersDictionary[s.SupplierId].name
          : $"ID {s.SupplierId}";
      string city = suppliersDictionary.ContainsKey(s.SupplierId)
          ? suppliersDictionary[s.SupplierId].city
          : "";
      return $"{s.SupplierId} - {name} ({city})";
    }));

    await client.SendTextMessageAsync(chatId, $"Список поставщиков:\n{list}");
  }
  else if (text.StartsWith("/list"))
  {
    var parts = text.Split(" ");
    if (parts.Length < 2)
    {
      await client.SendTextMessageAsync(chatId, "Используй: /list supplierId");
      return;
    }

    int supplierId = int.Parse(parts[1]);
    var products = repo.GetProducts(supplierId);
    if (!products.Any())
    {
      await client.SendTextMessageAsync(chatId, $"Нет товаров для поставщика {supplierId}");
      return;
    }

    await client.SendTextMessageAsync(chatId, $"Поставщик {supplierId}:\n{string.Join(", ", products)}");
  }
  else if (text.StartsWith("/delete"))
  {
    var parts = text.Split(" ");
    if (parts.Length < 3)
    {
      await client.SendTextMessageAsync(chatId, "Используй: /delete supplierId productId");
      return;
    }

    int supplierId = int.Parse(parts[1]);
    int productId = int.Parse(parts[2]);

    repo.DeleteProduct(supplierId, productId);
    await client.SendTextMessageAsync(chatId, $"Товар {productId} удалён для поставщика {supplierId} ✅");
  }
}

// ========================== Обработчик ошибок ==========================
Task HandleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken ct)
{
  Console.WriteLine(exception);
  return Task.CompletedTask;
}


// ========================== Фоновый мониторинг ==========================
_ = Task.Run(async () =>
{
  while (true)
  {
    var suppliers = repo.LoadData();
    if (!suppliers.Any())
    {
      await Task.Delay(30000);
      continue;
    }

    var payload = suppliers.Select(s => new object[] { s.SupplierId, s.ProductIds.ToArray() }).ToList();

    try
    {
      var response = await httpClient.PostAsJsonAsync("https://greenleaf-global.com/api/v1/delivery/goods/rest", payload);
      var stocks = await response.Content.ReadFromJsonAsync<List<List<int>>>() ?? new List<List<int>>();
      for (int i = 0; i < suppliers.Count; i++)
      {
        var supplier = suppliers[i];
        // если LastStock пустой или меньше ProductIds, инициализируем
        if (supplier.LastStock == null || supplier.LastStock.Count != supplier.ProductIds.Count)
        {
          supplier.LastStock = supplier.ProductIds.Select(_ => 0).ToList();
        }
      }

      for (int i = 0; i < suppliers.Count; i++)
      {
        if (i >= stocks.Count)
        {
          Console.WriteLine($"[WARN] Нет данных по поставщику {suppliers[i].SupplierId}");
          continue;
        }

        var supplier = suppliers[i];
        var supplierStocks = stocks[i];

        for (int j = 0; j < supplier.ProductIds.Count; j++)
        {
          int productId = supplier.ProductIds[j];

          // Защита от несоответствия длины
          int stock = (j < supplierStocks.Count) ? supplierStocks[j] : 0;

          if (stock > supplier.LastStock[j])
          {
            // Получаем название поставщика из справочника
            string supplierName = suppliersDictionary.ContainsKey(supplier.SupplierId)
                ? suppliersDictionary[supplier.SupplierId].name
                : $"ID {supplier.SupplierId}";

            string city = suppliersDictionary.ContainsKey(supplier.SupplierId)
                ? suppliersDictionary[supplier.SupplierId].city
                : "";
            foreach (var chatId in subscribedChatIds)
            {
              await bot.SendTextMessageAsync(chatId,
                               $"🔥 Товар {productId} появился у поставщика {supplierName} ({city})");
            }
          }
          supplier.LastStock[j] = stock;
        }
      }

      repo.SaveData(suppliers);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Ошибка API: {ex.Message}");
    }

    await Task.Delay(30000);
  }
});

Console.ReadLine();