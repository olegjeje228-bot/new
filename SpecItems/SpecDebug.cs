namespace EventHUD.SpecItems
{
    using System;
    using System.IO;
    using Exiled.API.Features;

    public static class SpecDebug
    {
        private static readonly object Sync = new object();

        public static string FilePath
        {
            get
            {
                return Path.Combine(Path.Combine(Paths.Configs, "EventHUD"), "debugitems.txt");
            }
        }

        public static void Log(string text)
        {
            try
            {
                lock (Sync)
                {
                    string dir = Path.GetDirectoryName(FilePath);

                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    File.AppendAllText(
                        FilePath,
                        "[" + DateTime.Now.ToString("dd.MM HH:mm:ss") + "] " + text + Environment.NewLine);
                }
            }
            catch
            {
            }
        }
    }
}