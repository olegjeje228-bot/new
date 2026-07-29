namespace EventHUD.Logging
{
    public static class GameLogService
    {
        public static readonly WebhookLogChannel Game =
            new WebhookLogChannel("GameLog", () => Plugin.Instance?.Config?.GameLogWebhookUrl);

        public static readonly WebhookLogChannel Moderation =
            new WebhookLogChannel("ModLog", () => Plugin.Instance?.Config?.ModerationLogWebhookUrl);

        public static void Start()
        {
            Game.Start();
            Moderation.Start();
        }

        public static void Stop()
        {
            Game.Stop();
            Moderation.Stop();
        }
    }
}