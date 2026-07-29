using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Exiled.API.Features;
using MEC;

namespace EventHUD.AntiDdos
{
    public class TrafficReportService
    {
        private static readonly CultureInfo Ru = new CultureInfo("ru-RU");

        private CoroutineHandle _handle;
        private long _lastRxBytes = -1;

        // поминутные дельты за последние 48 часов
        private readonly List<(DateTime time, long bytes)> _minutes = new List<(DateTime, long)>();
        // история 30-минутных окон (для среднего), в MB
        private readonly List<double> _history30 = new List<double>();
        private double _prevAvg30;

        private static string LogPath => Path.Combine(Paths.Configs, "EventHUD", "antiddos.txt");
        private static string TrafficFilePath => Path.Combine(Paths.Configs, "EventHUD-Traffic.txt");

        public void Start()
        {
            LoadHistory();
            _handle = Timing.RunCoroutine(Loop());
        }

        public void Stop() => Timing.KillCoroutines(_handle);

        private IEnumerator<float> Loop()
        {
            int minuteCounter = 0;
            while (true)
            {
                yield return Timing.WaitForSeconds(60f);
                try
                {
                    SampleMinute();
                    minuteCounter++;
                    if (minuteCounter >= 30)
                    {
                        minuteCounter = 0;
                        ReportAuto();
                    }
                }
                catch (Exception e) { WriteLog("ERROR loop: " + e.Message); }
            }
        }

        // ==== Сбор трафика ====

        private static long ReadRxBytes()
        {
            // /proc/net/dev: суммируем received bytes по всем интерфейсам кроме lo
            long total = 0;
            foreach (string line in File.ReadAllLines("/proc/net/dev").Skip(2))
            {
                string[] parts = line.Split(':');
                if (parts.Length != 2) continue;
                if (parts[0].Trim() == "lo") continue;

                string[] cols = parts[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (cols.Length > 0 && long.TryParse(cols[0], out long rx))
                    total += rx;
            }
            return total;
        }

        private void SampleMinute()
        {
            long rx = ReadRxBytes();
            if (_lastRxBytes >= 0)
            {
                long delta = rx - _lastRxBytes;
                if (delta < 0) delta = 0; // счётчик сбросился
                _minutes.Add((DateTime.UtcNow, delta));
                if (_minutes.Count > 2880) _minutes.RemoveAt(0);
            }
            _lastRxBytes = rx;
        }

        private double WindowMb(int minutes, int offsetMinutes = 0)
        {
            DateTime to = DateTime.UtcNow.AddMinutes(-offsetMinutes);
            DateTime from = to.AddMinutes(-minutes);
            long sum = 0;
            foreach (var m in _minutes)
                if (m.time > from && m.time <= to)
                    sum += m.bytes;
            return sum / 1024.0 / 1024.0;
        }

        // ==== Форматирование ====

        private static string FmtSize(double mb) =>
            mb >= 1024 ? (mb / 1024).ToString("0.##", Ru) + " GB" : mb.ToString("0", Ru) + " MB";

        private static string FmtPct(double pct)
        {
            if (Math.Abs(pct) < 5) return "0%"; // 3-4% не считается
            return (pct >= 0 ? "+" : "-") + Math.Abs(pct).ToString("0.##", Ru) + "%";
        }

        private static string Emoji(double valueMb, double avgMb)
        {
            if (avgMb <= 0) return "\U0001F7E9";   // зелёный: нет статистики - считаем нормой
            double r = valueMb / avgMb;
            if (r >= 100) return "\u2B1B";          // чёрный: крайне много
            if (r >= 15) return "\U0001F7E5";      // красный: очень много
            if (r >= 2.5) return "\U0001F7E8";     // жёлтый: много
            if (r <= 0.5) return "\u2B1C";          // белый: маловато
            return "\U0001F7E9";                    // зелёный: норма
        }

        private string BuildBlock(string title, int minutes)
        {
            double cur = WindowMb(minutes);
            double prev = WindowMb(minutes, minutes);
            double avg30 = _history30.Count > 0 ? _history30.Average() : 0;
            double avg = avg30 * (minutes / 30.0); // норма для этого окна

            double pctPrev = prev > 0 ? (cur - prev) / prev * 100.0 : 0;
            double pctAvg = avg > 0 ? (cur - avg) / avg * 100.0 : 0;

            double avgDelta = (avg30 - _prevAvg30) * (minutes / 30.0);
            string avgDeltaStr = Math.Abs(avgDelta) < 0.5
                ? ""
                : $" [ {(avgDelta >= 0 ? "+" : "-")}{FmtSize(Math.Abs(avgDelta))} ]";

            return
                $"DLB EVENTS STATUS [ {title} ]\n" +
                $"Получено: {FmtSize(cur)} трафика {Emoji(cur, avg)}\n" +
                $"{FmtPct(pctPrev)} от прошлого раза | {FmtPct(pctAvg)} от среднего\n" +
                $"Средний трафик: {FmtSize(avg)}{avgDeltaStr}";
        }

        // ==== Отчёты ====

        /// Автоотчёт раз в 30 минут.
        private void ReportAuto()
        {
            string block = BuildBlock("30 мин", 30);

            // обновляем историю среднего ПОСЛЕ построения отчёта
            _prevAvg30 = _history30.Count > 0 ? _history30.Average() : 0;
            double cur = WindowMb(30);
            _history30.Add(cur);
            if (_history30.Count > 336) _history30.RemoveAt(0); // храним неделю
            WriteLog("WINDOW30 " + cur.ToString("0.##", CultureInfo.InvariantCulture) + " MB");

            WriteLog(block.Replace("\n", " | "));
            SendToBot(block);
        }

        /// Ручной отчёт по /serverstatus: 30 мин + 2 часа + 24 часа.
        public void SendManualReport()
        {
            string text =
                BuildBlock("30 мин", 30) + "\n\n" +
                BuildBlock("2 часа", 120) + "\n\n" +
                BuildBlock("24 часа", 1440);

            WriteLog("MANUAL /serverstatus запрошен");
            SendToBot(text);
        }

        // Кладём отчёт в файл, бот подхватит и запостит в канал
        private static void SendToBot(string text)
        {
            try
            {
                File.AppendAllText(TrafficFilePath, text + "\n-----\n");
            }
            catch (Exception e) { Log.Warn("[Traffic] " + e.Message); }
        }

        // ==== antiddos.txt ====

        private static void WriteLog(string msg)
        {
            try
            {
                string dir = Path.GetDirectoryName(LogPath);
                Directory.CreateDirectory(dir);
                File.AppendAllText(LogPath, $"[{DateTime.Now:dd.MM HH:mm:ss}] {msg}{Environment.NewLine}");
            }
            catch { }
        }

        private void LoadHistory()
        {
            try
            {
                if (!File.Exists(LogPath)) return;
                string[] allLines = File.ReadAllLines(LogPath);
                for (int idx = allLines.Length - 1; idx >= 0 && idx > allLines.Length - 1500; idx--)
                {
                    string line = allLines[idx];
                    int i = line.IndexOf("WINDOW30 ", StringComparison.Ordinal);
                    if (i < 0) continue;
                    string num = line.Substring(i + 9).Replace(" MB", "").Trim();
                    if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double mb))
                        _history30.Insert(0, mb);
                    if (_history30.Count >= 336) break;
                }
                WriteLog($"START: загружено {_history30.Count} окон истории");
            }
            catch { }
        }
    }
}