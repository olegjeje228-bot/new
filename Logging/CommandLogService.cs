using System.Collections.Generic;
using System.Text;
using MEC;

namespace EventHUD.Logging
{
    /// <summary>Копит логи команд и раз в 5 секунд шлёт одним сообщением через webhook.</summary>
    public static class CommandLogService
    {
        private static readonly List<string> Queue = new List<string>();
        private static readonly object Lock = new object();
        private static CoroutineHandle _flusher;

        public static void Start()
        {
            if (!_flusher.IsRunning)
            {
                _flusher = Timing.RunCoroutine(FlushLoop());
                DebugFileLog.Write("[CommandLog] Отправщик запущен.");
            }
        }

        public static void Stop()
        {
            if (_flusher.IsRunning)
                Timing.KillCoroutines(_flusher);

            lock (Lock)
                Queue.Clear();
        }

        public static void Log(string steamId, string command, string response)
        {
            string line = $"{Sanitize(steamId, 40)} {Sanitize(command, 120)} -> {Sanitize(response, 200)}";

            lock (Lock)
            {
                Queue.Add(line);
                if (Queue.Count > 300)
                    Queue.RemoveAt(0);
            }
        }

        private static IEnumerator<float> FlushLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(5f);

                List<string> lines;
                lock (Lock)
                {
                    if (Queue.Count == 0)
                        continue;

                    lines = new List<string>(Queue);
                    Queue.Clear();
                }

                string url = Plugin.Instance?.Config?.CommandLogWebhookUrl;
                if (string.IsNullOrWhiteSpace(url))
                {
                    DebugFileLog.Write($"[CommandLog] ПРОПУСК: webhook URL пустой в конфиге, потеряно строк: {lines.Count}");
                    continue;
                }

                DebugFileLog.Write($"[CommandLog] Отправка {lines.Count} строк на webhook...");

                var sb = new StringBuilder();
                foreach (string line in lines)
                {
                    if (sb.Length + line.Length + 2 > 1900)
                    {
                        DiscordWebhookService.SendTo(url, $"```{sb}```");
                        sb.Clear();
                    }
                    sb.AppendLine(line);
                }

                if (sb.Length > 0)
                    DiscordWebhookService.SendTo(url, $"```{sb}```");
            }
        }

        private static string Sanitize(string s, int max)
        {
            if (string.IsNullOrEmpty(s))
                return "-";
            s = s.Replace("\n", " ").Replace("\r", " ").Replace("`", "'");
            return s.Length > max ? s.Substring(0, max) + "..." : s;
        }
    }
}
