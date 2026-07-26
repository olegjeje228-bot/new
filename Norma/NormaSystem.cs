using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Exiled.API.Features;
using MEC;

namespace EventHUD.Norma
{
    public class NormaSystem
    {
        public static NormaSystem Instance { get; private set; }

        private readonly Dictionary<string, string> _roles = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _nicks = new Dictionary<string, string>();
        private readonly Dictionary<string, long> _open = new Dictionary<string, long>();

        private readonly object _fileLock = new object();
        private static readonly object DbgLock = new object();
        private CoroutineHandle _loop;

        private static readonly Regex RaLine =
            new Regex(@"^\s*-\s*([^\s:]+@[^\s:]+)\s*:\s*(\S+)\s*$", RegexOptions.Compiled);

        private string DataFile => Path.Combine(Paths.Configs, "EventHUD", "norma.txt");

        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // ================= ЛОГ =================
        public static void Dbg(string msg)
        {
            try
            {
                string dir = Path.Combine(Paths.Configs, "EventHUD");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "debugnorma.txt");
                string line = "[" + DateTime.Now.ToString("dd.MM HH:mm:ss") + "] " + msg;

                lock (DbgLock)
                    File.AppendAllText(file, line + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        // ================= ЖИЗНЕННЫЙ ЦИКЛ =================

        public void Enable()
        {
            Instance = this;

            Dbg("==================================================");
            Dbg("ENABLE start");

            try
            {
                Dbg("Paths.Configs = " + Paths.Configs);
                Dbg("DataFile      = " + DataFile);
                Dbg("RA path       = " + Plugin.Instance.Config.NormaRemoteAdminPath);
                Dbg("Webhook set   = " + (!string.IsNullOrEmpty(Plugin.Instance.Config.NormaWebhook)));
                Dbg("NormaHours ролей в конфиге = " + Plugin.Instance.Config.NormaHours.Count);

                EnsureFile();
                LoadNicks();
                ReloadAdmins();
                PruneOld();

                Exiled.Events.Handlers.Player.Verified += OnVerified;
                Exiled.Events.Handlers.Player.Left += OnLeft;

                foreach (Player p in Player.List)
                    TryOpen(p);

                _loop = Timing.RunCoroutine(Loop());

                Dbg("ENABLE ok. Админов в списке: " + _roles.Count + ", онлайн сейчас: " + _open.Count);
                Log.Info("[Норма] Запущено. Админов: " + _roles.Count + ". Лог: debugnorma.txt");
            }
            catch (Exception e)
            {
                Dbg("ENABLE FAIL: " + e);
                Log.Error("[Норма] Ошибка запуска: " + e);
            }
        }

        public void Disable()
        {
            Dbg("DISABLE");

            Timing.KillCoroutines(_loop);

            Exiled.Events.Handlers.Player.Verified -= OnVerified;
            Exiled.Events.Handlers.Player.Left -= OnLeft;

            FlushSessions(true);
            Instance = null;
        }

        private IEnumerator<float> Loop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(300f);

                try
                {
                    FlushSessions(false);
                    ReloadAdmins();
                    CheckAutoReport();
                }
                catch (Exception e)
                {
                    Dbg("LOOP FAIL: " + e.Message);
                }
            }
        }

        // ================= АДМИНЫ =================

        public void ReloadAdmins()
        {
            try
            {
                string path = Plugin.Instance.Config.NormaRemoteAdminPath;

                if (!File.Exists(path))
                {
                    Dbg("ADMINS FAIL: файл не найден -> " + path);
                    return;
                }

                Dictionary<string, double> hours = Plugin.Instance.Config.NormaHours;
                Dictionary<string, string> found = new Dictionary<string, string>();
                int lines = 0;

                foreach (string raw in File.ReadAllLines(path))
                {
                    lines++;
                    Match m = RaLine.Match(raw);
                    if (!m.Success)
                        continue;

                    string userId = m.Groups[1].Value.Trim();
                    string role = m.Groups[2].Value.Trim();

                    if (found.TryGetValue(userId, out string old))
                    {
                        double oldNorm = hours.TryGetValue(old, out double a) ? a : -1;
                        double newNorm = hours.TryGetValue(role, out double b) ? b : -1;
                        if (newNorm <= oldNorm)
                            continue;
                    }

                    found[userId] = role;
                }

                Dbg("ADMINS: прочитано строк " + lines + ", распознано админов " + found.Count);

                if (found.Count == 0)
                {
                    Dbg("ADMINS FAIL: ни одной строки вида '- 7656...@steam: role'");
                    return;
                }

                _roles.Clear();
                foreach (KeyValuePair<string, string> kv in found)
                    _roles[kv.Key] = kv.Value;
            }
            catch (Exception e)
            {
                Dbg("ADMINS FAIL: " + e.Message);
            }
        }

        private bool IsTracked(string userId) =>
            !string.IsNullOrEmpty(userId) && _roles.ContainsKey(userId);

        // ================= ВРЕМЯ =================

        private void OnVerified(Exiled.Events.EventArgs.Player.VerifiedEventArgs ev) => TryOpen(ev.Player);

        private void OnLeft(Exiled.Events.EventArgs.Player.LeftEventArgs ev) => TryClose(ev.Player);

        private void TryOpen(Player p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.UserId))
                    return;

                if (!IsTracked(p.UserId))
                {
                    Dbg("JOIN не админ (нет в файле админов): " + p.UserId);
                    return;
                }

                _open[p.UserId] = Now;
                RememberNick(p.UserId, p.Nickname);
                Dbg("JOIN админ: " + p.Nickname + " (" + p.UserId + ", роль " + _roles[p.UserId] + ")");
            }
            catch (Exception e) { Dbg("JOIN FAIL: " + e.Message); }
        }

        private void TryClose(Player p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.UserId))
                    return;

                if (!_open.TryGetValue(p.UserId, out long start))
                    return;

                _open.Remove(p.UserId);
                WriteSession(p.UserId, start, Now);
                RememberNick(p.UserId, p.Nickname);
                Dbg("LEFT админ: " + p.Nickname + ", сессия " + (Now - start) + " сек");
            }
            catch (Exception e) { Dbg("LEFT FAIL: " + e.Message); }
        }

        private void FlushSessions(bool closeAll)
        {
            long now = Now;
            List<string> ids = _open.Keys.ToList();

            foreach (string id in ids)
            {
                long start = _open[id];
                if (now > start)
                    WriteSession(id, start, now);

                if (closeAll)
                    _open.Remove(id);
                else
                    _open[id] = now;
            }

            if (ids.Count > 0)
                Dbg("FLUSH сессий: " + ids.Count);
        }

        // ================= ИВЕНТЫ =================

        // Вызывается из StartCommand.Execute() при успешном "ev start":
        //     EventHUD.Norma.NormaSystem.Instance?.AddEvent(player);
        public void AddEvent(Player p)
        {
            if (p == null || string.IsNullOrEmpty(p.UserId))
                return;

            AddEvent(p.UserId, p.Nickname);
        }

        public void AddEvent(string userId, string nick)
        {
            try
            {
                if (!IsTracked(userId))
                {
                    Dbg("EVENT пропущен: " + userId + " нет в файле админов");
                    return;
                }

                RememberNick(userId, nick);
                Append("EVENT|" + userId + "|" + Now);
                Dbg("EVENT засчитан: " + (nick ?? userId));
                Log.Info("[Норма] Засчитан ивент: " + (nick ?? userId));
            }
            catch (Exception e) { Dbg("EVENT FAIL: " + e.Message); }
        }

        // ================= ФАЙЛ ДАННЫХ =================

        private void EnsureFile()
        {
            try
            {
                lock (_fileLock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(DataFile));
                    if (!File.Exists(DataFile))
                    {
                        File.WriteAllText(DataFile, "LASTREPORT|" + Now + Environment.NewLine);
                        Dbg("Создан norma.txt");
                    }
                }
            }
            catch (Exception e)
            {
                Dbg("НЕ СОЗДАТЬ norma.txt: " + e.Message);
            }
        }

        private void Append(string line)
        {
            try
            {
                lock (_fileLock)
                    File.AppendAllText(DataFile, line + Environment.NewLine);
            }
            catch (Exception e) { Dbg("APPEND FAIL: " + e.Message); }
        }

        private void WriteSession(string userId, long start, long end)
        {
            if (end <= start)
                return;

            Append("SESSION|" + userId + "|" + start + "|" + end);
        }

        private void RememberNick(string userId, string nick)
        {
            if (string.IsNullOrEmpty(nick))
                return;

            if (_nicks.TryGetValue(userId, out string old) && old == nick)
                return;

            _nicks[userId] = nick;
            Append("NICK|" + userId + "|" + nick.Replace("|", ""));
        }

        private void LoadNicks()
        {
            try
            {
                if (!File.Exists(DataFile))
                    return;

                foreach (string line in File.ReadAllLines(DataFile))
                {
                    if (!line.StartsWith("NICK|"))
                        continue;

                    string[] p = line.Split('|');
                    if (p.Length >= 3)
                        _nicks[p[1]] = p[2];
                }
            }
            catch { }
        }

        private void PruneOld()
        {
            try
            {
                if (!File.Exists(DataFile))
                    return;

                long cutoff = Now - (30L * 86400L);
                List<string> keep = new List<string>();
                bool changed = false;

                foreach (string line in File.ReadAllLines(DataFile))
                {
                    string[] p = line.Split('|');

                    if (line.StartsWith("SESSION|") && p.Length >= 4
                        && long.TryParse(p[3], out long end) && end < cutoff)
                    {
                        changed = true;
                        continue;
                    }

                    if (line.StartsWith("EVENT|") && p.Length >= 3
                        && long.TryParse(p[2], out long at) && at < cutoff)
                    {
                        changed = true;
                        continue;
                    }

                    keep.Add(line);
                }

                if (changed)
                    lock (_fileLock)
                        File.WriteAllLines(DataFile, keep);
            }
            catch { }
        }

        private long ReadLastReport()
        {
            try
            {
                long last = 0;
                foreach (string line in File.ReadAllLines(DataFile))
                    if (line.StartsWith("LASTREPORT|"))
                    {
                        string[] p = line.Split('|');
                        if (p.Length >= 2 && long.TryParse(p[1], out long v) && v > last)
                            last = v;
                    }

                return last;
            }
            catch { return 0; }
        }

        // ================= ПОДСЧЁТ =================

        private void Collect(long from, long to,
            out Dictionary<string, long> seconds,
            out Dictionary<string, int> events)
        {
            seconds = new Dictionary<string, long>();
            events = new Dictionary<string, int>();

            try
            {
                foreach (string line in File.ReadAllLines(DataFile))
                {
                    string[] p = line.Split('|');

                    if (line.StartsWith("SESSION|") && p.Length >= 4
                        && long.TryParse(p[2], out long s) && long.TryParse(p[3], out long e))
                    {
                        long overlap = Math.Min(e, to) - Math.Max(s, from);
                        if (overlap > 0)
                            seconds[p[1]] = (seconds.TryGetValue(p[1], out long cur) ? cur : 0) + overlap;
                    }
                    else if (line.StartsWith("EVENT|") && p.Length >= 3
                        && long.TryParse(p[2], out long at))
                    {
                        if (at >= from && at <= to)
                            events[p[1]] = (events.TryGetValue(p[1], out int c) ? c : 0) + 1;
                    }
                }
            }
            catch (Exception e) { Dbg("COLLECT FAIL: " + e.Message); }

            foreach (KeyValuePair<string, long> kv in _open)
            {
                long overlap = Math.Min(to, Now) - Math.Max(kv.Value, from);
                if (overlap > 0)
                    seconds[kv.Key] = (seconds.TryGetValue(kv.Key, out long cur) ? cur : 0) + overlap;
            }
        }

        // ================= ОТЧЁТ =================

        private void CheckAutoReport()
        {
            double periodDays = Plugin.Instance.Config.NormaAutoDays;
            if (periodDays <= 0)
                return;

            long last = ReadLastReport();
            if (last == 0)
            {
                Append("LASTREPORT|" + Now);
                return;
            }

            long left = (long)(periodDays * 86400) - (Now - last);
            if (left > 0)
            {
                Dbg("Автоотчёт через " + (left / 3600) + " часов");
                return;
            }

            Dbg("Автоотчёт: время пришло");
            SendReport(periodDays, true);
            Append("LASTREPORT|" + Now);
        }

        public void SendReport(double days, bool auto)
        {
            Dbg("---- SENDREPORT вызван, days=" + days + ", auto=" + auto + " ----");

            try
            {
                if (days <= 0)
                    days = Plugin.Instance.Config.NormaAutoDays;

                long to = Now;
                long from = to - (long)Math.Round(days * 86400.0);

                ReloadAdmins();
                Collect(from, to, out Dictionary<string, long> secs, out Dictionary<string, int> evs);

                Dbg("Данных: сессий у " + secs.Count + " человек, ивентов у " + evs.Count);

                Dictionary<string, double> normHours = Plugin.Instance.Config.NormaHours;
                Dictionary<string, int> normEvents = Plugin.Instance.Config.NormaEvents;
                double baseDays = Plugin.Instance.Config.NormaAutoDays;
                bool scale = Plugin.Instance.Config.NormaScaleByPeriod;
                double k = (scale && baseDays > 0) ? days / baseDays : 1.0;

                List<string> bad = new List<string>();
                List<string> good = new List<string>();
                int skipped = 0;

                foreach (KeyValuePair<string, string> kv in _roles.OrderBy(x => Nick(x.Key), StringComparer.OrdinalIgnoreCase))
                {
                    string userId = kv.Key;
                    string role = kv.Value;

                    if (!normHours.TryGetValue(role, out double hNorm))
                    {
                        skipped++;
                        Dbg("SKIP " + Nick(userId) + ": роли '" + role + "' нет в norma_hours");
                        continue;
                    }

                    int eNorm = normEvents.TryGetValue(role, out int en) ? en : 0;

                    double hNormScaled = hNorm * k;
                    double eNormScaled = eNorm * k;
                    int eNormShown = (int)Math.Ceiling(eNormScaled - 0.0001);
                    if (eNormShown < 0) eNormShown = 0;

                    long sec = secs.TryGetValue(userId, out long s) ? s : 0;
                    int ev = evs.TryGetValue(userId, out int c) ? c : 0;

                    bool pass = sec >= (long)(hNormScaled * 3600.0) && ev >= eNormShown;

                    string line = string.Format(
                        "{0} {1} ({2}) -- время: {3} | Норма: {4}ч | Ивенты: {5}/{6}",
                        pass ? "\u2705" : "\u274C",
                        Nick(userId), role, Dhm(sec),
                        hNormScaled.ToString("0.##", CultureInfo.InvariantCulture),
                        ev, eNormShown);

                    if (pass) good.Add(line);
                    else bad.Add(line);
                }

                Dbg("В отчёте: ❌ " + bad.Count + ", ✅ " + good.Count + ", пропущено без нормы " + skipped);

                StringBuilder sb = new StringBuilder();
                sb.Append("**НОРМА АДМИНОВ [ ").Append(HumanPeriod(days * 86400.0)).Append(" ]**");
                if (!auto)
                    sb.Append("  (запрос вручную)");
                sb.AppendLine();
                sb.AppendLine();

                if (bad.Count == 0 && good.Count == 0)
                    sb.AppendLine("Нет админов с настроенной нормой (проверь имена ролей в конфиге).");

                foreach (string l in bad) sb.AppendLine(l);
                foreach (string l in good) sb.AppendLine(l);

                sb.AppendLine();
                sb.Append("Не сдали: ").Append(bad.Count).Append(" | Сдали: ").Append(good.Count);

                string text = sb.ToString();
                Dbg("Текст готов, длина " + text.Length + " символов");

                SendWebhook(text);
            }
            catch (Exception e)
            {
                Dbg("SENDREPORT FAIL: " + e);
                Log.Error("[Норма] Ошибка отчёта: " + e);
            }
        }

        private string Nick(string userId) =>
            _nicks.TryGetValue(userId, out string n) && !string.IsNullOrEmpty(n) ? n : userId;

        private static string Dhm(long seconds)
        {
            long d = seconds / 86400; seconds %= 86400;
            long h = seconds / 3600; seconds %= 3600;
            long m = seconds / 60;
            return d + "д " + h + "ч " + m + "м";
        }

        private static string Plural(long n, string one, string few, string many)
        {
            long n100 = n % 100;
            if (n100 >= 11 && n100 <= 14)
                return many;

            switch (n % 10)
            {
                case 1: return one;
                case 2:
                case 3:
                case 4: return few;
                default: return many;
            }
        }

        public static string HumanPeriod(double totalSeconds)
        {
            long s = (long)Math.Round(totalSeconds);
            if (s <= 0)
                return "0 секунд";

            long d = s / 86400; s %= 86400;
            long h = s / 3600; s %= 3600;
            long m = s / 60;
            long sec = s % 60;

            List<string> parts = new List<string>();
            if (d > 0) parts.Add(d + " " + Plural(d, "день", "дня", "дней"));
            if (h > 0) parts.Add(h + " " + Plural(h, "час", "часа", "часов"));
            if (m > 0) parts.Add(m + " " + Plural(m, "минута", "минуты", "минут"));
            if (sec > 0) parts.Add(sec + " " + Plural(sec, "секунда", "секунды", "секунд"));

            return string.Join(" ", parts);
        }

        // ================= ВЕБХУК =================

        public static void SendWebhook(string text)
        {
            string url = Plugin.Instance.Config.NormaWebhook;

            // Санируем URL: обрезаем пробелы, убираем скобки/кавычки,
            // ищем первое https:// на случай, если YAML добавил мусор
            if (!string.IsNullOrEmpty(url))
            {
                url = url.Trim().Trim('<', '>', '"', '\'', '\uFEFF', '\u200B');
                int i = url.IndexOf("https://", StringComparison.OrdinalIgnoreCase);
                if (i > 0) url = url.Substring(i);
            }

            Dbg("WEBHOOK URL = [" + url + "]");

            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Dbg("WEBHOOK FAIL: URL некорректный");
                FallbackToTraffic(text);
                return;
            }

            if (!url.StartsWith("https://discord.com/api/webhooks/"))
                Dbg("WEBHOOK ПРЕДУПРЕЖДЕНИЕ: URL выглядит странно");

            List<string> chunks = new List<string>();
            StringBuilder cur = new StringBuilder();

            foreach (string line in text.Split('\n'))
            {
                if (cur.Length + line.Length + 1 > 1900)
                {
                    chunks.Add(cur.ToString());
                    cur.Clear();
                }
                cur.Append(line).Append('\n');
            }

            if (cur.Length > 0)
                chunks.Add(cur.ToString());

            Dbg("WEBHOOK: отправляю частей " + chunks.Count);

            new Thread(() =>
            {
                int index = 0;

                foreach (string chunk in chunks)
                {
                    index++;

                    try
                    {
                        string json = "{\"content\":\"" + Escape(chunk) + "\",\"allowed_mentions\":{\"parse\":[]}}";
                        byte[] body = Encoding.UTF8.GetBytes(json);

                        ServicePointManager.SecurityProtocol =
                            SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                        ServicePointManager.ServerCertificateValidationCallback =
                            (a, b, c, d) => true;

                        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                        req.Method = "POST";
                        req.ContentType = "application/json";
                        req.UserAgent = "EventHUD-Norma";
                        req.ContentLength = body.Length;
                        req.Timeout = 15000;

                        using (Stream st = req.GetRequestStream())
                            st.Write(body, 0, body.Length);

                        using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                            Dbg("WEBHOOK OK часть " + index + ", код " + (int)resp.StatusCode);

                        Thread.Sleep(700);
                    }
                    catch (WebException we)
                    {
                        string detail = string.Empty;

                        try
                        {
                            if (we.Response != null)
                                using (StreamReader sr = new StreamReader(we.Response.GetResponseStream()))
                                    detail = sr.ReadToEnd();
                        }
                        catch { }

                        Dbg("WEBHOOK FAIL часть " + index + ": " + we.Message + " | ответ: " + detail);
                        FallbackToTraffic(chunk);
                    }
                    catch (Exception e)
                    {
                        Dbg("WEBHOOK FAIL часть " + index + ": " + e.Message);
                        FallbackToTraffic(chunk);
                    }
                }
            })
            { IsBackground = true }.Start();
        }

        private static void FallbackToTraffic(string text)
        {
            try
            {
                string file = Path.Combine(Paths.Configs, "EventHUD-Traffic.txt");
                File.AppendAllText(file, text + Environment.NewLine + "-----" + Environment.NewLine, Encoding.UTF8);
                Dbg("FALLBACK: отчёт записан в EventHUD-Traffic.txt (придёт через бота)");
            }
            catch (Exception e)
            {
                Dbg("FALLBACK FAIL: " + e.Message);
            }
        }

        private static string Escape(string s) => s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", string.Empty)
            .Replace("\n", "\\n")
            .Replace("\t", " ");
    }
}