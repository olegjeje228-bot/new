using System;
using Exiled.API.Features;

namespace EventHUD
{
    public static class ServerNameService
    {
        private static string _original;

        public static void SetStatus(string statusText, string colorHex)
        {
            try
            {
                _original ??= Server.Name;
                Server.Name = $"{_original}\n<color={colorHex}>{statusText}</color>";
            }
            catch (Exception ex)
            {
                Log.Warn($"[EventHUD] Не удалось сменить название сервера: {ex.Message}");
            }
        }

        public static void Reset()
        {
            try
            {
                if (_original != null)
                    Server.Name = _original;
            }
            catch (Exception ex)
            {
                Log.Warn($"[EventHUD] Не удалось вернуть название сервера: {ex.Message}");
            }
        }
    }
}
