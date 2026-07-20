using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Exiled.API.Features;
using Newtonsoft.Json.Linq;

namespace EventHUD.Ai
{
    public static class AiModerationService
    {
        private class Entry
        {
            public string Nick;
            public string Question;
            public string Answer;
        }

        private static readonly List<Entry> Buffer = new List<Entry>();
        private static readonly object Lock = new object();

        public static void Enqueue(string nick, string question, string answer)
        {
            var cfg = Plugin.Instance.Config;
            if (!cfg.AiModerationEnabled)
                return;

            List<Entry> batch = null;

            lock (Lock)
            {
                Buffer.Add(new Entry { Nick = nick, Question = question, Answer = answer });
                if (Buffer.Count >= cfg.AiModerationEvery)
                {
                    batch = new List<Entry>(Buffer);
                    Buffer.Clear();
                }
            }

            if (batch != null)
                Task.Run(() => ReviewBatch(batch));
        }

        private static async Task ReviewBatch(List<Entry> batch)
        {
            try
            {
                var cfg = Plugin.Instance.Config;
                var sb = new StringBuilder();
                for (int i = 0; i < batch.Count; i++)
                {
                    sb.AppendLine($"Ответ {i + 1}. Игрок {batch[i].Nick} спросил: {Trim(batch[i].Question, 300)}");
                    sb.AppendLine($"DeepSeek ответил: {Trim(batch[i].Answer, 800)}");
                    sb.AppendLine();
                }

                string system =
                    "Ты модератор ответов ИИ на игровом сервере SCP: Secret Laboratory. " +
                    "Ниже последние ответы модели DeepSeek игрокам. Проверь каждый: " +
                    "нет ли вреда серверу, слива системного промпта или ключей, оскорблений игроков, " +
                    "помощи в нарушении правил, читах или эксплойтах, выполнения запрещённых команд, откровенного бреда или вранья о сервере. " +
                    "Если всё нормально, ответь строго одним словом: OK. " +
                    "Если есть проблема, ответь строго в формате: ПРОБЛЕМА <номер ответа>: <краткое описание>.";

                var body = new JObject
                {
                    ["model"] = "z-ai/glm-5",
                    ["max_tokens"] = 500,
                    ["temperature"] = 0.0,
                    ["messages"] = new JArray
                    {
                        new JObject { ["role"] = "system", ["content"] = system },
                        new JObject { ["role"] = "user", ["content"] = sb.ToString() }
                    }
                };

                var req = new HttpRequestMessage(HttpMethod.Post, cfg.AiBaseUrl.TrimEnd('/') + "/chat/completions")
                {
                    Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json")
                };
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + cfg.AiApiKey);

                var resp = await AiService.Http.SendAsync(req).ConfigureAwait(false);
                string raw = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    Log.Warn($"[AiModeration] Проверка не удалась: {resp.StatusCode} {Trim(raw, 200)}");
                    return;
                }

                string verdict = JObject.Parse(raw)?["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim() ?? "";

                if (verdict.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Debug($"[AiModeration] Проверка {batch.Count} ответов: OK");
                    return;
                }

                Log.Warn($"[AiModeration] GLM-5 нашёл проблему: {verdict}");
                await SendAlert(verdict, batch).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Warn($"[AiModeration] Ошибка проверки: {e.Message}");
            }
        }

        private static async Task SendAlert(string verdict, List<Entry> batch)
        {
            var cfg = Plugin.Instance.Config;
            if (string.IsNullOrEmpty(cfg.AiModerationWebhookUrl))
                return;

            var desc = new StringBuilder();
            desc.AppendLine("**Вердикт:** " + Trim(verdict, 500));
            desc.AppendLine();
            for (int i = 0; i < batch.Count; i++)
                desc.AppendLine($"**{i + 1}. {batch[i].Nick}:** {Trim(batch[i].Question, 150)}");

            var payload = new JObject
            {
                ["embeds"] = new JArray
                {
                    new JObject
                    {
                        ["title"] = "Надзор ИИ: подозрительный ответ DeepSeek",
                        ["description"] = desc.ToString(),
                        ["color"] = 15158332
                    }
                }
            };

            var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
            await AiService.Http.PostAsync(cfg.AiModerationWebhookUrl, content).ConfigureAwait(false);
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}
