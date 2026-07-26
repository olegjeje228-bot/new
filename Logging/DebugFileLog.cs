using System;
using System.IO;
using Exiled.API.Features;

namespace EventHUD.Logging
{
    /// <summary>Диагностика логгера команд в txt-файл.</summary>
    public static class DebugFileLog
    {
        private static readonly string FilePath = Path.Combine(Paths.Configs, "EventHUD-Debug.txt");
        private static readonly object Lock = new object();

        public static void Write(string message)
        {
            try
            {
                lock (Lock)
                    File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch { }
        }
    }
}
