using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;

namespace EventHUD.Ai
{
    public static class AiModelStore
    {
        private static readonly Dictionary<string, string> Selected = new Dictionary<string, string>();

        private static string FilePath => Path.Combine(Paths.Configs, "EventHUD-AiModels.txt");

        public static string Get(string userId)
        {
            string model;
            return Selected.TryGetValue(userId, out model) ? model : null;
        }

        public static void Set(string userId, string modelKey)
        {
            Selected[userId] = modelKey;
            Save();
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return;

                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var idx = line.IndexOf('=');
                    if (idx <= 0)
                        continue;
                    Selected[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
                }
            }
            catch (Exception e)
            {
                Log.Warn($"[AiModelStore] Не удалось загрузить выбор моделей: {e.Message}");
            }
        }

        private static void Save()
        {
            try
            {
                File.WriteAllLines(FilePath, Selected.Select(p => p.Key + "=" + p.Value));
            }
            catch (Exception e)
            {
                Log.Warn($"[AiModelStore] Не удалось сохранить выбор моделей: {e.Message}");
            }
        }
    }
}
