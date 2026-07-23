using System;
using System.Collections.Generic;
using System.Text;
using MEC;

namespace EventHUD.Logging
{
    /// <summary>Очередь логов для одного webhook: копит строки, шлёт пачкой раз в 5 сек.</summary>
    public class WebhookLogChannel
    {
        private readonly List<string> _queue = new List<string>();
        private readonly object _lock = new object();
        private readonly Func<string> _getUrl;
        private readonly string _name;
        private CoroutineHandle _flusher;

        public WebhookLogChannel(string name, Func<string> getUrl)
        {
            _name = name;
            _getUrl = getUrl;
        }

        public void Start()
        {
            if (!_flusher.IsRunning)
                _flusher = Timing.RunCoroutine(FlushLoop());
        }

        public void Stop()
        {
            if (_flusher.IsRunning)
                Timing.KillCoroutines(_flusher);

            lock (_lock)
                _queue.Clear();
        }

        public void Add(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            line = line.Replace("\n", " ").Replace("\r", " ").Replace("`", "'");
            if (line.Length > 300)
                line = line.Substring(0, 300) + "...";

            lock (_lock)
            {
                _queue.Add(line);
                if (_queue.Count > 500)
                    _queue.RemoveAt(0);
            }
        }

        private IEnumerator<float> FlushLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(5f);

                List<string> lines;
                lock (_lock)
                {
                    if (_queue.Count == 0)
                        continue;

                    lines = new List<string>(_queue);
                    _queue.Clear();
                }

                string url = _getUrl?.Invoke();
                if (string.IsNullOrWhiteSpace(url))
                {
                    DebugFileLog.Write($"[{_name}] ПРОПУСК: webhook URL пустой, потеряно строк: {lines.Count}");
                    continue;
                }

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
    }
}