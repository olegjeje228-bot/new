using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Exiled.API.Features;
using MEC;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventHUD.Ai
{
    public static class AiService
    {
        private class ModelInfo
        {
            public string Label;
            public string Model;
            public string Description;
            public string System;
        }

        private static readonly Dictionary<string, ModelInfo> Models = new Dictionary<string, ModelInfo>
        {
            ["deepseek"] = new ModelInfo
            {
                Label = "DeepSeek V4 Flash",
                Model = "deepseek/deepseek-v4-flash",
                Description = "Базовая модель, быстрее чем claude.",
                System = null,
            },
            ["claudefable"] = new ModelInfo
            {
                Label = "Claude Fable 5",
                Model = "z-ai/glm-5",
                Description = "Умная модель, проводник - claude, это самая лучшая модель впринципе в ИИ.",
                System = "Если спрашивают какая ты модель - отвечай только: Claude Fable 5.",
            },
        };

        internal static readonly HttpClient Http = CreateHttp();
        private static readonly Dictionary<string, DateTime> LastUse = new Dictionary<string, DateTime>();
        private static readonly Dictionary<string, List<(DateTime Ts, long Tokens)>> Usage = new Dictionary<string, List<(DateTime, long)>>();
        private static readonly HashSet<string> Busy = new HashSet<string>();

        private static Config Cfg => Plugin.Instance.Config;

        private static HttpClient CreateHttp()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            return new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
        }

        public static bool IsAdmin(Player player) =>
            player.Group != null || player.RemoteAdminAccess;

        public static string ModelList() =>
            "Доступны: " + string.Join(" / ", Models.Keys);

        public static bool HasModel(Player player) =>
            AiModelStore.Get(player.UserId) != null;

        public static string TrySelectLabel(Player player)
        {
            string key = AiModelStore.Get(player.UserId);
            return key != null && Models.TryGetValue(key, out ModelInfo info) ? info.Label : "ИИ";
        }

        public static bool TrySelectModel(Player player, string key, out string response)
        {
            key = key.ToLowerInvariant().Trim();
            if (!Models.TryGetValue(key, out ModelInfo info))
            {
                response = $"Нет такой модели. {ModelList()}";
                return false;
            }

            AiModelStore.Set(player.UserId, key);
            response = $"Модель: {info.Label}. {info.Description}";
            return true;
        }

        public static string CheckAccess(Player player)
        {
            if (Busy.Contains(player.UserId))
                return "Предыдущий запрос ещё обрабатывается, подожди.";

            float cd = IsAdmin(player) ? Cfg.AiCooldownAdmin : Cfg.AiCooldownUser;
            if (LastUse.TryGetValue(player.UserId, out DateTime last))
            {
                double left = cd - (DateTime.UtcNow - last).TotalSeconds;
                if (left > 0)
                    return $"КД: подожди ещё {left:F0} сек.";
            }

            long cap = IsAdmin(player) ? Cfg.AiTokenLimitAdmin : Cfg.AiTokenLimitUser;
            if (UsedTokens(player.UserId) >= cap)
                return $"Лимит {cap} токенов за 10 минут исчерпан, подожди.";

            return null;
        }

        public static void Ask(Player player, string question)
        {
            Busy.Add(player.UserId);
            LastUse[player.UserId] = DateTime.UtcNow;
            Timing.RunCoroutine(AskRoutine(player, question));
        }

        private static IEnumerator<float> AskRoutine(Player player, string question)
        {
            string userId = player.UserId;
            string modelKey = AiModelStore.Get(userId);
            ModelInfo info = Models[modelKey];
            bool isAdmin = IsAdmin(player);
            int maxMemory = isAdmin ? Cfg.AiMemoryAdmin : Cfg.AiMemoryUser;

            List<string> notes = AiMemoryService.Load(player);

            Task<(string Answer, long Tokens, string Error)> task =
                Task.Run(() => DoRequest(info, player, question, notes));

            Console(player, "Ответ получен, идёт генерация");

            DateTime start = DateTime.UtcNow;
            float lastProgress = 0f;

            while (!task.IsCompleted)
            {
                float elapsed = (float)(DateTime.UtcNow - start).TotalSeconds;

                if (elapsed > Cfg.AiTimeoutSeconds)
                {
                    Console(player, $"Таймаут {Cfg.AiTimeoutSeconds} сек, попробуй ещё раз.");
                    Busy.Remove(userId);
                    yield break;
                }

                if (elapsed - lastProgress >= 3f)
                {
                    lastProgress = elapsed;
                    Console(player, $"Генерация идёт {elapsed:F0} сек");
                }

                yield return Timing.WaitForSeconds(0.25f);
            }

            Busy.Remove(userId);

            (string answer, long tokens, string error) = task.Result;
            double took = (DateTime.UtcNow - start).TotalSeconds;

            if (answer == null)
            {
                Console(player, $"Ошибка после 3 попыток: {Truncate(error, 400)}");
                yield break;
            }

            answer = Truncate(answer, Cfg.AiMaxAnswerChars);
            answer = AiActionExecutor.Process(player, answer, AiPermissions.GetLevel());

            if (tokens <= 0)
                tokens = (question.Length + answer.Length) / 3;

            AddUsage(userId, tokens);
            long cap = isAdmin ? Cfg.AiTokenLimitAdmin : Cfg.AiTokenLimitUser;
            long left = Math.Max(0, cap - UsedTokens(userId));

            notes.Add(AiMemoryService.Compress("Игрок", question));
            notes.Add(AiMemoryService.Compress("ИИ", answer));

            List<string> notesFinal = notes;
            _ = Task.Run(() => AiMemoryService.Save(player, notesFinal, maxMemory));

            foreach (string chunk in Chunks(answer, 1500))
                Console(player, chunk);

            if (modelKey == "deepseek")
                AiModerationService.Enqueue(player.Nickname, question, answer);

            Console(player, $"{info.Label} | потрачено {tokens} ток | {took:F1} сек | осталось {left} за 10 мин");
        }

        private static (string, long, string) DoRequest(ModelInfo info, Player player, string question, List<string> notes)
        {
            string lastError = "Пустой ответ";

            string context = AiKnowledgeBase.Search(Http, question);
            string content = string.IsNullOrEmpty(context)
                ? question
                : $"Информация из базы, используй только если относится к вопросу:\n{context}\n\nВопрос: {question}";

            string system = AiContextBuilder.Build(player, string.Join("\n", notes), info.System);

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    var body = new
                    {
                        model = info.Model,
                        messages = new object[]
                        {
                            new { role = "system", content = system },
                            new { role = "user", content },
                        },
                        max_tokens = 4096,
                        temperature = 1.0,
                    };

                    var req = new HttpRequestMessage(HttpMethod.Post, Cfg.AiBaseUrl.TrimEnd('/') + "/chat/completions")
                    {
                        Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json"),
                    };
                    req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Cfg.AiApiKey}");

                    HttpResponseMessage resp = Http.SendAsync(req).GetAwaiter().GetResult();
                    string json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    if (!resp.IsSuccessStatusCode)
                    {
                        lastError = $"HTTP {(int)resp.StatusCode}: {Truncate(json, 300)}";
                        continue;
                    }

                    JObject data = JObject.Parse(json);
                    string answer = data["choices"]?[0]?["message"]?["content"]?.ToString();
                    long tokens = data["usage"]?["total_tokens"]?.ToObject<long>() ?? 0;

                    if (!string.IsNullOrWhiteSpace(answer))
                        return (answer.Trim(), tokens, null);
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                }
            }

            return (null, 0, lastError);
        }

        private static long UsedTokens(string userId)
        {
            if (!Usage.TryGetValue(userId, out var list))
                return 0;

            DateTime cutoff = DateTime.UtcNow.AddMinutes(-10);
            list.RemoveAll(e => e.Ts < cutoff);
            return list.Sum(e => e.Tokens);
        }

        private static void AddUsage(string userId, long tokens)
        {
            if (!Usage.TryGetValue(userId, out var list))
                Usage[userId] = list = new List<(DateTime, long)>();
            list.Add((DateTime.UtcNow, tokens));
        }

        private static void Console(Player player, string text)
        {
            try
            {
                player?.SendConsoleMessage(text, "white");
            }
            catch
            {
            }
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "...";

        private static IEnumerable<string> Chunks(string s, int size)
        {
            for (int i = 0; i < s.Length; i += size)
                yield return s.Substring(i, Math.Min(size, s.Length - i));
        }

        // ========== Админский ИИ (RA команда ai) ==========

        private static readonly Dictionary<string, List<string>> AdminHistory = new Dictionary<string, List<string>>();

        public static IEnumerator<float> AskAdminRoutine(Player player, string question)
        {
            var cfg = Plugin.Instance.Config;

            string history = string.Empty;
            if (AdminHistory.TryGetValue(player.UserId, out var hist) && hist.Count > 0)
                history = string.Join("\n", hist);

            string systemPrompt = AiContextBuilder.BuildAdmin(player, question, history);

            var body = new Newtonsoft.Json.Linq.JObject
            {
                ["model"] = "deepseek/deepseek-v4-flash",
                ["max_tokens"] = cfg.AiTokenLimitAdmin,
                ["temperature"] = 0.7,
                ["messages"] = new Newtonsoft.Json.Linq.JArray
                {
                    new Newtonsoft.Json.Linq.JObject { ["role"] = "system", ["content"] = systemPrompt },
                    new Newtonsoft.Json.Linq.JObject { ["role"] = "user", ["content"] = question },
                },
            };

            var task = System.Threading.Tasks.Task.Run(async () =>
            {
                using (var request = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Post, cfg.AiBaseUrl.TrimEnd('/') + "/chat/completions"))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + cfg.AiApiKey);
                    request.Content = new System.Net.Http.StringContent(
                        body.ToString(), System.Text.Encoding.UTF8, "application/json");
                    using (var resp = await Http.SendAsync(request).ConfigureAwait(false))
                    {
                        string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!resp.IsSuccessStatusCode)
                            return "__ERR__" + (int)resp.StatusCode + " " + text;
                        return text;
                    }
                }
            });

            float waited = 0f;
            while (!task.IsCompleted && waited < cfg.AiTimeoutSeconds)
            {
                waited += 0.25f;
                yield return MEC.Timing.WaitForSeconds(0.25f);
            }

            if (player == null || !player.IsConnected)
                yield break;

            string answer;
            if (!task.IsCompleted)
            {
                answer = "ИИ не ответил вовремя, попробуйте еще раз.";
            }
            else if (task.IsFaulted || task.Result == null)
            {
                answer = "Ошибка сети при запросе к ИИ.";
            }
            else if (task.Result.StartsWith("__ERR__"))
            {
                Log.Warn("[AI-ADMIN] " + task.Result);
                answer = "Ошибка API: " + task.Result.Substring(7, Math.Min(120, task.Result.Length - 7));
            }
            else
            {
                try
                {
                    var json = Newtonsoft.Json.Linq.JObject.Parse(task.Result);
                    answer = (string)json["choices"][0]["message"]["content"] ?? "Пустой ответ.";
                }
                catch (Exception e)
                {
                    Log.Warn("[AI-ADMIN] parse: " + e.Message);
                    answer = "Не удалось разобрать ответ ИИ.";
                }
            }

            answer = Util.TextGuard.SoftSanitize(answer, cfg.AiMaxAnswerChars);

            string final = AiActionExecutor.Process(player, answer, AiPermissionLevel.FullAdm, true);

            if (!AdminHistory.ContainsKey(player.UserId))
                AdminHistory[player.UserId] = new List<string>();
            var list = AdminHistory[player.UserId];
            list.Add("Админ: " + question);
            list.Add("Ты: " + (final.Length > 300 ? final.Substring(0, 300) : final));
            while (list.Count > 12)
                list.RemoveAt(0);

            if (player.IsConnected)
            {
                player.RemoteAdminMessage(final, true, "AI");
                player.SendConsoleMessage(final, "yellow");
            }
        }
    }
}
