using System;
using System.IO;
using Exiled.API.Features;

namespace EventHUD.Audio
{
    /// <summary>
    /// Логгер в файл Configs/EventHUD/Audio/log.txt —
    /// для диагностики без доступа к консоли сервера.
    /// </summary>
    public static class FileLog
    {
        private static readonly object Sync = new object();

        public static string LogPath =>
            Path.Combine(Paths.Configs, "EventHUD", "Audio", "log.txt");

        public static void Write(string message)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(LogPath));

                    File.AppendAllText(
                        LogPath,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Файловый лог не должен ронять плагин
            }
        }

        /// <summary>Очистить лог (вызывается при старте раунда/плагина).</summary>
        public static void Clear()
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(LogPath));
                    File.WriteAllText(LogPath, string.Empty);
                }
            }
            catch { }
        }

        /// <summary>Пишет исключение полностью: тип, сообщение, все вложенные причины и стек.</summary>
        public static void WriteEx(string context, Exception e)
        {
            Write($"{context}: {e.GetType().Name}: {e.Message}");

            Exception inner = e.InnerException;
            int depth = 0;

            while (inner != null && depth < 5)
            {
                Write($"   -> причина: {inner.GetType().Name}: {inner.Message}");
                inner = inner.InnerException;
                depth++;
            }

            Write("   стек: " + e.ToString().Replace(Environment.NewLine, " | "));
        }

        /// <summary>Последние строки лога — для команды elevat log.</summary>
        public static string Tail(int lines = 25)
        {
            try
            {
                lock (Sync)
                {
                    if (!File.Exists(LogPath))
                        return "Лог пуст (файл не создан).";

                    string[] all = File.ReadAllLines(LogPath);

                    if (all.Length == 0)
                        return "Лог пуст.";

                    int start = Math.Max(0, all.Length - lines);
                    return string.Join("\n", all, start, all.Length - start);
                }
            }
            catch (Exception e)
            {
                return "Ошибка чтения лога: " + e.Message;
            }
        }
    }
}