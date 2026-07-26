namespace EventHUD.SpecItems
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Exiled.API.Features;
    using UnityEngine;

    public static class SpecAudio
    {
        private static readonly HashSet<string> LoadedClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static System.Type audioPlayerType;

        private static System.Type speakerType;

        private static bool typesChecked;

        public static string AudioDir
        {
            get
            {
                return Path.Combine(Path.Combine(Paths.Configs, "EventHUD"), "Audio");
            }
        }

        public static void PlayAt(Vector3 position, string fileName, float volume, float range)
        {
            try
            {
                InitTypes();

                if (audioPlayerType == null)
                {
                    SpecDebug.Log("AUDIO: сборка AudioPlayer не найдена");
                    return;
                }

                string alias = EnsureClip(fileName);

                if (alias == null)
                    return;

                float apiVolume = Mathf.Clamp01(volume / 5f);
                float distance = Mathf.Max(1f, range);
                string playerName = "SpecItem_" + Guid.NewGuid().ToString("N").Substring(0, 8);

                MethodInfo createOrGet = audioPlayerType.GetMethods()
                    .FirstOrDefault(m => m.Name == "CreateOrGet");

                if (createOrGet == null)
                {
                    SpecDebug.Log("AUDIO: CreateOrGet не найден");
                    return;
                }

                ParameterInfo[] pars = createOrGet.GetParameters();
                object[] args = new object[pars.Length];
                args[0] = playerName;

                for (int i = 1; i < pars.Length; i++)
                {
                    System.Type parameterType = pars[i].ParameterType;

                    if (parameterType == typeof(bool))
                        args[i] = false;
                    else if (parameterType == typeof(byte))
                        args[i] = (byte)255;
                    else
                        args[i] = null;
                }

                object player = createOrGet.Invoke(null, args);

                if (player == null)
                {
                    SpecDebug.Log("AUDIO: CreateOrGet вернул null");
                    return;
                }

                System.Type t = player.GetType();

                SetProperty(t, player, "SendSoundGlobally", false);
                SetProperty(t, player, "DestroyWhenAllClipsPlayed", true);

                MethodInfo addSpeaker = t.GetMethod(
                    "AddSpeaker",
                    new System.Type[] { typeof(string), typeof(bool), typeof(float), typeof(float) });

                if (addSpeaker != null)
                {
                    object speaker = addSpeaker.Invoke(player, new object[] { "Main", true, 1f, distance });

                    if (speaker != null && speakerType != null)
                    {
                        PropertyInfo positionProperty = speakerType.GetProperty("Position");

                        if (positionProperty != null && positionProperty.CanWrite)
                        {
                            positionProperty.SetValue(speaker, position, null);
                        }
                        else
                        {
                            MethodInfo setPosition = speakerType.GetMethod("SetPosition", new System.Type[] { typeof(Vector3) });

                            if (setPosition != null)
                                setPosition.Invoke(speaker, new object[] { position });
                        }
                    }
                }

                MethodInfo setSpeakerPosition = t.GetMethod(
                    "SetSpeakerPosition",
                    new System.Type[] { typeof(string), typeof(Vector3) });

                if (setSpeakerPosition != null)
                    setSpeakerPosition.Invoke(player, new object[] { "Main", position });

                MethodInfo addClip = t.GetMethod(
                    "AddClip",
                    new System.Type[] { typeof(string), typeof(float), typeof(bool), typeof(bool) });

                if (addClip == null)
                {
                    SpecDebug.Log("AUDIO: AddClip не найден");
                    return;
                }

                object playback = addClip.Invoke(player, new object[] { alias, apiVolume, false, false });

                SpecDebug.Log("AUDIO play " + fileName + " alias=" + alias + " vol=" + apiVolume.ToString("0.00")
                    + " range=" + distance + " " + (playback != null ? "ok" : "null"));
            }
            catch (Exception e)
            {
                SpecDebug.Log("AUDIO ошибка (" + fileName + "): " + e.Message);

                if (e.InnerException != null)
                    SpecDebug.Log("AUDIO inner: " + e.InnerException.Message);
            }
        }

        private static void SetProperty(System.Type type, object target, string name, object value)
        {
            try
            {
                PropertyInfo property = type.GetProperty(name);

                if (property != null && property.CanWrite)
                    property.SetValue(target, value, null);
            }
            catch
            {
            }
        }

        private static string EnsureClip(string fileName)
        {
            string alias = "spec_" + Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();

            if (LoadedClips.Contains(alias))
                return alias;

            string full = Path.Combine(AudioDir, fileName);

            if (!File.Exists(full))
            {
                SpecDebug.Log("AUDIO: нет файла " + full);
                return null;
            }

            try
            {
                System.Type storage = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try
                        {
                            return a.GetTypes();
                        }
                        catch
                        {
                            return new System.Type[0];
                        }
                    })
                    .FirstOrDefault(x => x.Name == "AudioClipStorage");

                if (storage == null)
                {
                    SpecDebug.Log("AUDIO: AudioClipStorage не найден");
                    return null;
                }

                MethodInfo loadClip = storage.GetMethod("LoadClip", new System.Type[] { typeof(string), typeof(string) });

                if (loadClip == null)
                {
                    SpecDebug.Log("AUDIO: LoadClip не найден");
                    return null;
                }

                loadClip.Invoke(null, new object[] { full, alias });
                LoadedClips.Add(alias);
                SpecDebug.Log("AUDIO загружен клип " + fileName + " -> " + alias);
                return alias;
            }
            catch (Exception e)
            {
                SpecDebug.Log("AUDIO ошибка загрузки " + fileName + ": " + e.Message);
                return null;
            }
        }

        private static void InitTypes()
        {
            if (typesChecked)
                return;

            typesChecked = true;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.FullName.StartsWith("AudioPlayer"))
                    continue;

                audioPlayerType = assembly.GetType("AudioPlayer");
                speakerType = assembly.GetType("Speaker");
                break;
            }

            SpecDebug.Log("AUDIO InitTypes: AudioPlayer=" + (audioPlayerType == null ? "нет" : "есть")
                + ", Speaker=" + (speakerType == null ? "нет" : "есть"));
        }
    }
}