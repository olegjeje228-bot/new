using System;
using System.IO;
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

        private static Action<Vector3, string, float, float, float> _playAt;
        private static Action<string, float> _playGlobal;
        private static bool _audioChecked;

        private static void InitAudio()
        {
            if (_audioChecked) return;
            _audioChecked = true;

            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!a.FullName.StartsWith("AudioPlayer")) continue;

                    var playerType = a.GetType("AudioPlayer");
                    if (playerType == null) break;

                    var staticMethods = playerType.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                    // AudioPlayer.CreateOrGet(name, onIntialCreation) — ищем по имени + кол-ву параметров
                    var createMethod = playerType.GetMethod("CreateOrGet",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                        null, new[] { typeof(string), typeof(Delegate) }, null);
                    if (createMethod == null)
                    {
                        // fallback: ищем любой CreateOrGet с 2 параметрами
                        foreach (var m in playerType.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public))
                        {
                            if (m.Name == "CreateOrGet" && m.GetParameters().Length == 2)
                            {
                                createMethod = m;
                                break;
                            }
                        }
                    }

                    if (createMethod != null)
                    {
                        _playAt = (pos, clip, volume, minDist, maxDist) =>
                        {
                            var player = createMethod.Invoke(null, new object[] {
                                $"EventHUD-{clip}-{_counter++}",
                                (Action<object>)(p =>
                                {
                                    var addSpeaker = p.GetType().GetMethod("AddSpeaker",
                                        new[] { typeof(string), typeof(bool), typeof(float), typeof(float) });
                                    addSpeaker?.Invoke(p, new object[] { "Main", true, minDist, maxDist });

                                    var setPos = p.GetType().GetMethod("SetSpeakerPosition",
                                        new[] { typeof(string), typeof(Vector3) });
                                    setPos?.Invoke(p, new object[] { "Main", pos });

                                    var addClip = p.GetType().GetMethod("AddClip",
                                        new[] { typeof(string), typeof(float) });
                                    addClip?.Invoke(p, new object[] { clip, volume });
                                })
                            });

                            Timing.CallDelayed(30f, () =>
                            {
                                try
                                {
                                    var destroy = player.GetType().GetMethod("Destroy", Type.EmptyTypes);
                                    destroy?.Invoke(player, null);
                                }
                                catch { }
                            });
                        };

                        _playGlobal = (clip, volume) =>
                        {
                            var player = createMethod.Invoke(null, new object[] {
                                $"EventHUD-global-{_counter++}",
                                (Action<object>)(p =>
                                {
                                    var addSpeaker = p.GetType().GetMethod("AddSpeaker",
                                        new[] { typeof(string), typeof(bool), typeof(float) });
                                    addSpeaker?.Invoke(p, new object[] { "Main", false, 5000f });

                                    var addClip = p.GetType().GetMethod("AddClip",
                                        new[] { typeof(string), typeof(float) });
                                    addClip?.Invoke(p, new object[] { clip, volume });
                                })
                            });

                            Timing.CallDelayed(60f, () =>
                            {
                                try
                                {
                                    var destroy = player.GetType().GetMethod("Destroy", Type.EmptyTypes);
                                    destroy?.Invoke(player, null);
                                }
                                catch { }
                            });
                        };
                    }

                    break;
                }
            }
            catch { }
        }

        // Загрузить все .ogg из папки Audio через AudioClipStorage
        public static void LoadAll()
        {
            try
            {
                Directory.CreateDirectory(AudioDir);

                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!a.FullName.StartsWith("AudioPlayer")) continue;

                    var storage = a.GetType("AudioClipStorage");
                    if (storage == null) break;

                    var loadClip = storage.GetMethod("LoadClip",
                        new[] { typeof(string), typeof(string) });

                    if (loadClip != null)
                    {
                        foreach (string file in Directory.GetFiles(AudioDir, "*.ogg"))
                        {
                            string name = Path.GetFileNameWithoutExtension(file);
                            try
                            {
                                loadClip.Invoke(null, new object[] { file, name });
                                Log.Info($"[Sound] Загружен звук: {name}");
                            }
                            catch (Exception e)
                            {
                                Log.Warn($"[Sound] Не удалось загрузить {name}: {e.Message}");
                            }
                        }
                    }

                    break;
                }
            }
            catch (Exception e)
            {
                Log.Warn($"[Sound] {e.Message}");
            }
        }

        public static void PlayAt(Vector3 position, string clipName,
            float volume = 1f, float minDistance = 3f, float maxDistance = 25f)
        {
            try
            {
                InitAudio();
                _playAt?.Invoke(position, clipName, volume, minDistance, maxDistance);
            }
            catch (Exception e)
            {
                Log.Warn($"[Sound] PlayAt {clipName}: {e.Message}");
            }
        }

        public static void PlayGlobal(string clipName, float volume = 1f)
        {
            try
            {
                InitAudio();
                _playGlobal?.Invoke(clipName, volume);
            }
            catch (Exception e)
            {
                Log.Warn($"[Sound] PlayGlobal {clipName}: {e.Message}");
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
