using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;

namespace EventHUD.Ai
{
    public static class AiMemoryService
    {
        private const int MaxEntryChars = 350;

        private static string Dir => Path.Combine(Paths.Configs, "EventHUD-AiMemory");

        public static List<string> Load(Player player)
        {
            try
            {
                string file = FileFor(player);
                if (!File.Exists(file))
                    return new List<string>();

                string[] lines = File.ReadAllLines(file);
                var notes = new List<string>();

                foreach (string line in lines.Skip(1))
                {
                    if (line.StartsWith("ads="))
                        continue;
                    if (line.Length > 0)
                        notes.Add(line);
                }

                return notes;
            }
            catch (Exception ex)
            {
                Log.Warn($"[EventHUD.Ai] Не прочитал память: {ex.Message}");
                return new List<string>();
            }
        }

        public static void Save(Player player, List<string> notes, int maxMessages)
        {
            try
            {
                Directory.CreateDirectory(Dir);

                var lines = new List<string> { player.UserId ?? "unknown" };
                lines.AddRange(notes.Skip(Math.Max(0, notes.Count - maxMessages)));

                File.WriteAllLines(FileFor(player), lines);
            }
            catch (Exception ex)
            {
                Log.Warn($"[EventHUD.Ai] Не сохранил память: {ex.Message}");
            }
        }

        public static void Clear(Player player)
        {
            try
            {
                string file = FileFor(player);
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
            }
        }

        public static string Compress(string role, string text)
        {
            string oneLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
            if (oneLine.Length > MaxEntryChars)
                oneLine = oneLine.Substring(0, MaxEntryChars) + "...";
            return $"[{role}] {oneLine}";
        }

        private static string FileFor(Player player)
        {
            string safeNick = string.Concat((player.Nickname ?? "Unknown")
                .Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            if (safeNick.Length == 0)
                safeNick = "Unknown";
            return Path.Combine(Dir, safeNick + ".txt");
        }
    }
}
