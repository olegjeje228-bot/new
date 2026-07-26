using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using MEC;
using UnityEngine;

namespace EventHUD.Audio
{
    public static class SoundService
    {
        private static int _counter;

        public static string AudioDir =>
            Path.Combine(Paths.Configs, "EventHUD", "Audio");

        private static Func<Vector3, string, float, float, float, object> _playAt;
        private static Func<string, float, object> _playGlobal;
        private static bool _audioChecked;
        private static readonly HashSet<string> loadedClips = new HashSet<string>();

        /// <summary>Проверка, жив ли AudioPlayer (наследник MonoBehaviour).</summary>
        private static bool IsDead(object player)
        {
            return !(player is UnityEngine.Object uo) || uo == null;
        }

        private static void InitAudio()
        {
            if (_audioChecked) return;
            _audioChecked = true;

            try
            {
                bool assemblyFound = false;

                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!a.FullName.StartsWith("AudioPlayer")) continue;

                    assemblyFound = true;
                    FileLog.Write("[Sound] Сборка найдена: " + a.FullName);

                    var playerType = a.GetType("AudioPlayer");
                    if (playerType == null)
                    {
                        FileLog.Write("[Sound] ОШИБКА: тип AudioPlayer не найден!");
                        break;
                    }

                    // CreateOrGet один, у него 9 опциональных параметров.
                    MethodInfo createMethod = playerType
                        .GetMethods(BindingFlags.Static | BindingFlags.Public)
                        .FirstOrDefault(m => m.Name == "CreateOrGet");

                    if (createMethod == null)
                    {
                        FileLog.Write("[Sound] ОШИБКА: CreateOrGet не найден вообще!");
                        break;
                    }

                    ParameterInfo[] pars = createMethod.GetParameters();
                    FileLog.Write($"[Sound] CreateOrGet найден, параметров: {pars.Length}");

                    object CreatePlayer(string name)
                    {
                        object[] args = new object[pars.Length];
                        args[0] = name;

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

                        object player = createMethod.Invoke(null, args);
                        if (player != null)
                        {
                            // Выставляем флаги через свойства напрямую
                            var t = player.GetType();
                            t.GetProperty("SendSoundGlobally")?.SetValue(player, true);
                            t.GetProperty("DestroyWhenAllClipsPlayed")?.SetValue(player, false);
                            FileLog.Write($"[Sound] SendSoundGlobally={t.GetProperty("SendSoundGlobally")?.GetValue(player)}, DestroyWhenAllClipsPlayed={t.GetProperty("DestroyWhenAllClipsPlayed")?.GetValue(player)}");
                        }
                        return player;
                    }

                    _playAt = (pos, clip, volume, minDist, maxDist) =>
                    {
                        object player = CreatePlayer($"EventHUD-{clip}-{_counter++}");
                        if (player == null)
                        {
                            FileLog.Write("[Sound] ОШИБКА: CreateOrGet вернул null");
                            return null;
                        }

                        Type t = player.GetType();

                        var addSpeaker = t.GetMethod("AddSpeaker", new[]
                        {
                            typeof(string), typeof(Vector3), typeof(float),
                            typeof(bool), typeof(float), typeof(float)
                        });

                        if (addSpeaker == null)
                            FileLog.Write("[Sound] ОШИБКА: AddSpeaker(6) не найден");
                        else
                            addSpeaker.Invoke(player, new object[]
                                { "Main", pos, volume, true, minDist, maxDist });

                        var addClip = t.GetMethod("AddClip", new[]
                        {
                            typeof(string), typeof(float), typeof(bool), typeof(bool)
                        });

                        if (addClip == null)
                            FileLog.Write("[Sound] ОШИБКА: AddClip(4) не найден");
                        else
                            addClip.Invoke(player, new object[] { clip, volume, false, true });

                        return player;
                    };

                    _playGlobal = (clip, volume) =>
                    {
                        object player = CreatePlayer($"EventHUD-global-{_counter++}");
                        if (player == null) return null;

                        Type t = player.GetType();

                        var addSpeaker = t.GetMethod("AddSpeaker", new[]
                        {
                            typeof(string), typeof(float), typeof(bool),
                            typeof(float), typeof(float)
                        });

                        addSpeaker?.Invoke(player, new object[]
                            { "Main", volume, false, 5000f, 5000f });

                        var addClip = t.GetMethod("AddClip", new[]
                        {
                            typeof(string), typeof(float), typeof(bool), typeof(bool)
                        });

                        addClip?.Invoke(player, new object[] { clip, volume, false, true });

                        return player;
                    };

                    break;
                }

                if (!assemblyFound)
                    FileLog.Write("[Sound] ОШИБКА: AudioPlayerApi не установлен!");
            }
            catch (Exception e)
            {
                FileLog.WriteEx("[Sound] ОШИБКА InitAudio", e);
            }
        }

        // Загрузить все .ogg из папки Audio через AudioClipStorage
        public static void LoadAll()
        {
            try
            {
                Directory.CreateDirectory(AudioDir);

                string[] oggFiles = Directory.GetFiles(AudioDir, "*.ogg");
                FileLog.Write($"[Sound] Папка {AudioDir}: найдено ogg-файлов: {oggFiles.Length} ({string.Join(", ", System.Array.ConvertAll(oggFiles, Path.GetFileName))})");

                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!a.FullName.StartsWith("AudioPlayer")) continue;

                    var storage = a.GetType("AudioClipStorage");
                    if (storage == null) break;

                    var loadClip = storage.GetMethod("LoadClip",
                        new[] { typeof(string), typeof(string) });

                    if (loadClip != null)
                    {
                        foreach (string file in oggFiles)
                        {
                            string name = Path.GetFileNameWithoutExtension(file);

                            try
                            {
                                // === УЛЬТРА-ДИАГНОСТИКА ФАЙЛА ===
                                var info = new FileInfo(file);
                                byte[] head = new byte[64];

                                using (var fs = File.OpenRead(file))
                                    fs.Read(head, 0, (int)Math.Min(64, info.Length));

                                string magic = System.Text.Encoding.ASCII.GetString(head, 0, Math.Min(4, head.Length));
                                string headText = System.Text.Encoding.ASCII.GetString(head);

                                string codec =
                                    headText.Contains("\x01vorbis") ? "VORBIS (правильный)" :
                                    headText.Contains("OpusHead")   ? "OPUS — НЕ поддерживается, нужен Vorbis!" :
                                    headText.Contains("FLAC")       ? "FLAC — НЕ поддерживается!" :
                                    "неизвестный (возможно, переименованный mp3/wav)";

                                FileLog.Write($"[Sound] Файл {info.Name}: {info.Length} байт, изменён {info.LastWriteTime:HH:mm:ss dd.MM}, контейнер: {(magic == "OggS" ? "OGG" : $"НЕ OGG (magic='{magic}')")}, кодек: {codec}");

                                // === ЗАГРУЗКА ===
                                loadClip.Invoke(null, new object[] { file, name });
                                Log.Info($"[Sound] Загружен звук: {name}");
                                FileLog.Write($"[Sound] Загружен клип: {name}");
                                loadedClips.Add(name);
                            }
                            catch (Exception e)
                            {
                                Log.Warn($"[Sound] Не удалось загрузить {name}: {e.Message}");
                                FileLog.WriteEx($"[Sound] ОШИБКА загрузки {name}", e);
                            }
                        }
                    }

                    break;
                }
            }
            catch (Exception e)
            {
                Log.Warn($"[Sound] {e.Message}");
                FileLog.WriteEx("[Sound] LoadAll error", e);
            }
        }

        public static object PlayAt(Vector3 position, string clipName,
            float volume = 1f, float minDistance = 3f, float maxDistance = 25f)
        {
            try
            {
                InitAudio();

                if (_playAt == null)
                {
                    FileLog.Write($"[Sound] ПРОПУЩЕН звук {clipName}: AudioPlayer API недоступен.");
                    return null;
                }

                if (!loadedClips.Contains(clipName))
                {
                    FileLog.Write($"[Sound] ПРОПУЩЕН {clipName}: клип НЕ загружен, играть нечего.");
                    return null;
                }

                FileLog.Write($"[Sound] Играю {clipName} в {position}, vol={volume}, dist={minDistance}-{maxDistance}");
                return _playAt(position, clipName, volume, minDistance, maxDistance);
            }
            catch (Exception e)
            {
                FileLog.WriteEx($"[Sound] ОШИБКА PlayAt {clipName}", e);
                return null;
            }
        }

        public static void PlayGlobal(string clipName, float volume = 1f)
        {
            try
            {
                InitAudio();

                if (_playGlobal == null)
                {
                    FileLog.Write($"[Sound] ПРОПУЩЕН глобальный звук {clipName}: AudioPlayer API недоступен.");
                    return;
                }

                if (!loadedClips.Contains(clipName))
                {
                    FileLog.Write($"[Sound] ПРОПУЩЕН {clipName}: клип НЕ загружен, играть нечего.");
                    return;
                }

                FileLog.Write($"[Sound] Играю глобально {clipName}, vol={volume}");
                _playGlobal(clipName, volume);
            }
            catch (Exception e)
            {
                FileLog.WriteEx($"[Sound] ОШИБКА PlayGlobal {clipName}", e);
            }
        }

        /// <summary>Зацикленный звук, следующий за лифтом. Возвращает handle для остановки.</summary>
        public static object PlayFollowing(
            Exiled.API.Features.Lift lift,
            string clipName,
            float volume = 1f,
            float minDistance = 2f,
            float maxDistance = 25f)
        {
            try
            {
                InitAudio();

                if (_playAt == null)
                {
                    FileLog.Write($"[Sound] ПРОПУЩЕН {clipName}: API недоступен.");
                    return null;
                }

                if (!loadedClips.Contains(clipName))
                {
                    FileLog.Write($"[Sound] ПРОПУЩЕН {clipName}: клип НЕ загружен, играть нечего.");
                    return null;
                }

                object player = CreateLoopedPlayer($"EventHUD-{clipName}-{_counter++}", clipName, volume, minDistance, maxDistance);
                if (player == null) return null;

                FileLog.Write($"[Sound] Играю {clipName} (зациклен, следует за лифтом), vol={volume}");

                // Диагностика: жив ли плеер через 2 секунды
                object captured = player;
                Timing.CallDelayed(2f, () => FileLog.Write($"[Sound] Через 2с: жив={!IsDead(captured)}"));

                Timing.RunCoroutine(FollowSpeaker(player, lift));
                return player;
            }
            catch (Exception e)
            {
                FileLog.WriteEx($"[Sound] ОШИБКА PlayFollowing {clipName}", e);
                return null;
            }
        }

        private static object CreateLoopedPlayer(string name, string clip, float volume, float minDist, float maxDist)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!a.FullName.StartsWith("AudioPlayer")) continue;

                var playerType = a.GetType("AudioPlayer");
                if (playerType == null) break;

                MethodInfo createMethod = playerType
                    .GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "CreateOrGet");

                if (createMethod == null) break;

                ParameterInfo[] pars = createMethod.GetParameters();
                object[] args = new object[pars.Length];
                args[0] = name;
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

                object player = createMethod.Invoke(null, args);
                if (player == null) return null;

                // Выставляем флаги через свойства напрямую
                var t = player.GetType();
                t.GetProperty("SendSoundGlobally")?.SetValue(player, true);
                t.GetProperty("DestroyWhenAllClipsPlayed")?.SetValue(player, false);
                FileLog.Write($"[Sound] Looped: SendSoundGlobally={t.GetProperty("SendSoundGlobally")?.GetValue(player)}, DestroyWhenAllClipsPlayed={t.GetProperty("DestroyWhenAllClipsPlayed")?.GetValue(player)}");

                var addSpeaker = t.GetMethod("AddSpeaker", new[]
                {
                    typeof(string), typeof(Vector3), typeof(float),
                    typeof(bool), typeof(float), typeof(float)
                });

                object speakerResult = null;
                if (addSpeaker == null)
                    FileLog.Write("[Sound] ОШИБКА: AddSpeaker(6) не найден");
                else
                    speakerResult = addSpeaker.Invoke(player, new object[]
                        { "Main", Vector3.zero, volume, true, minDist, maxDist });

                var addClip = t.GetMethod("AddClip", new[]
                {
                    typeof(string), typeof(float), typeof(bool), typeof(bool)
                });

                object clipResult = null;
                if (addClip == null)
                    FileLog.Write("[Sound] ОШИБКА: AddClip(4) не найден");
                else
                    clipResult = addClip.Invoke(player, new object[] { clip, volume, true, false });

                FileLog.Write($"[Sound] CreateLoopedPlayer: AddSpeaker -> {(speakerResult != null ? "ок" : "NULL!")}, AddClip -> {(clipResult != null ? "ок" : "NULL!")}");

                return player;
            }
            return null;
        }

        private static IEnumerator<float> FollowSpeaker(object player, Exiled.API.Features.Lift lift)
        {
            var setPos = player.GetType().GetMethod("SetSpeakerPosition", new[] { typeof(string), typeof(Vector3) });

            while (true)
            {
                yield return Timing.WaitForSeconds(0.1f);

                if (IsDead(player))
                    yield break;

                try { setPos?.Invoke(player, new object[] { "Main", lift.Position }); }
                catch { yield break; }
            }
        }

        /// <summary>
        /// Play a live stream URL (AddLiveStream) at a position with volume and range.
        /// Uses AudioPlayerApi.CreateOrGet + AddSpeaker + AddLiveStream via reflection.
        /// </summary>
        public static object PlayStream(string url, Vector3 position, float volume, float maxDistance, string playerName)
        {
            try
            {
                InitAudio();

                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!a.FullName.StartsWith("AudioPlayer")) continue;

                    var playerType = a.GetType("AudioPlayer");
                    if (playerType == null) break;

                    MethodInfo createMethod = playerType
                        .GetMethods(BindingFlags.Static | BindingFlags.Public)
                        .FirstOrDefault(m => m.Name == "CreateOrGet");

                    if (createMethod == null) break;

                    ParameterInfo[] pars = createMethod.GetParameters();
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

                    object player = createMethod.Invoke(null, args);
                    if (player == null) return null;

                    var t = player.GetType();
                    t.GetProperty("SendSoundGlobally")?.SetValue(player, true);
                    t.GetProperty("DestroyWhenAllClipsPlayed")?.SetValue(player, false);

                    var addSpeaker = t.GetMethod("AddSpeaker", new[]
                    {
                        typeof(string), typeof(Vector3), typeof(float),
                        typeof(bool), typeof(float), typeof(float)
                    });

                    addSpeaker?.Invoke(player, new object[]
                        { "Main", position, volume, true, 2f, maxDistance });

                    var addLiveStream = t.GetMethod("AddLiveStream", new[]
                    {
                        typeof(string), typeof(float), typeof(string)
                    });

                    if (addLiveStream == null)
                    {
                        FileLog.Write("[Sound] AddLiveStream(3) ne nayden v AudioPlayerApi!");
                        t.GetMethod("Destroy", Type.EmptyTypes)?.Invoke(player, null);
                        return null;
                    }

                    addLiveStream.Invoke(player, new object[] { url, volume, "RadioStream" });

                    FileLog.Write($"[Sound] Stream zapushchen: {url}, vol={volume}, maxDist={maxDistance}, name={playerName}");
                    return player;
                }

                return null;
            }
            catch (Exception e)
            {
                FileLog.WriteEx("[Sound] Oshibka PlayStream", e);
                return null;
            }
        }

        /// <summary>Остановить зацикленный звук (например, при restore лифта).</summary>
        public static void StopHandle(object player)
        {
            if (player == null || IsDead(player))
                return;

            try
            {
                player.GetType().GetMethod("Destroy", Type.EmptyTypes)?.Invoke(player, null);
                FileLog.Write("[Sound] Зацикленный звук лифта остановлен.");
            }
            catch (Exception e)
            {
                FileLog.WriteEx("[Sound] StopHandle", e);
            }
        }

        // Единая точка входа: играет звук по настройкам из конфига.
        // eventPos — точка события (для spatial); если null — звук глобальный.
        public static void Play(string clipName, Vector3? eventPos = null)
        {
            try
            {
                // Настройки из конфига; если звука там нет — дефолт (spatial, 100%)
                SoundSettings s = null;
                if (Plugin.Instance?.Config?.Sounds != null)
                    Plugin.Instance.Config.Sounds.TryGetValue(clipName, out s);
                if (s == null)
                    s = new SoundSettings();

                if (!s.Enabled)
                    return;

                if (s.IsSpatial && eventPos.HasValue)
                {
                    Vector3 pos = eventPos.Value + new Vector3(s.OffsetX, s.OffsetY, s.OffsetZ);
                    PlayAt(pos, clipName, s.Volume, s.MinDistance, s.MaxDistance);
                }
                else
                {
                    PlayGlobal(clipName, s.Volume);
                }
            }
            catch (Exception e)
            {
                Log.Warn($"[Sound] Play {clipName}: {e.Message}");
            }
        }
    }
}