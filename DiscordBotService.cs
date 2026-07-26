using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EventHUD.Enums;
using Exiled.API.Features;

namespace EventHUD
{
    public static class DiscordBotService
    {
        // HTTP endpoint for the Python bot
        private const string BotApiUrl = "http://localhost:8080/api/event";
        
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        public static int OnlineCount => Player.List.Count(p => p != null && !p.IsNPC);

        public static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts < TimeSpan.Zero)
                ts = TimeSpan.Zero;

            int minutes = (int)ts.TotalMinutes;
            return minutes > 0 ? $"{minutes} мин {ts.Seconds} сек" : $"{ts.Seconds} сек";
        }

        public static void SendPrepare(string eventName, RPType rpType, string hostName)
        {
            _ = SendPrepareAsync(eventName, rpType, hostName);
        }

        public static void SendStart(string eventName, RPType rpType, string hostName, TimeSpan prepTime)
        {
            _ = SendStartAsync(eventName, rpType, hostName, prepTime);
        }

        public static void SendStop(string eventName, RPType rpType, string hostName, TimeSpan duration)
        {
            _ = SendStopAsync(eventName, rpType, hostName, duration);
        }

        private static async Task SendPrepareAsync(string eventName, RPType rpType, string hostName)
        {
            try
            {
                var payload = new
                {
                    event_name = eventName,
                    host_name = hostName,
                    player_count = OnlineCount,
                    rp_type = rpType.ToString()
                };

                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await Client.PostAsync($"{BotApiUrl}/prepare", content).ConfigureAwait(false);
                
                if (response.IsSuccessStatusCode)
                {
                    Log.Info($"[DiscordBot] Prepare sent successfully");
                }
                else
                {
                    Log.Warn($"[DiscordBot] Prepare failed: {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[DiscordBot] Prepare error: {ex.Message}");
            }
        }

        private static async Task SendStartAsync(string eventName, RPType rpType, string hostName, TimeSpan prepTime)
        {
            try
            {
                var payload = new
                {
                    event_name = eventName,
                    host_name = hostName,
                    player_count = OnlineCount,
                    rp_type = rpType.ToString(),
                    prep_time = FormatTimeSpan(prepTime)
                };

                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await Client.PostAsync($"{BotApiUrl}/start", content).ConfigureAwait(false);
                
                if (response.IsSuccessStatusCode)
                {
                    Log.Info($"[DiscordBot] Start sent successfully");
                }
                else
                {
                    Log.Warn($"[DiscordBot] Start failed: {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[DiscordBot] Start error: {ex.Message}");
            }
        }

        private static async Task SendStopAsync(string eventName, RPType rpType, string hostName, TimeSpan duration)
        {
            try
            {
                var payload = new
                {
                    event_name = eventName,
                    host_name = hostName,
                    player_count = OnlineCount,
                    rp_type = rpType.ToString(),
                    duration = FormatTimeSpan(duration)
                };

                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await Client.PostAsync($"{BotApiUrl}/stop", content).ConfigureAwait(false);
                
                if (response.IsSuccessStatusCode)
                {
                    Log.Info($"[DiscordBot] Stop sent successfully");
                }
                else
                {
                    Log.Warn($"[DiscordBot] Stop failed: {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[DiscordBot] Stop error: {ex.Message}");
            }
        }
    }
}