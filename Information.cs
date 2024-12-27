using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Final_Project
{
    internal class Information
    {
        private static readonly HttpClient HttpClient = new();

        // Метод для поиска тем на Википедии
        public async Task<List<string>> SearchTopicsAsync(string query)
        {
            var url = $"https://ru.wikipedia.org/w/api.php?action=query&format=json&list=search&srsearch={Uri.EscapeDataString(query)}&utf8=true";

            try
            {
                var response = await HttpClient.GetStringAsync(url);
                var json = JObject.Parse(response);
 
                var searchResults = json["query"]?["search"]?.Children().ToList();

                if (searchResults == null || !searchResults.Any())
                {
                    return new List<string>();
                }

                // Возвращаем только первые 5 результатов
                return searchResults.Take(5).Select(result => result["title"]?.ToString() ?? "").ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при поиске тем: {ex.Message}");
                return new List<string>();
            }
        }


        // Метод для получения структуры статьи на Википедии
        public async Task<List<string>> GetArticleSectionsAsync(string title)
        {
            var url = $"https://ru.wikipedia.org/w/api.php?action=parse&page={Uri.EscapeDataString(title)}&prop=sections&format=json&utf8=true";

            try
            {
                var response = await HttpClient.GetStringAsync(url);
                Console.WriteLine(response);
                var json = JObject.Parse(response);

                var sections = json["parse"]?["sections"]?.Children().ToList();

                if (sections == null || !sections.Any())
                {
                    return new List<string>();
                }

                // Сортируем по индексу (поле "index")
                var sortedSections = sections
                    .OrderBy(section => (int?)section["index"] ?? int.MaxValue) // сортировка по числовому значению индекса
                    .Select(section => section["line"]?.ToString() ?? "")
                    .ToList();

                return sortedSections;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении разделов статьи: {ex.Message}");
                return new List<string>();
            }
        }



        // Метод для получения текста определённого раздела статьи
        public async Task<List<string>> GetSectionContentAsync(string title, int sectionIndex)
        {
            var url = $"https://ru.wikipedia.org/w/api.php?action=parse&page={Uri.EscapeDataString(title)}&section={sectionIndex}&prop=text&format=json&utf8=true";
            var resultChunks = new List<string>();

            try
            {
                var response = await HttpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                // Получаем HTML содержимое секции
                var contentHtml = json["parse"]?["text"]?["*"]?.ToString();

                if (string.IsNullOrWhiteSpace(contentHtml))
                {
                    resultChunks.Add("Раздел не содержит текста.");
                    return resultChunks;
                }

                // Удаляем HTML-теги и очищаем текст
                var plainText = StripHtml(contentHtml);
                plainText = RemoveUnnecessaryElements(plainText);
                plainText = CleanUpText(plainText);

                // Разбиваем текст на части по 4096 символов
                for (int i = 0; i < plainText.Length; i += 4096)
                {
                    var chunk = plainText.Substring(i, Math.Min(4096, plainText.Length - i));
                    resultChunks.Add(chunk);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении текста раздела: {ex.Message}");
                resultChunks.Add("Ошибка при получении текста раздела.");
            }

            return resultChunks;
        }

        // Функция для удаления HTML-тегов
        private string StripHtml(string input)
        {
            try
            {
                // Загружаем HTML в HtmlAgilityPack
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(input);

                // Удаляем ненужные теги, например скрипты и стили
                var nodesToRemove = doc.DocumentNode.SelectNodes("//script|//style|//noscript");
                if (nodesToRemove != null)
                {
                    foreach (var node in nodesToRemove)
                    {
                        node.Remove();
                    }
                }

                // Удаляем ненужные элементы, например, с определёнными классами ("ts-Родственные_проекты")
                var unwantedDivs = doc.DocumentNode.SelectNodes("//div[contains(@class, 'ts-Родственные_проекты')]");
                if (unwantedDivs != null)
                {
                    foreach (var node in unwantedDivs)
                    {
                        node.Remove();
                    }
                }

                // Преобразуем очищенный HTML в текст
                return doc.DocumentNode.InnerText;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в StripHtml: {ex.Message}");
                return input; // В случае ошибки возвращаем исходный текст
            }
        }


        // Функция для удаления лишних элементов (например, ссылки или блоки с "родственными проектами")
        private string RemoveUnnecessaryElements(string input)
        {
            // Удаляем ненужные блоки с "родственными проектами"
            input = System.Text.RegularExpressions.Regex.Replace(input, @"<div[^>]*class=""ts-Родственные_проекты""[^>]*>.*?</div>", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
            // Убираем теги с ссылками
            input = System.Text.RegularExpressions.Regex.Replace(input, @"<a[^>]*>.*?</a>", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
            // Убираем прочие метки
            input = System.Text.RegularExpressions.Regex.Replace(input, @"<!--.*?-->", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);

            return input;
        }

        // Функция для удаления дополнительных ненужных символов (например, "[править | править код]")
        private string CleanUpText(string input)
        {
            // Убираем элементы типа "[править | править код]"
            input = System.Text.RegularExpressions.Regex.Replace(input, @"\[.*?\]", string.Empty);
            // Убираем прочие лишние пробелы и символы
            input = System.Text.RegularExpressions.Regex.Replace(input, @"\s+", " ").Trim();

            return input;
        }



        

    }
}
