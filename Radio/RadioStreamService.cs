using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EventHUD.Audio;
using Exiled.API.Features;
using MEC;
using UnityEngine;

namespace EventHUD.Radio
{
    public static class RadioStreamService
    {
        private static readonly float[] VolumeSteps = { 0f, 20f, 35f, 50f, 70f, 100f };

        private static readonly List<string> Tracks = new List<string>();
        private static readonly Dictionary<string, string> Aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> Blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "elbroke", "tripwire", "elevat", "elevator"
        };

        private static Type _audioPlayerType;
        private static Type _speakerType;

        private static void InitTypes()
        {
            if (_audioPlayerType != null) return;

            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!a.FullName.StartsWith("AudioPlayer")) continue;

                _audioPlayerType = a.GetType("AudioPlayer");
                _speakerType = a.GetType("Speaker");
                break;
            }
        }

        public static string Folder
        {
            get
            {
                string baseDir = Path.Combine(Paths.Configs, "EventHUD");
                return Path.Combine(baseDir, Plugin.Instance?.Config.RadioFmAudioFolder ?? "Audio/Radio");
            }
        }

        public static IReadOnlyList<string> TrackList => Tracks;

        public static void Init(string audioRoot)
        {
            ReloadTracks();
        }

        public static void ReloadTracks()
        {
            Tracks.Clear();
            Aliases.Clear();

            string folder = Folder;
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                FileLog.Write("[Radio] sozdana papka: " + folder);
                return;
            }

            string[] files;
            try { files = Directory.GetFiles(folder, "*.ogg", SearchOption.TopDirectoryOnly); }
            catch { return; }

            foreach (string path in files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (IsBlocked(name))
                {
                    FileLog.Write("[Radio] skip " + name);
                    continue;
                }

                string alias = "radio_" + name.ToLowerInvariant();
                try
                {
                    var audioClipStorage = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .FirstOrDefault(t => t.Name == "AudioClipStorage");

                    if (audioClipStorage != null)
                    {
                        var loadClip = audioClipStorage.GetMethod("LoadClip", new[] { typeof(string), typeof(string) });
                        loadClip?.Invoke(null, new object[] { path, alias });
                    }

                    Tracks.Add(name);
                    Aliases[name] = alias;
                    FileLog.Write($"[Radio] loaded file={Path.GetFileName(path)} track={name} alias={alias}");
                }
                catch (Exception ex)
                {
                    FileLog.Write($"[Radio] fail load {name}: {ex.Message}");
                }
            }

            FileLog.Write($"[Radio] tracks loaded: {Tracks.Count}");
            RadioDebugLog.Write($"ReloadTracks: папка={folder}, загружено {Tracks.Count}: [{string.Join(", ", Tracks)}]");
        }

        public static bool IsBlocked(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            string n = name.Trim();
            if (Blocked.Contains(n)) return true;
            string low = n.ToLowerInvariant();
            return low.Contains("elbroke") || low.Contains("elevat") || low.Contains("elevator") || low.Contains("tripwire");
        }

        public static string PickRandom()
        {
            if (Tracks.Count == 0) return null;
            return Tracks[UnityEngine.Random.Range(0, Tracks.Count)];
        }

        public static string ResolveName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            string raw = input.Trim();
            if (raw.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring(0, raw.Length - 4);
            if (IsBlocked(raw)) return null;

            if (Tracks.Contains(raw, StringComparer.OrdinalIgnoreCase))
                return Tracks.First(t => string.Equals(t, raw, StringComparison.OrdinalIgnoreCase));

            foreach (string t in Tracks)
            {
                if (t.IndexOf(raw, StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            }
            return null;
        }

        private static string GenPlayerName(RadioUnit unit)
        {
            return "RadioFM_" + unit.Number + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static bool IsDeadSafe(object ap)
        {
            if (ap == null) return true;
            try
            {
                var prop = _audioPlayerType?.GetProperty("IsDead");
                if (prop == null) return false;
                return (bool)prop.GetValue(ap);
            }
            catch { return true; }
        }

        private static void SafeDestroy(object ap)
        {
            if (ap == null) return;

            try
            {
                var destroy = ap.GetType().GetMethod("Destroy", Type.EmptyTypes);
                if (destroy != null)
                {
                    destroy.Invoke(ap, null);
                    return;
                }
            }
            catch (Exception ex) { RadioDebugLog.WriteEx("SafeDestroy: Destroy()", ex); }

            // Запасной путь: AudioPlayer — это MonoBehaviour, сносим GameObject средствами Unity
            try
            {
                if (ap is UnityEngine.Component c && c != null)
                    UnityEngine.Object.Destroy(c.gameObject);
                else if (ap is UnityEngine.Object uo && uo != null)
                    UnityEngine.Object.Destroy(uo);
            }
            catch (Exception ex) { RadioDebugLog.WriteEx("SafeDestroy: Unity Destroy", ex); }
        }

        public static bool Play(RadioUnit unit, string trackName, float maxDistance)
        {
            InitTypes();
            RadioDebugLog.Write($"Play: AudioPlayerType={( _audioPlayerType != null ? _audioPlayerType.Assembly.GetName().Name : "NULL — AudioPlayerApi не установлен/не загружен!")}");

            if (unit == null || string.IsNullOrEmpty(trackName))
            {
                RadioDebugLog.Write("Play: unit или trackName пустой.");
                return false;
            }

            string resolved = ResolveName(trackName);
            if (resolved == null || !Aliases.TryGetValue(resolved, out string alias))
            {
                RadioDebugLog.Write($"Play: НЕ НАЙДЕН трек/alias для '{trackName}'. resolved={resolved ?? "null"}, aliases: [{string.Join(", ", Aliases.Keys)}]");
                FileLog.Write($"[Radio] net treka/alias: {trackName}");
                return false;
            }
            RadioDebugLog.Write($"Play: resolved='{resolved}', alias='{alias}'");

            Stop(unit);

            float dist = Mathf.Max(1f, maxDistance);
            Vector3 pos = unit.Position;
            float apiVol = Mathf.Clamp01(VolumeOf(unit.Volume) / 100f);
            string playerName = GenPlayerName(unit);

            try
            {
                if (_audioPlayerType == null)
                {
                    FileLog.Write("[Radio] AudioPlayerType null");
                    return false;
                }

                var createOrGet = _audioPlayerType.GetMethods()
                    .FirstOrDefault(m => m.Name == "CreateOrGet");

                if (createOrGet == null)
                {
                    RadioDebugLog.Write("Play: CreateOrGet не найден в AudioPlayer!");
                    FileLog.Write("[Radio] CreateOrGet not found");
                    return false;
                }

                var pars = createOrGet.GetParameters();
                object[] args = new object[pars.Length];
                args[0] = playerName;
                for (int i = 1; i < pars.Length; i++)
                {
                    Type pt = pars[i].ParameterType;
                    if (pt == typeof(bool))
                        args[i] = pars[i].Name == "sendSoundGlobally";
                    else if (pt == typeof(byte))
                        args[i] = (byte)255;
                    else
                        args[i] = null;
                }

                object ap = createOrGet.Invoke(null, args);
                RadioDebugLog.Write($"Play: CreateOrGet -> {(ap == null ? "NULL" : IsDeadSafe(ap) ? "DEAD" : "ok")}, name={playerName}");

                if (ap == null || IsDeadSafe(ap))
                {
                    SafeDestroy(ap);
                    FileLog.Write("[Radio] CreateOrGet dead, retrying...");
                    playerName = GenPlayerName(unit);
                    args[0] = playerName;
                    ap = createOrGet.Invoke(null, args);
                }

                if (ap == null || IsDeadSafe(ap))
                {
                    RadioDebugLog.Write("Play: CreateOrGet мёртв дважды — AudioPlayer не создаётся.");
                    FileLog.Write("[Radio] CreateOrGet dead twice");
                    return false;
                }

                var t = ap.GetType();
                t.GetProperty("SendSoundGlobally")?.SetValue(ap, true);
                t.GetProperty("DestroyWhenAllClipsPlayed")?.SetValue(ap, false);

                try { t.GetMethod("RemoveAllClips")?.Invoke(ap, null); } catch { }

                // ── Speaker ──
                object sp = null;

                // 1) 6-параметровая перегрузка (как в SoundService — рабочая)
                var addSpeaker6 = t.GetMethod("AddSpeaker", new[]
                {
                    typeof(string), typeof(Vector3), typeof(float),
                    typeof(bool), typeof(float), typeof(float)
                });

                if (addSpeaker6 != null)
                {
                    sp = addSpeaker6.Invoke(ap, new object[] { "Main", pos, apiVol, true, 2f, dist });
                    RadioDebugLog.Write($"Play: AddSpeaker(6) -> {(sp != null ? "ok" : "NULL")}");
                }
                else
                {
                    // 2) запасной вариант: 5 параметров, позицию ставим отдельно
                    var addSpeaker5 = t.GetMethod("AddSpeaker", new[]
                    {
                        typeof(string), typeof(float), typeof(bool), typeof(float), typeof(float)
                    });

                    if (addSpeaker5 != null)
                    {
                        sp = addSpeaker5.Invoke(ap, new object[] { "Main", apiVol, true, 2f, dist });
                        RadioDebugLog.Write($"Play: AddSpeaker(5) -> {(sp != null ? "ok" : "NULL")}");
                    }
                    else
                    {
                        // ни одной знакомой перегрузки — выпишем в лог все, какие есть
                        foreach (var m in t.GetMethods().Where(m => m.Name == "AddSpeaker"))
                            RadioDebugLog.Write("Play: доступная перегрузка AddSpeaker(" +
                                string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name)) + ")");
                    }
                }

                if (sp == null)
                {
                    RadioDebugLog.Write("Play: ДИНАМИК НЕ СОЗДАН — звука не будет. Прерываю.");
                    SafeDestroy(ap);
                    return false;
                }

                // позиция динамика (на случай 5-параметровой перегрузки)
                var setSpeakerPos = t.GetMethod("SetSpeakerPosition", new[] { typeof(string), typeof(Vector3) });
                setSpeakerPos?.Invoke(ap, new object[] { "Main", pos });

                // AddClip — громкость 1.0 (громкость уже задана на спикере)
                var addClip = t.GetMethod("AddClip", new[]
                {
                    typeof(string), typeof(float), typeof(bool), typeof(bool)
                });

                object playback = null;
                if (addClip != null)
                    playback = addClip.Invoke(ap, new object[] { alias, 1f, true, false });
                RadioDebugLog.Write($"Play: AddClip {(addClip == null ? "НЕ НАЙДЕН" : "ok")}, playback={(playback != null ? "ok" : "NULL — клип не добавился (не загружен в AudioClipStorage?)")}");

                unit.AudioHandle = ap;
                unit.CurrentTrack = resolved;
                unit.PlayerName = playerName;
                unit.IsOn = true;

                FileLog.Write($"[Radio] play #{unit.Number} track={resolved} alias={alias} name={playerName} vol={apiVol:0.00} range={dist} pos={pos} playback={(playback != null ? "ok" : "null")}");

                return playback != null && sp != null;
            }
            catch (Exception ex)
            {
                RadioDebugLog.WriteEx("Play: ИСКЛЮЧЕНИЕ", ex);
                FileLog.Write($"[Radio] play err: {ex.GetType().Name} {ex.Message}");
                if (ex.InnerException != null)
                    FileLog.Write($"[Radio] inner: {ex.InnerException.Message}");
                unit.IsOn = false;
                unit.CurrentTrack = "";
                unit.PlayerName = "";
                return false;
            }
        }

        public static void Stop(RadioUnit unit)
        {
            if (unit == null) return;
            InitTypes();

            // 1) Основной путь: уничтожаем сохранённый объект напрямую
            if (unit.AudioHandle != null)
            {
                SafeDestroy(unit.AudioHandle);
                RadioDebugLog.Write($"Stop: #{unit.Number} — хэндл уничтожен напрямую.");
                unit.AudioHandle = null;
            }
            // 2) Подстраховка: ищем по имени через TryGet (Get в API нет!)
            else if (_audioPlayerType != null && !string.IsNullOrEmpty(unit.PlayerName))
            {
                try
                {
                    var tryGet = _audioPlayerType.GetMethod("TryGet",
                        new[] { typeof(string), _audioPlayerType.MakeByRefType() });

                    if (tryGet != null)
                    {
                        object[] a = { unit.PlayerName, null };
                        if ((bool)tryGet.Invoke(null, a) && a[1] != null)
                        {
                            SafeDestroy(a[1]);
                            RadioDebugLog.Write($"Stop: #{unit.Number} — найден через TryGet, уничтожен.");
                        }
                    }
                    else
                    {
                        RadioDebugLog.Write("Stop: ни хэндла, ни TryGet — зову StopAllRadioPlayers().");
                        StopAllRadioPlayers();
                    }
                }
                catch (Exception ex)
                {
                    RadioDebugLog.WriteEx("Stop: ошибка", ex);
                }
            }

            unit.IsOn = false;
            unit.CurrentTrack = "";
            unit.PlayerName = "";
            unit.AudioHandle = null;
            FileLog.Write($"[Radio] stop #{unit.Number}");
        }

        public static void StopUnit(RadioUnit unit) { Stop(unit); }

        public static void StopAllRadioPlayers()
        {
            InitTypes();
            if (_audioPlayerType == null) return;

            try
            {
                var dictMember = _audioPlayerType.GetProperty("AudioPlayerByName")?.GetValue(null)
                              ?? _audioPlayerType.GetField("AudioPlayerByName")?.GetValue(null);

                if (dictMember is System.Collections.IDictionary dict)
                {
                    var toKill = new List<object>();
                    foreach (System.Collections.DictionaryEntry e in dict)
                    {
                        if (e.Key is string key && key.StartsWith("RadioFM_", StringComparison.Ordinal))
                            toKill.Add(e.Value);
                    }

                    foreach (object p in toKill)
                        SafeDestroy(p);

                    RadioDebugLog.Write($"StopAllRadioPlayers: уничтожено {toKill.Count} плеер(ов).");
                }
                else
                {
                    RadioDebugLog.Write("StopAllRadioPlayers: словарь AudioPlayerByName не найден.");
                }
            }
            catch (Exception ex)
            {
                RadioDebugLog.WriteEx("StopAllRadioPlayers", ex);
            }
        }

        public static float VolumeOf(int volume)
        {
            int v = Mathf.Clamp(volume, 0, 5);
            return VolumeSteps[v];
        }

        public static string BuildTracksList()
        {
            if (Tracks.Count == 0) return "Trekov net. Kini .ogg v Audio/Radio/";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Trek: {Tracks.Count}:");
            for (int i = 0; i < Tracks.Count; i++)
                sb.AppendLine((i + 1) + ". " + Tracks[i]);
            return sb.ToString().TrimEnd();
        }
    }
}