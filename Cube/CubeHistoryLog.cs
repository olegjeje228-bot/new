using System;
using System.IO;
using System.Linq;
using System.Text;
using Exiled.API.Features;

namespace EventHUD.Cube
{
    public static class CubeHistoryLog
    {
        private const int MaxLines = 15000;
        private static readonly object Sync = new object();

        public static string FilePath =>
            Path.Combine(Paths.Configs, "EventHUD", "kub.txt");

        public static void Append(string line)
        {
            lock (Sync)
            {
                try
                {
                    string directory = Path.GetDirectoryName(FilePath);

                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    File.AppendAllText(
                        FilePath,
                        (line ?? string.Empty) + Environment.NewLine,
                        new UTF8Encoding(false));

                    TrimToMaximum();
                }
                catch (Exception e)
                {
                    Log.Error(
                        $"[Cube] Ne udalos zapisat kub.txt: {e}");
                }
            }
        }

        private static void TrimToMaximum()
        {
            if (!File.Exists(FilePath))
                return;

            string[] lines = File.ReadAllLines(
                FilePath,
                Encoding.UTF8);

            if (lines.Length <= MaxLines)
                return;

            string[] remaining = lines
                .Skip(lines.Length - MaxLines)
                .ToArray();

            string temporary = FilePath + ".tmp";

            File.WriteAllLines(
                temporary,
                remaining,
                new UTF8Encoding(false));

            if (File.Exists(FilePath))
                File.Delete(FilePath);

            File.Move(temporary, FilePath);
        }

        public static int GetLineCount()
        {
            lock (Sync)
            {
                try
                {
                    return File.Exists(FilePath)
                        ? File.ReadLines(FilePath).Count()
                        : 0;
                }
                catch
                {
                    return 0;
                }
            }
        }
    }
}