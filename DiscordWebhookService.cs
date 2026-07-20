using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using EventHUD.Enums;
using EventHUD.Extensions;
using EventHUD.Models;
using Exiled.API.Features;
using Newtonsoft.Json;

namespace EventHUD
{
    public static class DiscordWebhookService
    {
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            return new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        private static string WebhookUrl => Plugin.Instance?.Config?.DiscordWebhookUrl;

        public static int OnlineCount => Player.List.Count(p => !p.IsNPC);

        public static string GetRolePing(RPType type) => type switch
        {
            RPType.NONRP    => "<@&1525359474724835440>",
            RPType.FUNRP    => "<@&1525359474724835440>",
            RPType.LIGHTRP  => "<@&1525360019674103818>",
            RPType.MEDIUMRP => "<@&1525360019674103818>",
            RPType.HARDRP   => "<@&1525360019674103818>",
            RPType.FULLRP   => "<@&1525360171541594183>",
            _               => string.Empty
        };

        public static string FormatTimeSpan(TimeSpan t) =>
            t.TotalHours >= 1   ? $"{(int)t.TotalHours} ч {t.Minutes} мин"
            : t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes} мин"
            : $"{Math.Max(0, t.Seconds)} сек";

        // ---------- Анонсы-embed ----------

        public static void SendPrepareEmbed(EventSession s)
        {
            long prepStart = ToUnix(s.StartedAt);

            Post(new
            {
                content = GetRolePing(s.RpType),
                allowed_mentions = new { parse = new[] { "roles", "users" } },
                embeds = new object[]
                {
                    new
                    {
                        title = s.EventName?.ToUpperInvariant(),
                        description = HostLine("Проводит", s),
                        color = ColorInt(s.RpType),
                        fields = new object[]
                        {
                            Field("Уровень РП", s.RpType.GetShortName()),
                            Field("Подготовка", $"началась <t:{prepStart}:R>"),
                            Field("Онлайн", OnlineCount.ToString()),
                        },
                        footer = new { text = "DLB Events" },
                        timestamp = DateTime.UtcNow.ToString("o"),
                    }
                }
            });
        }

        public static void SendStartEmbed(EventSession s, TimeSpan prepTime)
        {
            Post(new
            {
                allowed_mentions = new { parse = new[] { "users" } },
                embeds = new object[]
                {
                    new
                    {
                        title = s.EventName?.ToUpperInvariant(),
                        description = HostLine("Начался! Проводит", s),
                        color = ColorInt(s.RpType),
                        fields = new object[]
                        {
                            Field("Уровень РП", s.RpType.GetShortName()),
                            Field("Подготовка заняла", FormatTimeSpan(prepTime)),
                            Field("Онлайн", OnlineCount.ToString()),
                        },
                        footer = new { text = "DLB Events" },
                        timestamp = DateTime.UtcNow.ToString("o"),
                    }
                }
            });
        }

        public static void SendStopEmbed(EventSession s, TimeSpan duration)
        {
            Post(new
            {
                allowed_mentions = new { parse = new[] { "users" } },
                embeds = new object[]
                {
                    new
                    {
                        title = s.EventName?.ToUpperInvariant(),
                        description = HostLine("Завершён. Проводил", s),
                        color = ColorInt(s.RpType),
                        fields = new object[]
                        {
                            Field("Длительность", FormatTimeSpan(duration)),
                            Field("Онлайн", OnlineCount.ToString()),
                        },
                        footer = new { text = "DLB Events" },
                        timestamp = DateTime.UtcNow.ToString("o"),
                    }
                }
            });
        }

        public static void Send(string content) =>
            Post(new { content, allowed_mentions = new { parse = new[] { "roles" } } });

        public static void SendCallAlert(string message)
        {
            string url = Plugin.Instance?.Config?.CallWebhookUrl;
            if (string.IsNullOrEmpty(url))
                return;

            var payload = new Newtonsoft.Json.Linq.JObject { ["content"] = message };
            var content = new System.Net.Http.StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json");
            _ = Client.PostAsync(url, content);
        }

        // ---------- Внутрянка ----------

        private static object Field(string name, string value) =>
            new { name, value, inline = true };

        private static string HostLine(string prefix, EventSession s)
        {
            string mention = HostMention(s.HostUserId);
            return mention == null
                ? $"{prefix} **{s.HostNickname}**"
                : $"{prefix} **{s.HostNickname}** ({mention})";
        }

        private static string HostMention(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return null;

            string steamId = userId.Replace("@steam", string.Empty).Replace("@northwood", string.Empty);
            var map = Plugin.Instance?.Config?.HostDiscordIds;
            return map != null && map.TryGetValue(steamId, out ulong id) ? $"<@{id}>" : null;
        }

        private static int ColorInt(RPType type) =>
            Convert.ToInt32(type.GetColor().TrimStart('#'), 16);

        private static long ToUnix(DateTime utc) =>
            new DateTimeOffset(utc == default ? DateTime.UtcNow : utc, TimeSpan.Zero).ToUnixTimeSeconds();

        private static void Post(object payload)
        {
            string url = WebhookUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                Log.Warn("[EventHUD] Discord webhook URL не задан в конфиге.");
                return;
            }

            _ = PostAsync(url, payload);
        }

        private static async Task PostAsync(string url, object payload)
        {
            try
            {
                string json = JsonConvert.SerializeObject(payload);
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    HttpResponseMessage resp = await Client.PostAsync(url, content).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                    {
                        string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        Log.Warn($"[EventHUD] Webhook HTTP {(int)resp.StatusCode}: {body}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[EventHUD] Webhook error: {ex.Message}");
            }
        }
    }
}
