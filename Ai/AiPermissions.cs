using System;
using System.IO;
using Exiled.API.Features;

namespace EventHUD.Ai
{
    public enum AiPermissionLevel
    {
        None = 0,
        Safe = 1,
        Adm = 2,
        FullAdm = 3
    }

    public static class AiPermissions
    {
        public static bool Safe { get; private set; } = true;
        public static bool Adm { get; private set; }
        public static bool FullAdm { get; private set; }

        private static string FilePath => Path.Combine(Paths.Configs, "EventHUD-AiPerms.txt");

        public static AiPermissionLevel GetLevel()
        {
            if (FullAdm) return AiPermissionLevel.FullAdm;
            if (Adm) return AiPermissionLevel.Adm;
            if (Safe) return AiPermissionLevel.Safe;
            return AiPermissionLevel.None;
        }

        public static bool Set(string mode, bool value)
        {
            switch (mode.ToLowerInvariant())
            {
                case "safe": Safe = value; break;
                case "adm": Adm = value; break;
                case "fulladm": FullAdm = value; break;
                default: return false;
            }

            Save();
            return true;
        }

        public static string Status()
        {
            return $"safe: {(Safe ? "on" : "off")} | adm: {(Adm ? "on" : "off")} | fulladm: {(FullAdm ? "on" : "off")} | итоговый уровень: {GetLevel()}";
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return;

                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var parts = line.Split('=');
                    if (parts.Length != 2)
                        continue;

                    bool value = parts[1].Trim() == "1";
                    switch (parts[0].Trim())
                    {
                        case "safe": Safe = value; break;
                        case "adm": Adm = value; break;
                        case "fulladm": FullAdm = value; break;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn($"[AiPermissions] Не удалось загрузить настройки: {e.Message}");
            }
        }

        private static void Save()
        {
            try
            {
                File.WriteAllText(FilePath,
                    $"safe={(Safe ? 1 : 0)}\nadm={(Adm ? 1 : 0)}\nfulladm={(FullAdm ? 1 : 0)}\n");
            }
            catch (Exception e)
            {
                Log.Warn($"[AiPermissions] Не удалось сохранить настройки: {e.Message}");
            }
        }
    }
}
