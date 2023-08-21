using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Polling;
using ClosedXML.Excel;
using Telegram.Bot.Types.Enums;
using Npgsql;
using System.Text;
using System.Text.RegularExpressions;

namespace TelegramBotExperiments
{

    class Program
    {
        static ITelegramBotClient bot = new TelegramBotClient("6484251570:AAF94t5Q_jDJOVprzg60pVLuSB-CIylWHhA");

        static string connString = "Host=db;Username=ApiBotTeleGrUser;Password=ApiBotPS2007;Database=ApiBotTeleGrUser";      

        public static List<DataSource> DataSourcesG = new List<DataSource>();

        private static List<UserData> _dataUsers;
        public static List<UserData> UsersDataSource => GetUserDatas();

        public static UserData currentData = new UserData();

        public static string tokenAdmin = "79c333a6227bee0069a6b57a270b8249";

        public static bool isBlockUser = false;
        public static bool isUnBlockUser = false;
        public static bool isAdminPanel = false;


        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(update));
            if (update.Type == UpdateType.Message)
            {
                var message = update.Message;
                if (UsersDataSource.Any(x => x.Role == "Admin" && x.UserName == message.From.Id.ToString()) && message.Type != MessageType.Document )
                {
                    if (message.Text.ToLower() == "/start" || message.Text.ToLower() == "перезапустить")
                    {

                        isAdminPanel = true;
                        ReplyKeyboardMarkup replyKeyboardMarkup = new(new[] { new KeyboardButton[] { "Перезапустить", "Помощь", "Пользователи" },
                                                                              new KeyboardButton[] { "Заблокировать Пользователя" },
                                                                              new KeyboardButton[] { "Разблокировать Пользователя" },
                        })
                        {
                            ResizeKeyboard = true
                        };
                        Message sentMessage = await botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "Теперь вам доступно меню с командами",
                        replyMarkup: replyKeyboardMarkup,
                        cancellationToken: cancellationToken);
                        await botClient.SendTextMessageAsync(message.Chat, "Вы вошли как Администратор 👑👑👑");
                        await botClient.SendTextMessageAsync(message.Chat, "Что бы подгрузить данные, всавьте файл Excel");

                    }

                    if (message.Text.ToLower() == "/users" || message.Text.ToLower() == "пользователи".ToLower())
                    {
                        StringBuilder build = new StringBuilder();

                        

                        using (XLWorkbook workbook = new XLWorkbook())
                        {
                            // Создаем новую рабочую книгу
                            IXLWorksheet worksheet = workbook.Worksheets.Add("Sheet1");

                            // Заполняем некоторые данные в ячейки
                            worksheet.Cell(1, 1).Value = "ID";
                            worksheet.Cell(1, 3).Value = "ФИО";
                            worksheet.Cell(1, 4).Value = "Номер тел";
                            int row = 2;
                            foreach (var user in UsersDataSource.OrderBy(x => x.Role).ToList())
                            {
                                worksheet.Cell(row, 1).Value = user.Empid;
                                worksheet.Cell(row, 2).Value = user.Role;
                                worksheet.Cell(row, 3).Value = user.FullName;
                                worksheet.Cell(row, 4).Value = user.NumberPhone;
                                row++;
                            }

                            // Сохраняем файл Excel в память
                            using (MemoryStream ms = new MemoryStream())
                            {
                                workbook.SaveAs(ms);
                                ms.Position = 0;

                                // Создаем экземпляр InputFileStream
                                InputFileStream inputFileStream = new InputFileStream(ms, "example.xlsx");

                                // Отправляем файл Excel в качестве ответа на сообщение пользователя
                                await bot.SendDocumentAsync(message.Chat.Id, inputFileStream);
                            }
                        }

                    }

                    if (isBlockUser)
                    {
                        isBlockUser = false;
                        var EmpId = Convert.ToInt32(message.Text);
                        var pattern = "^((8|\\+7)[\\- ]?)?(\\(?\\d{3}\\)?[\\- ]?)?[\\d\\- ]{7,10}$";

                        bool isValidNumber = Regex.IsMatch(message.Text.Replace(" ", ""), pattern);

                        if (isValidNumber)
                        {
                            QueryInsertUser("NoName", "NoName", "Block", message.Text, 0);

                            await botClient.SendTextMessageAsync(message.Chat, $@"Пользователь по номеру телефона {message.Text} - заблокирован");
                        }
                        else
                        {
                            try
                            {
                                QueryUpdateAdmin("Block", EmpId);

                                await botClient.SendTextMessageAsync(message.Chat, $@"Пользователь ID:{EmpId} заблокирован");
                            }
                            catch (Exception e)
                            {
                                await botClient.SendTextMessageAsync(message.Chat, $@"Пользователь ID:{EmpId} не заблокирован {e.Message}");
                            }

                        }
                        return;
                    }



                    if (message.Text.ToLower() == "Заблокировать Пользователя".ToLower())
                    {
                        isBlockUser = true;
                        await botClient.SendTextMessageAsync(message.Chat, $@"Введите ИД/или номер телефона пользователя");
                        return;
                    }



                    if (isUnBlockUser)
                    {
                        isUnBlockUser = false;
                        var EmpId = Convert.ToInt32(message.Text);
                        try
                        {
                            QueryUpdateAdmin("Active", EmpId);

                            await botClient.SendTextMessageAsync(message.Chat, $@"Пользователь ID:{EmpId} разблокирован");
                        }
                        catch (Exception e)
                        {
                            await botClient.SendTextMessageAsync(message.Chat, $@"Пользователь ID:{EmpId} не разблокирован {e.Message}");
                        }
                        return;
                    }

                    if (message.Text.ToLower() == "Разблокировать Пользователя".ToLower())
                    {
                        isUnBlockUser = true;
                        await botClient.SendTextMessageAsync(message.Chat, $@"Введите ИД пользователя");
                        return;
                    }


                }


                if (message.Type == MessageType.Document && message.Document.FileName.EndsWith(".xlsx") && UsersDataSource.Any(x => x.Role == "Admin" && x.UserName == message.From.Id.ToString()))
                {
                    QueryTruncate("Services");
                    // Получите информацию о документе
                    var document = message.Document;
                    var fileId = document.FileId;
                    var fileName = document.FileName;

                    // Скачайте файл в поток данных
                    var fileStream = await botClient.GetFileAsync(fileId);

                    // Загружаем скачанный файл
                    using (var stream = System.IO.File.OpenWrite(document.FileName))
                    {
                        await botClient.DownloadFileAsync(fileStream.FilePath, stream);
                    }

                    string filePath = Path.GetFileName(document.FileName);

                    try
                    {
                        using (var workbook = new XLWorkbook(filePath))
                        {
                            var worksheet = workbook.Worksheet(1);
                            int rowCount = worksheet.RowsUsed().Count();
                            int columCount = worksheet.ColumnsUsed().Count();

                            for (int row = 2; row <= rowCount; row++)
                            {
                                var model_search = worksheet.Cell(row, 1).Value.ToString().ToLower();
                                var model = worksheet.Cell(row, 2).Value.ToString();
                                if (model_search != string.Empty)
                                {
                                    string str = "";

                                    for (int col = 3; col <= columCount; col++)
                                    {
                                        str += $@"{(str == string.Empty ? "" : "@")}{getStrExcel(worksheet, 1, col)}${getStrExcel(worksheet, row, col)}";
                                    }
                                    QueryInsert(model_search, model, str);
                                }
                            }
                        }

                        DataSourcesG = QuerySelect();
                        await botClient.SendTextMessageAsync(message.Chat, "Данные загружены");
                    }
                    catch (Exception x)
                    {
                        Console.WriteLine(x.Message);
                    }
                }
                string getStrExcel(IXLWorksheet? worksheet, int row, int colum)
                {
                    return worksheet.Cell(row, colum).Value.ToString();
                }

                if (message.Text is null) return;

                if (UsersDataSource.Any(x => x.UserName == message.From.Id.ToString() && x.Role == "Block"))
                {
                    await botClient.SendTextMessageAsync(message.Chat, "Вы заблокированы, обратитесь к администраторам, обратитесь за помощью @NEAMIZE");
                    return;
                }


                if (currentData.Status == 0 && !UsersDataSource.Any(x =>  x.UserName == message.From.Id.ToString()))
                {
                    if (message.Text != string.Empty)
                    {
                        currentData.Status = 1;
                        await botClient.SendTextMessageAsync(message.Chat, "Введите ваше ФИО");
                    }
                    return;
                }


                if (currentData.Status == 1)
                {                    
                    if (message.Text != string.Empty)
                    {
                        currentData.FullName = message.Text;
                        currentData.Status = 2;
                        await botClient.SendTextMessageAsync(message.Chat, "Введите ваш номер телефона");
                    }
                    return;
                }

                if (currentData.Status == 2)
                {

                    if (message.Text != string.Empty)
                    {
                        currentData.NumberPhone = message.Text.Replace(" ","").Replace("(","").Replace(")", "").Replace("-", "");


                        currentData.Status = 3;
                        if (QueryGetStatus(message.From.Id.ToString()) == -1)
                        {
                            currentData.Role = "Active";

                            var numberPhone = currentData.NumberPhone;

                            if (currentData.NumberPhone.StartsWith("+"))
                            {
                                numberPhone = currentData.NumberPhone.Replace("+7", "8");
                            }

                            if (currentData.NumberPhone.StartsWith("9"))
                            {
                                numberPhone = $"8{currentData.NumberPhone}";
                            }

                            if (UsersDataSource.Any(x => x.NumberPhone == numberPhone))
                            {
                                QueryDeleteUserNumberPhone(numberPhone);
                                QueryInsertUser(message.From.Id.ToString(), currentData.FullName, "Block", numberPhone, currentData.Status);
                                await botClient.SendTextMessageAsync(message.Chat, "Вы были заблокированы администратором обратитесь за помощью @NEAMIZE");
                            }
                            else
                            {

                                QueryInsertUser(message.From.Id.ToString(), currentData.FullName, currentData.Role, numberPhone, currentData.Status);
                                await botClient.SendTextMessageAsync(message.Chat, "Данные успешно сохранены, можно вводить запрос на поиск");
                            }
                        }
                        else
                            QueryInsertStatus(currentData.Status, message.From.Id.ToString());

                        
                    }
                    return;
                }



                if (message.Text.ToLower() == "Помощь".ToLower())
                {
                    string response = "Обратитесь за помощью к администратору @NEAMIZE";
                    await botClient.SendTextMessageAsync(message.Chat, response);
                    return;
                }


                if (message.Text.ToLower() == "/start" || message.Text.ToLower() == "перезапустить".ToLower())
                {
                    currentData.Status = 0;

                    if (UsersDataSource.Any(x => x.UserName == message.From.Id.ToString()))
                    {
                        await botClient.SendTextMessageAsync(message.Chat, "Добро пожаловать! Перезапуск выполнен...");
                        return;
                    }

                    string response = "Встречайте бота, который сделает вашу жизнь проще и поможет вам с разными задачами! 🤖\r\n\r\nЧто умеет этот бот?\r\n\r\n🔍 Поиск по услугам:\r\n   - Смотрите совместимость стекол и сенсоров между моделями.\r\n   - Узнавайте примерные цены на переклей.\r\n   - Проверяйте наличие запчастей на центральном складе или у представителей.\r\n   - Исследуйте самый большой выбор модельного ряда.\r\n\r\n\U0001f91d Зарегистрируйтесь и облегчите работу себе и своим сотрудникам с помощью удобной автоматизации процессов.\r\n\r\nНе теряйте время зря, просто задайте мне вопрос, и я найду нужные вам ответы! 🚀";

                    ReplyKeyboardMarkup replyKeyboardMarkup = new(new[] { new KeyboardButton[] { "Перезапустить", "Помощь" }, })
                    {
                        ResizeKeyboard = true
                    };
                    if (!isAdminPanel)
                    {
                        Message sentMessage = await botClient.SendTextMessageAsync(
                            chatId: message.Chat.Id,
                            text: "Теперь вам доступно меню с командами",
                            replyMarkup: replyKeyboardMarkup,
                            cancellationToken: cancellationToken);
                    }

                    await botClient.SendTextMessageAsync(message.Chat, response);                                    


                    return;
                }

                if (message.Text.ToLower() == tokenAdmin && (currentData.Status == 3 || QueryGetStatus(message.From.Id.ToString()) == 3))
                {
                    QueryUpdateAdmin("Admin", message.From.Id.ToString());
                    await botClient.SendTextMessageAsync(message.Chat, "Теперь вы админ, вам доступна команда /users и загрузка данных в базу");
                }

                if (message.Text.ToLower() != tokenAdmin &&  (currentData.Status == 3 || QueryGetStatus(message.From.Id.ToString()) == 3) && message.Text.ToLower() != "Перезапустить".ToLower() && message.Text.ToLower() != "Помощь".ToLower() && message.Text.ToLower() != "Пользователи".ToLower() && message.Text.ToLower() != "УдалитьПользователя".ToLower())
                {
                    bool isCompliteSearch = false;
                    var messageText = message.Text.ToLower().Split(' ');

                    var dataSource = DataSourcesG;

                    var dataSourceNew = new List<DataSource>();
                    foreach (var itemDataSource in dataSource.Where(x => x.ModelSearch.Contains(message.Text.ToLower().Split(' ')[0])))
                    {
                        bool isSearchMessage = true;
                        foreach (var itemWordSecond in message.Text.ToLower().Split(' '))
                        {
                            if (!itemDataSource.ModelSearch.Split(',').Contains(itemWordSecond))
                            {
                                isSearchMessage = false;
                                break;
                            }
                        }

                        if (isSearchMessage) dataSourceNew.Add(itemDataSource);
                    }

                    if (dataSourceNew.Count == 0)
                    {
                        string resMessage = "";
                        string input = message.Text.ToLower();
                        string pattern = @"(\D+)(\d+)";
                        Match match = Regex.Match(input, pattern);

                        if (match.Success)
                        {
                            string text = match.Groups[1].Value;
                            int number = int.Parse(match.Groups[2].Value);
                            resMessage = $"{text} {number}";
                            foreach (var itemDataSource in dataSource.Where(x => x.ModelSearch.Contains(resMessage.ToLower().Split(' ')[0])))
                            {
                                bool isSearchMessage = true;
                                foreach (var itemWordSecond in resMessage.ToLower().Split(' '))
                                {
                                    if (!itemDataSource.ModelSearch.Split(',').Contains(itemWordSecond))
                                    {
                                        isSearchMessage = false;
                                        break;
                                    }
                                }

                                if (isSearchMessage) dataSourceNew.Add(itemDataSource);
                            }
                        }
                    }

                    StringBuilder builder = new StringBuilder();

                    foreach (var item in dataSourceNew)
                    {

                        if (builder.ToString() != "") builder.AppendLine("-----------------------");
                        builder.AppendLine(@$"Модель устройства: {item.ModelName}{Environment.NewLine}{Environment.NewLine}Доступны следующие услуги:");

                        foreach (var textItem in item.Services.Split('@'))
                        {
                            var textElement = textItem.Split('$');
                            if (textElement[1] != "") builder.AppendLine($@"{textElement[0]}: {textElement[1]} руб."); ;
                        }

                        isCompliteSearch = true;

                    }
                    if (isCompliteSearch)
                        try
                        {
                            await botClient.SendTextMessageAsync(message.Chat, builder.ToString());
                        }
                        catch
                        {
                            await botClient.SendTextMessageAsync(message.Chat, "Ответ получился слишком длинным, просьба утончить запрос");
                        }


                    if (dataSourceNew.Count == 0)
                        await botClient.SendTextMessageAsync(message.Chat, "По вашему запросу ничего не найдень, обратитесь за помощью @NEAMIZE");
                }

            }

            if (update.Type == UpdateType.CallbackQuery)
            {
                // Тут идет обработка всех нажатий на кнопки, тут никаких особых доп условий не надо, тк у каждой кнопки своя ссылка
                var callbackQuery = update.CallbackQuery;
                var nameCommand = callbackQuery.Data;
                //if (nameCommand == "master")
                //{
                //    botClient.DeleteMessageAsync(
                //            callbackQuery.Message.Chat.Id,
                //            callbackQuery.Message.MessageId);
                //    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Вы зашли под ролью Мастер");

                //    currentData.Role = "master";
                //    currentData.Status = 1;
                //    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Введите ваше ФИО");
                //}

                //if (nameCommand == "provider")
                //{
                //    botClient.DeleteMessageAsync(
                //            callbackQuery.Message.Chat.Id,
                //            callbackQuery.Message.MessageId);
                //    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Вы зашли под ролью Поставщик");
                //    currentData.Role = "provider";
                //    currentData.Status = 1;
                //    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Введите ваше ФИО");
                //}

                if (nameCommand == "listAdmin")
                {

                    StringBuilder build = new StringBuilder();

                    foreach (var user in UsersDataSource.OrderBy(x=>x.Role).ToList())
                    {
                        build.AppendLine($@"ID: {user.Empid} Роль: {user.Role} ФИО: {user.FullName} Номер тел. {user.NumberPhone}");
                    }

                    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, build.ToString());
                }
                
            }

        }

       

        public static async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(exception));
        }

        static async Task RunBot()
        {
            Console.WriteLine("Запущен бот " + bot.GetMeAsync().Result.FirstName);

            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { },
            };

            try
            {
                await bot.ReceiveAsync(
                    HandleUpdateAsync,
                    HandleErrorAsync,
                    receiverOptions,
                    cancellationToken
                );
            }
            catch (Exception x)
            {
                cts.Cancel();
                Console.WriteLine(x.Message);
            }
            Console.WriteLine(cts.IsCancellationRequested);
        }


        async static Task Main(string[] args)
        {
            MigrateDatabase();

            Console.WriteLine("Загружаем данные из БД ");

            DataSourcesG = QuerySelect();

            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { },
            };

            await Task.Run(() => RunBot());
            

            Console.WriteLine(cancellationToken.CanBeCanceled);
            Console.ReadLine();
        }


        public static List<DataSource> QuerySelect()
        {
            List<DataSource> list = new List<DataSource>();
            using (NpgsqlConnection connection = new NpgsqlConnection(connString))
            {
                connection.Open();

                using (NpgsqlCommand command = new NpgsqlCommand("SELECT * FROM \"Services\";", connection))
                {     
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {

                            list.Add(
                                new DataSource
                                {
                                    ModelSearch = (string)reader["model_search"],
                                    ModelName = (string)reader["model"],
                                    Services = (string)reader["Text"],
                                }
                                );
                        }
                    }
                }
            }

            return list;
        }

        public static List<UserData> GetUserDatas()
        {
            var list = new List<UserData>();

            using (NpgsqlConnection connection = new NpgsqlConnection(connString))
            {
                connection.Open();

                using (NpgsqlCommand command = new NpgsqlCommand("SELECT * FROM \"Users\" WHERE \"UserName\" IS NOT NULL;", connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {

                            list.Add(
                                new UserData
                                {
                                    Empid = (int)reader["EmpId"],
                                    UserName = (string)reader["UserName"],
                                    FullName = (string)reader["FullName"],
                                    Role = (string)reader["Role"],
                                    NumberPhone = (string)reader["NumberPhone"],
                                    Status = (int)reader["Status"],
                                }
                                );
                        }
                    }
                }
            }

            return list;
        }



        public static void QueryInsert(string model_search,string model,string text)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connString))
            {
                connection.Open();

                using (NpgsqlCommand command = new NpgsqlCommand("INSERT INTO public.\"Services\"(model_search, model, \"Text\") VALUES ( @model_search, @model, @Text);", connection))
                {
                    command.Parameters.AddWithValue("model_search", model_search);
                    command.Parameters.AddWithValue("model", model);
                    command.Parameters.AddWithValue("Text", text);

                    int rowsAffected = command.ExecuteNonQuery();
                }
            }
        }

        public static void QueryInsertStatus(int status,string userMame)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connString))
            {
                connection.Open();

                using (NpgsqlCommand command = new NpgsqlCommand(@$"UPDATE public.""Users"" SET  ""Status""={status} WHERE ""UserName"" = '{userMame}';", connection))
                {
                    
                    int rowsAffected = command.ExecuteNonQuery();
                }
            }
        }

        public static void QueryUpdateAdmin(string role, string userMame)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connString))
            {
                connection.Open();

                using (NpgsqlCommand command = new NpgsqlCommand(@$"UPDATE public.""Users"" SET  ""Role""='{role}' WHERE ""UserName"" = '{userMame}';", connection))
                {

                    int rowsAffected = command.ExecuteNonQuery();
                }
            }
        }

        public static void QueryUpdateAdmin(string role, int userMame)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connString))
            {
                connection.Open();

                using (NpgsqlCommand command = new NpgsqlCommand(@$"UPDATE public.""Users"" SET  ""Role""='{role}' WHERE ""EmpId"" = '{userMame}';", connection))
                {

                    int rowsAffected = command.ExecuteNonQuery();
                }
            }
        }

        //public static void QueryDeleteUser(int EmpId)
        //{
        //    using (NpgsqlConnection connection = new NpgsqlConnection(connString))
        //    {
        //        connection.Open();

        //        using (NpgsqlCommand command = new NpgsqlCommand(@$"DELETE FROM public.""Users"" WHERE  ""EmpId"" = '{EmpId}';", connection))
        //        {

        //            int rowsAffected = command.ExecuteNonQuery();
        //        }
        //    }
        //}

        public static void QueryDeleteUserNumberPhone(string NumberPhone)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connString))
            {
                connection.Open();

                using (NpgsqlCommand command = new NpgsqlCommand(@$"DELETE FROM public.""Users"" WHERE  ""NumberPhone"" = '{NumberPhone}';", connection))
                {

                    int rowsAffected = command.ExecuteNonQuery();
                }
            }
        }

        public static void QueryInsertUser(string userName, string fullName, string role, string numberPhone, int status)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connString))
            {
                connection.Open();

                using (NpgsqlCommand command = new NpgsqlCommand("INSERT INTO public.\"Users\"(\r\n\t\"UserName\", \"FullName\", \"Role\", \"NumberPhone\", \"Status\")\r\n\tVALUES (@userName,@fullName, @role, @numberPhone, @status);", connection))
                {
                    command.Parameters.AddWithValue("userName", userName);
                    command.Parameters.AddWithValue("fullName", fullName);
                    command.Parameters.AddWithValue("role", role);
                    command.Parameters.AddWithValue("numberPhone", numberPhone);
                    command.Parameters.AddWithValue("status", status);

                    int rowsAffected = command.ExecuteNonQuery();
                }
            }
        }

        public static int QueryGetStatus(string userName)
        {            
            using (NpgsqlConnection connection = new NpgsqlConnection(connString))
            {
                connection.Open();

                using (NpgsqlCommand command = new NpgsqlCommand(@$"SELECT ""UserName"", coalesce(""Status"",0) as ""Status""	FROM public.""Users""	WHERE ""UserName"" = '{userName}';", connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            return (int)reader["Status"];                           
                        }
                    }
                }
            }
            return -1;
        }

        public static void QueryTruncate(string table)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connString))
            {
                connection.Open();

                using (NpgsqlCommand command = new NpgsqlCommand(@$"TRUNCATE public.""Services"" RESTART IDENTITY;", connection))
                {
                    int rowsAffected = command.ExecuteNonQuery();
                }
            }
        }

        public static void MigrateDatabase()
        {
            using (var connection = new NpgsqlConnection(connString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    DO
                    $$
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM information_schema.tables WHERE table_name = 'Services') THEN
                            CREATE TABLE IF NOT EXISTS public.""Services""
(
    ""Id"" integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    model_search character varying(1040000) COLLATE pg_catalog.""default"",
    model character varying(1040000) COLLATE pg_catalog.""default"",
    ""Text"" character varying(1040000) COLLATE pg_catalog.""default"",
    CONSTRAINT ""Services_pkey"" PRIMARY KEY (""Id"")
);
                        END IF;
                    END
                    $$";
                command.ExecuteNonQuery();

                command.CommandText = @"
                    DO
                    $$
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM information_schema.tables WHERE table_name = 'Users') THEN
                            CREATE TABLE IF NOT EXISTS public.""Users""
(
    ""EmpId"" integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    ""UserName"" character varying(500) COLLATE pg_catalog.""default"",
    ""FullName"" character varying(500) COLLATE pg_catalog.""default"",
    ""Role"" character varying(500) COLLATE pg_catalog.""default"",
    ""NumberPhone"" character varying(500) COLLATE pg_catalog.""default"",
    ""Status"" integer,
    CONSTRAINT ""Users_pkey"" PRIMARY KEY (""EmpId"")
);
                        END IF;
                    END
                    $$";
                command.ExecuteNonQuery();


                connection.Close();
            }
        }
    }

    public class DataSource
    {
        public string ModelSearch { get; set; }
        public string ModelName { get; set; }
        public string Services { get; set; }
    }

    public class UserData
    {
        public int Empid { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public string NumberPhone { get; set; }
        public int Status { get; set; }
    }


}