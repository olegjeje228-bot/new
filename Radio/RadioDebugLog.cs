using System;
using System.IO;

namespace EventHUD.Radio
{
    public static class RadioDebugLog
    {
        private static readonly object Lock = new object();

        private static string FilePath =>
            Path.Combine(RadioStreamService.Folder, "debug.txt");

        public static void Write(string message)
        {
            try
            {
                lock (Lock)
                {
                    Directory.CreateDirectory(RadioStreamService.Folder);
                    File.AppendAllText(FilePath,
                        $"[{DateTime.Now:dd.MM HH:mm:ss.fff}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // лог не должен ронять радио
            }
        }

        public static void WriteEx(string message, Exception ex)
        {
            Write($"{message}: {ex.GetType().Name}: {ex.Message}" +
                  (ex.InnerException != null ? $" | inner: {ex.InnerException.Message}" : "") +
                  $"\n{ex.StackTrace}");
        }

        public static void Clear()
        {
            try
            {
                lock (Lock)
                {
                    Directory.CreateDirectory(RadioStreamService.Folder);
                    File.WriteAllText(FilePath, "");
                }
            }
            catch { }
        }
    }
}