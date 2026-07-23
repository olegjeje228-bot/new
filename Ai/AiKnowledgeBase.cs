using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using Exiled.API.Features;

namespace EventHUD.Ai
{
    public static class AiKnowledgeBase
    {
        private const int CacheTtlSeconds = 60;
        private const int TopN = 3;
        private const int MinScore = 1;

        private static readonly HashSet<string> Stop = new HashSet<string>
        {
            "что", "кто", "как", "где", "когда", "почему", "такое", "это",
            "есть", "был", "мне", "ты", "вы", "мы", "он", "она"
        };

        private static List<(string Title, string Text)> _rows = new List<(string, string)>();
        private static DateTime _loadedAt = DateTime.MinValue;

        public static string Search(HttpClient client, string question)
        {
            List<(string Title, string Text)> rows = LoadRows(client);

            List<string> words = Regex.Matches(question.ToLowerInvariant(), @"\w+")
                .Cast<Match>()
                .Select(m => m.Value)
                .Where(w => w.Length > 2 && !Stop.Contains(w))
                .ToList();

            if (rows.Count == 0 || words.Count == 0)
                return string.Empty;

            var found = new List<(int Score, string Title, string Text)>();
            foreach ((string title, string text) in rows)
            {
                string titleL = title.ToLowerInvariant();
                string fullL = (title + " " + text).ToLowerInvariant();

                int score = 0;
                foreach (string word in words)
                {
                    if (titleL.Contains(word))
                        score += 3;
                    else if (fullL.Contains(word))
                        score += 1;
                }

                if (score >= MinScore)
                    found.Add((score, title, text));
            }

            return string.Join("\n", found
                .OrderByDescending(f => f.Score)
                .Take(TopN)
                .Select(f => $"{f.Title}: {f.Text}"));
        }

        private static List<(string, string)> LoadRows(HttpClient client)
        {
            if (_rows.Count > 0 && (DateTime.UtcNow - _loadedAt).TotalSeconds < CacheTtlSeconds)
                return _rows;

            try
            {
                string url = Plugin.Instance.Config.AiKnowledgeUrl;
                if (string.IsNullOrWhiteSpace(url))
                    return _rows;

                string raw = client.GetStringAsync(url).GetAwaiter().GetResult();
                var rows = new List<(string, string)>();

                foreach (string line in raw.Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0)
                        continue;

                    int sep = trimmed.IndexOf(':');
                    if (sep > 0)
                        rows.Add((trimmed.Substring(0, sep).Trim(), trimmed.Substring(sep + 1).Trim()));
                    else
                        rows.Add((trimmed, string.Empty));
                }

                _rows = rows;
                _loadedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Log.Warn($"[EventHUD.Ai] База знаний недоступна: {ex.Message}");
            }

            return _rows;
        }
    }
}
