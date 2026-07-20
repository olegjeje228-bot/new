namespace EventHUD.AntiDdos
{
    /// <summary>
    /// Уровни (слои) реакции на флуд подключений.
    /// </summary>
    public enum AntiDdosLevel
    {
        /// <summary>Норма — ограничений нет.</summary>
        Normal = 0,

        /// <summary>Слой 1 — лёгкий флуд: пер-IP rate-limit.</summary>
        Layer1 = 1,

        /// <summary>Слой 2 — сильный флуд: жёсткий лимит + задержка новых.</summary>
        Layer2 = 2,

        /// <summary>Слой 3 — сервер не справляется: только известные игроки.</summary>
        Layer3 = 3,

        /// <summary>Слой 4 — lockdown: все подключения отклоняются, только консоль.</summary>
        Layer4 = 4
    }
}
