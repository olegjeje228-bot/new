using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;

namespace EventHUD.AntiDdos
{
    /// <summary>
    /// Анти-DDoS уровня приложения (L7 join-флуд).
    /// НЕ защищает от объёмного DDoS — это делает только хост/сеть (GRE, scrubbing).
    ///
    /// 4 слоя реакции с гистерезисом и авто-деэскалацией:
    ///   Слой 1 — per-IP rate-limit
    ///   Слой 2 — жёсткий лимит + отклонение новых подключений
    ///   Слой 3 — только известные (вернувшиеся) игроки
    ///   Слой 4 — LOCKDOWN: все подключения отклоняются, только консоль
    ///
    /// Управление ТОЛЬКО через конфиг хоста.
    /// </summary>
    public class AntiDdosService
    {
        private readonly Config _config;
        private CoroutineHandle _monitorHandle;

        private static readonly HttpClient _http = new HttpClient();
        private readonly object _sync = new object();

        // Времена всех подключений (для conn/sec) — ограниченный размер
        private readonly LinkedList<DateTime> _connTimes = new LinkedList<DateTime>();
        // Подключения по IP
        private readonly Dictionary<string, List<DateTime>> _perIp = new Dictionary<string, List<DateTime>>();
        // Временные баны IP → время окончания
        private readonly Dictionary<string, DateTime> _tempBans = new Dictionary<string, DateTime>();
        // Известные (недавно игравшие) UserId — пропускаются на Слое 3
        // Не очищается при перезапуске раунда — игроки прошлых раундов тоже "известные"
        private readonly HashSet<string> _knownUsers = new HashSet<string>();
        // Максимальный размер _knownUsers (защита от переполнения)
        private const int MaxKnownUsers = 5000;

        private DateTime? _calmSince;

        // Кеш conn/sec для мгновенной эскалации в OnPreAuthenticating
        private int _cachedConnPerSec;
        private DateTime _cachedConnTime = DateTime.MinValue;

        public AntiDdosLevel CurrentLevel { get; private set; } = AntiDdosLevel.Normal;

        public AntiDdosService(Config config)
        {
            _config = config;
        }

        public void Start() => _monitorHandle = Timing.RunCoroutine(MonitorLoop());
        public void Stop() => Timing.KillCoroutines(_monitorHandle);

        // ── Игрок успешно вошёл — запоминаем как «известного» ──
        public void OnVerified(VerifiedEventArgs ev)
        {
            if (ev.Player == null || string.IsNullOrEmpty(ev.Player.UserId)) return;
            lock (_sync)
            {
                if (_knownUsers.Count >= MaxKnownUsers)
                    _knownUsers.Clear(); // Защита от переполнения
                _knownUsers.Add(ev.Player.UserId);
            }
        }

        // ── Перехват подключения ДО авторизации ──
        public void OnPreAuthenticating(PreAuthenticatingEventArgs ev)
        {
            if (!_config.AntiDdosEnabled) return;

            string ip = ev.IpAddress ?? "unknown";

            // Whitelist — всегда пропускаем (стафф/доверенные)
            if (_config.AntiDdosWhitelistIps != null && _config.AntiDdosWhitelistIps.Contains(ip))
                return;

            DateTime now = DateTime.UtcNow;
            int perIpCount;
            bool banned;
            int connPerSec;

            lock (_sync)
            {
                // Добавляем подключение в общий список
                _connTimes.AddLast(now);
                // Ограничиваем размер списка — храним максимум 5 секунд
                var cutoff = now.AddSeconds(-5.0);
                while (_connTimes.Count > 0 && _connTimes.First.Value < cutoff)
                    _connTimes.RemoveFirst();

                // Подсчёт per-IP
                if (!_perIp.TryGetValue(ip, out var list))
                {
                    list = new List<DateTime>();
                    _perIp[ip] = list;
                }
                list.Add(now);
                float win = _config.AntiDdosPerIpWindowSeconds;
                list.RemoveAll(t => (now - t).TotalSeconds > win);
                perIpCount = list.Count;

                // Очистка пустых IP-записей (защита от переполнения памяти)
                if (list.Count == 0)
                    _perIp.Remove(ip);

                banned = _tempBans.TryGetValue(ip, out var expiry) && expiry > now;

                // Мгновенный подсчёт conn/sec для эскалации в реальном времени
                connPerSec = _connTimes.Count(t => (now - t).TotalSeconds <= 1.0);
                _cachedConnPerSec = connPerSec;
                _cachedConnTime = now;
            }

            // Забаненный IP
            if (banned)
            {
                ev.Reject("Временно заблокировано (анти-флуд). Попробуйте позже.", true);
                return;
            }

            // Мгновенная эскалация: если conn/sec уже превышает Layer 4 — сразу lockdown
            // Не ждём мониторинга (он раз в секунду), а реагируем на каждое подключение
            var level = CurrentLevel;
            if (connPerSec >= _config.AntiDdosLayer4ConnPerSec)
            {
                if (level < AntiDdosLevel.Layer4)
                    SetLevel(AntiDdosLevel.Layer4, connPerSec, Server.SmoothTps);
                level = AntiDdosLevel.Layer4;
            }
            else if (connPerSec >= _config.AntiDdosLayer3ConnPerSec && level < AntiDdosLevel.Layer3)
            {
                SetLevel(AntiDdosLevel.Layer3, connPerSec, Server.SmoothTps);
                level = AntiDdosLevel.Layer3;
            }
            else if (connPerSec >= _config.AntiDdosLayer2ConnPerSec && level < AntiDdosLevel.Layer2)
            {
                SetLevel(AntiDdosLevel.Layer2, connPerSec, Server.SmoothTps);
                level = AntiDdosLevel.Layer2;
            }

            // Слой 4 — полный lockdown
            if (level == AntiDdosLevel.Layer4)
            {
                ev.Reject("Сервер под DDoS-атакой. Вход временно закрыт.", true);
                return;
            }

            // Слой 3 — только известные игроки
            if (level == AntiDdosLevel.Layer3 && _config.AntiDdosLayer3KnownOnly)
            {
                bool known;
                lock (_sync) { known = ev.UserId != null && _knownUsers.Contains(ev.UserId); }
                if (!known)
                {
                    ev.Reject("Сервер перегружен. Вход открыт только вернувшимся игрокам.", true);
                    return;
                }
            }

            // Пер-IP лимит (Слой 1+)
            if (level >= AntiDdosLevel.Layer1 && perIpCount > _config.AntiDdosMaxConnPerIp)
            {
                lock (_sync) { _tempBans[ip] = now.AddSeconds(_config.AntiDdosIpTempBanSeconds); }
                ev.Reject("Слишком много подключений с вашего IP.", true);
                return;
            }

            // Слой 2 — отклоняем НОВЫХ (неизвестных) игроков, известных — пропускаем
            // Раньше был только Delay(2), что не спасало: сервер всё равно обрабатывал
            // каждое подключение, и бэклог переполнялся.
            if (level == AntiDdosLevel.Layer2)
            {
                bool known;
                lock (_sync) { known = ev.UserId != null && _knownUsers.Contains(ev.UserId); }
                if (!known)
                {
                    ev.Reject("Сервер перегружен. Попробуйте зайти позже.", true);
                    return;
                }
                // Известных — пропускаем, но с задержкой
                ev.Delay(2, false);
            }
        }

        // ── Мониторинг ──
        private IEnumerator<float> MonitorLoop()
        {
            while (true)
            {
                float interval = _config.AntiDdosMonitorInterval > 0f ? _config.AntiDdosMonitorInterval : 1f;
                yield return Timing.WaitForSeconds(interval);

                if (!_config.AntiDdosEnabled)
                {
                    if (CurrentLevel != AntiDdosLevel.Normal)
                        SetLevel(AntiDdosLevel.Normal, 0, 0);
                    continue;
                }

                try { Evaluate(); }
                catch (Exception ex) { Log.Debug($"[AntiDdos] Evaluate: {ex.Message}"); }
            }
        }

        private void Evaluate()
        {
            DateTime now = DateTime.UtcNow;
            int connPerSec;

            lock (_sync)
            {
                // Очистка старых записей
                var cutoff = now.AddSeconds(-5.0);
                while (_connTimes.Count > 0 && _connTimes.First.Value < cutoff)
                    _connTimes.RemoveFirst();

                connPerSec = _connTimes.Count(t => (now - t).TotalSeconds <= 1.0);

                // Очистка истёкших банов
                var expired = _tempBans.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList();
                foreach (var k in expired) _tempBans.Remove(k);

                // Очистка пустых IP-записей
                var emptyIps = _perIp.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key).ToList();
                foreach (var k in emptyIps) _perIp.Remove(k);
            }

            double tps = Server.SmoothTps;

            // Целевой слой по подключениям/сек.
            // TPS используется ТОЛЬКО для добивания до более высокого слоя,
            // но НЕ для самостоятельной эскалации — TPS может падать из-за
            // тяжёлых ивентов, медицины, гранат, и это НЕ DDoS.
            AntiDdosLevel target;
            if (connPerSec >= _config.AntiDdosLayer4ConnPerSec)
                target = AntiDdosLevel.Layer4;
            else if (connPerSec >= _config.AntiDdosLayer3ConnPerSec)
                target = AntiDdosLevel.Layer3;
            else if (connPerSec >= _config.AntiDdosLayer2ConnPerSec)
                target = AntiDdosLevel.Layer2;
            else if (connPerSec >= _config.AntiDdosLayer1ConnPerSec)
                target = AntiDdosLevel.Layer1;
            else if (connPerSec >= _config.AntiDdosLayer3ConnPerSec / 2 && tps <= _config.AntiDdosLayer3TpsThreshold)
                // Много подключений + TPS критически низкая → добиваем до Слоя 3
                target = AntiDdosLevel.Layer3;
            else if (connPerSec >= _config.AntiDdosLayer4ConnPerSec / 2 && tps <= _config.AntiDdosLayer4TpsThreshold)
                // Очень много подключений + TPS умирает → добиваем до Слоя 4
                target = AntiDdosLevel.Layer4;
            else
                target = AntiDdosLevel.Normal;

            if (target > CurrentLevel)
            {
                // Эскалация — мгновенно
                _calmSince = null;
                SetLevel(target, connPerSec, tps);
            }
            else if (target < CurrentLevel)
            {
                // Деэскалация — по одному слою после периода спокойствия
                if (_calmSince == null) _calmSince = now;
                if ((now - _calmSince.Value).TotalSeconds >= _config.AntiDdosDeescalateSeconds)
                {
                    SetLevel(CurrentLevel - 1, connPerSec, tps);
                    _calmSince = null;
                }
            }
            else
            {
                _calmSince = null;
            }
        }

        private void SetLevel(AntiDdosLevel level, int connPerSec, double tps)
        {
            if (level == CurrentLevel) return;
            var prev = CurrentLevel;
            CurrentLevel = level;
            Alert(prev, level, connPerSec, tps);
        }

        private void Alert(AntiDdosLevel prev, AntiDdosLevel level, int connPerSec, double tps)
        {
            string msg = level switch
            {
                AntiDdosLevel.Normal => $"[AntiDdos] Атака отражена, возврат в норму (conn/s={connPerSec}, tps={tps:0}).",
                AntiDdosLevel.Layer1 => $"[AntiDdos] СЛОЙ 1 (лёгкий флуд): включён per-IP лимит. conn/s={connPerSec}.",
                AntiDdosLevel.Layer2 => $"[AntiDdos] СЛОЙ 2 (сильный флуд): отклонение новых, задержка известных. conn/s={connPerSec}.",
                AntiDdosLevel.Layer3 => $"[AntiDdos] СЛОЙ 3 (перегрузка): вход только вернувшимся игрокам. conn/s={connPerSec}, tps={tps:0}.",
                AntiDdosLevel.Layer4 => $"[AntiDdos] СЛОЙ 4 (LOCKDOWN): DDOS АТАКА — вход закрыт, только консоль! conn/s={connPerSec}, tps={tps:0}.",
                _ => $"[AntiDdos] Уровень: {level}."
            };

            if (level >= AntiDdosLevel.Layer3) Log.Error(msg);
            else if (level >= AntiDdosLevel.Layer1) Log.Warn(msg);
            else Log.Info(msg);

            SendWebhook(level, msg);
        }

        private void SendWebhook(AntiDdosLevel level, string msg)
        {
            string url = _config.AntiDdosWebhookUrl;
            if (string.IsNullOrWhiteSpace(url)) return;

            // Fire-and-forget — сеть не должна ронять сервер
            Task.Run(async () =>
            {
                try
                {
                    string prefix = level == AntiDdosLevel.Layer4 ? "@here " : string.Empty;
                    string json = "{\"content\":\"" + Escape(prefix + msg) + "\"}";
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                        await _http.PostAsync(url, content).ConfigureAwait(false);
                }
                catch { }
            });
        }

        private static string Escape(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");

        // ── Сброс на новом раунде ──
        // ВНИМАНИЕ: _knownUsers НЕ очищается — игроки прошлых раундов остаются "известными".
        // Это критично для Слоя 3: если очистить, то после рестарта раунда ни один игрок
        // не пройдёт фильтр "только известные", и легитимные игроки не смогут войти.
        public void Reset()
        {
            lock (_sync)
            {
                _connTimes.Clear();
                _perIp.Clear();
                _tempBans.Clear();
                // _knownUsers НЕ очищаем!
            }
            CurrentLevel = AntiDdosLevel.Normal;
            _calmSince = null;
        }
    }
}