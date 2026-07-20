using System.ComponentModel;
using Exiled.API.Interfaces;

namespace EventHUD
{
    public sealed class Config : IConfig
    {
        [Description("Включён ли плагин")]
        public bool IsEnabled { get; set; } = true;

        [Description("Debug режим")]
        public bool Debug { get; set; } = false;

        // ===================== ОБЩИЙ РАЗМЕР ШРИФТА =====================

        [Description("Базовый размер шрифта")]
        public int BaseFontSize { get; set; } = 21;

        // ===================== ЗАГОЛОВОК =====================

        [Description("Текст заголовка")]
        public string TitleText { get; set; } = "DLB Events";

        [Description("Цвет заголовка")]
        public string TitleColor { get; set; } = "#C0C0C0";

        [Description("Voffset заголовка")]
        public float TitleVoffset { get; set; } = 62f;

        [Description("Indent заголовка, %")]
        public float TitleIndent { get; set; } = -29.3f;

        // ===================== [id] [name] =====================

        [Description("Voffset строки [id] [name]")]
        public float NicknameVoffset { get; set; } = 61f;

        [Description("Indent строки [id] [name], %")]
        public float NicknameIndent { get; set; } = -29.9f;

        [Description("Размер шрифта [id] [name]")]
        public int NicknameFontSize { get; set; } = 20;

        [Description("Максимальная длина РП имени персонажа")]
        public int MaxRpNameLength { get; set; } = 24;

        // ===================== CInfo =====================

        [Description("Текст метки перед CInfo")]
        public string RoleLabelText { get; set; } = "CInfo";

        [Description("Voffset метки 'CInfo:'")]
        public float RoleLabelVoffset { get; set; } = 60f;

        [Description("Indent метки 'CInfo:', %")]
        public float RoleLabelIndent { get; set; } = -29.5f;

        [Description("Voffset значения CInfo")]
        public float RoleValueVoffset { get; set; } = 60f;

        [Description("Размер шрифта значения CInfo")]
        public int RoleValueFontSize { get; set; } = 13;

        [Description("Цвет значения CInfo")]
        public string RoleValueColor { get; set; } = "#00FFFF";

        // ===================== Статус ивента =====================

        [Description("Базовое значение indent для формулы центрирования")]
        public float EventIndentBase { get; set; } = 47f;

        [Description("Наклон формулы — на сколько % менять indent за каждый символ")]
        public float EventIndentSlope { get; set; } = 0.4f;

        [Description("Voffset статуса ивента")]
        public float EventVoffset { get; set; } = 63f;

        [Description("Максимальная длина названия ивента")]
        public int MaxEventNameLength { get; set; } = 20;

        // ===================== AFK / отсутствие хоста =====================

        [Description("Цвет текста статуса ивента, когда проводящий вышел")]
        public string HostOfflineColor { get; set; } = "#555555";

        [Description("Цвет текста статуса ивента при возврате проводящего")]
        public string HostReturnColor { get; set; } = "#00FF00";

        [Description("Длительность подсветки возврата проводящего, сек")]
        public float HostReturnHighlightDuration { get; set; } = 3f;

        [Description("Через сколько секунд бездействия RA считаем AFK")]
        public int AfkThresholdSeconds { get; set; } = 120;

        [Description("Текст AFK-режима")]
        public string AfkText { get; set; } = "Сервер в AFK-режиме";

        [Description("Цвет текста AFK-режима")]
        public string AfkColor { get; set; } = "#555555";

        // ===================== Эффект: короткий пульс =====================

        [Description("Интервал короткого пульса, сек")]
        public int MinutePulseIntervalSeconds { get; set; } = 60;

        [Description("Длительность короткого пульса, сек")]
        public float MinutePulseDuration { get; set; } = 2f;

        [Description("Цвет короткого пульса (hex, без решётки)")]
        public string MinutePulseColor { get; set; } = "FF4444";

        // ===================== Эффект: сильный флеш =====================

        [Description("Интервал сильного флеша, сек")]
        public int ColorFlashIntervalSeconds { get; set; } = 300;

        [Description("Длительность сильного флеша, сек")]
        public float ColorFlashDuration { get; set; } = 2f;

        [Description("Цвет сильного флеша (hex, без решётки)")]
        public string ColorFlashColor { get; set; } = "FF0000";

        // ===================== Эффекты: общее =====================

        [Description("Количество цветовых шагов за цикл эффекта")]
        public int EffectColorSteps { get; set; } = 20;

        [Description("Как часто отправлять HUD во время эффекта, сек")]
        public float EffectTickInterval { get; set; } = 0.15f;

        // ===================== Прочее =====================

        [Description("Интервал обновления HUD в обычном режиме, сек")]
        public float HudUpdateInterval { get; set; } = 0.15f;

        [Description("Показывать ли HUD по умолчанию")]
        public bool HudEnabledByDefault { get; set; } = true;

        [Description("Сколько секунд держать строку 'Ивент заканчивается' перед сбросом")]
        public float StopLingerSeconds { get; set; } = 5f;

        // ===================== Discord webhook =====================

        [Description("Discord webhook URL для анонсов ивентов")]
        public string DiscordWebhookUrl { get; set; } = "https://discord.com/api/webhooks/1525721529592053840/5CoYrvN1eUhls_z9fY0vCmRBD1tGg1yBMVt9Y1QOgtWmePem_xf7lJEpoomvGbLHV0Pt";

        [Description("SteamID64 -> Discord ID проводящих (для упоминания в анонсах)")]
        public System.Collections.Generic.Dictionary<string, ulong> HostDiscordIds { get; set; } = new System.Collections.Generic.Dictionary<string, ulong>
        {
            ["76561198732542518"] = 1475815739322663012,
            ["76561199684226286"] = 1423620904885424209,
            ["76561199854447494"] = 1346501845983563838,
            ["76561199683685990"] = 1147134931487694881,
            ["76561199388087145"] = 1064117343715864656,
            ["76561199687703494"] = 1362329625992364053,
        };

        // ===================== ИИ-ассистент (.ai) =====================

        [Description("Ключ API для .ai (tokengo)")]
        public string AiApiKey { get; set; } = "TUjG6e95XpKJ4nZwp1aH3T1siWhUvyEi02P7wY1EwRAkA560";

        [Description("Base URL API для .ai")]
        public string AiBaseUrl { get; set; } = "https://api.tokengo.com/v1";

        [Description("URL базы знаний для .ai (pastebin raw)")]
        public string AiKnowledgeUrl { get; set; } = "https://pastebin.com/raw/XV9vLYab";

        [Description("КД .ai для обычных игроков (сек)")]
        public float AiCooldownUser { get; set; } = 30f;

        [Description("КД .ai для админов (сек)")]
        public float AiCooldownAdmin { get; set; } = 10f;

        [Description("Лимит токенов за 10 минут: юзер")]
        public long AiTokenLimitUser { get; set; } = 5000000;

        [Description("Лимит токенов за 10 минут: админ")]
        public long AiTokenLimitAdmin { get; set; } = 30000000;

        [Description("Сообщений памяти: юзер")]
        public int AiMemoryUser { get; set; } = 30;

        [Description("Сообщений памяти: админ")]
        public int AiMemoryAdmin { get; set; } = 100;

        [Description("Макс. символов в ответе .ai")]
        public int AiMaxAnswerChars { get; set; } = 5000;

        [Description("Таймаут генерации .ai (сек)")]
        public int AiTimeoutSeconds { get; set; } = 300;

        [Description("Команды, доступные ИИ в режиме adm (первое слово команды, в нижнем регистре)")]
        public System.Collections.Generic.List<string> AiAdmCommands { get; set; } = new System.Collections.Generic.List<string>
        {
            "give", "forceclass", "fc", "effect", "heal", "cassie", "cassieadv", "kit"
        };

        [Description("Команды, запрещённые ИИ всегда, даже в режиме fulladm")]
        public System.Collections.Generic.List<string> AiBlockedCommands { get; set; } = new System.Collections.Generic.List<string>
        {
            "ban", "oban", "offlineban", "unban", "pardon", "kick", "mute", "unmute", "warn",
            "config", "serverconfig", "warhead", "detonation_start", "detonation_instant",
            "server_event", "roundrestart", "softrestart", "forcestart"
        };

        [Description("Максимум команд за один ответ ИИ")]
        public int AiMaxCommandsPerAnswer { get; set; } = 3;

        [Description("Максимум команд за один ответ админского ИИ")]
        public int AiAdminMaxCommands { get; set; } = 10;

        [Description("Включить надзор: glm-5 проверяет ответы deepseek")]
        public bool AiModerationEnabled { get; set; } = true;

        [Description("Проверять каждые N ответов deepseek")]
        public int AiModerationEvery { get; set; } = 5;

        [Description("Вебхук Discord для алертов надзора (пусто = только лог в консоль сервера)")]
        public string AiModerationWebhookUrl { get; set; } = string.Empty;

        [Description("Киты для команды kit: название -> список предметов")]
        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ItemType>> AiKits { get; set; } = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ItemType>>
        {
            ["medic"] = new System.Collections.Generic.List<ItemType> { ItemType.Medkit, ItemType.Painkillers, ItemType.Adrenaline, ItemType.SCP500 },
            ["guard"] = new System.Collections.Generic.List<ItemType> { ItemType.GunFSP9, ItemType.ArmorLight, ItemType.Medkit, ItemType.Radio },
            ["mtf"] = new System.Collections.Generic.List<ItemType> { ItemType.GunE11SR, ItemType.ArmorCombat, ItemType.Medkit, ItemType.Radio, ItemType.KeycardMTFOperative },
            ["chaos"] = new System.Collections.Generic.List<ItemType> { ItemType.GunAK, ItemType.ArmorCombat, ItemType.Medkit, ItemType.Painkillers },
        };

        [Description("Вебхук Discord для .call (staff канал). Пусто = не отправлять в Discord")]
        public string CallWebhookUrl { get; set; } = string.Empty;

        // ===================== RP-модуль: Рация =====================

        [Description("Минимальная допустимая частота рации")]
        public float RadioFreqMin { get; set; } = 10.0f;

        [Description("Максимальная допустимая частота рации")]
        public float RadioFreqMax { get; set; } = 390.0f;

        [Description("Частота волны '?'")]
        public float RadioFreqUnknown { get; set; } = 15.0f;

        [Description("Частота волны 'Комплекс'")]
        public float RadioFreqFacility { get; set; } = 95.0f;

        [Description("Частота волны 'СБ'")]
        public float RadioFreqSecurity { get; set; } = 150.0f;

        [Description("Частота волны 'МОГ'")]
        public float RadioFreqMtf { get; set; } = 200.0f;

        [Description("Частота волны 'ПХ'")]
        public float RadioFreqChaos { get; set; } = 250.0f;

        [Description("Текст строки волны рации в HUD")]
        public string RadioWaveLabel { get; set; } = "Волна рации";

        [Description("Voffset строки волны рации")]
        public float RadioWaveVoffset { get; set; } = 59f;

        [Description("Indent строки волны рации, %")]
        public float RadioWaveIndent { get; set; } = -29.5f;

        [Description("Шаблон хинта смены режима. {0}=цвет, {1}=название")]
        public string RadioSwitchHintTemplate { get; set; } = "Переключен режим рации: <color={0}>{1}</color>";

        [Description("Voffset хинта смены режима")]
        public float RadioSwitchHintVoffset { get; set; } = 15f;

        [Description("Indent хинта смены режима, %")]
        public float RadioSwitchHintIndent { get; set; } = 26f;

        [Description("Размер шрифта хинта смены режима")]
        public int RadioSwitchHintFontSize { get; set; } = 35;

        [Description("Длительность хинта смены режима, сек")]
        public float RadioSwitchHintDuration { get; set; } = 1f;

        [Description("ID слайдера Radio HZ в Server-Specific Settings (не менять без причины)")]
        public int RadioHzSettingId { get; set; } = 9001;

        // ===================== RP-модуль: Медицина =====================

        [Description("Порог передоза адреналином (кол-во за жизнь)")]
        public int AdrenalineOverdoseThreshold { get; set; } = 3;

        [Description("Порог передоза обезболивающим (кол-во за жизнь)")]
        public int PainkillerOverdoseThreshold { get; set; } = 4;

        [Description("Длительность эффектов передоза, сек")]
        public float OverdoseEffectDuration { get; set; } = 25f;

        [Description("Intensity Slowness при передозе")]
        public byte OverdoseSlownessIntensity { get; set; } = 55;

        [Description("Intensity Asphyxiated при передозе")]
        public byte OverdoseAsphyxiatedIntensity { get; set; } = 25;

        [Description("Макс. урон от гранаты для капиллярного кровотечения")]
        public float LightBleedGrenadeMaxDamage { get; set; } = 10f;

        [Description("Макс. урон от падения для капиллярного кровотечения")]
        public float LightBleedFallMaxDamage { get; set; } = 15f;

        [Description("Длит. Concussed при капиллярном, сек")]
        public float LightBleedConcussedDuration { get; set; } = 5f;

        [Description("Урон/сек бурной фазы капиллярного")]
        public float LightBleedBurstDps { get; set; } = 2f;

        [Description("Длительность бурной фазы капиллярного, сек")]
        public float LightBleedBurstDuration { get; set; } = 5f;

        [Description("Урон пассивной фазы капиллярного за интервал")]
        public float LightBleedPassiveDps { get; set; } = 1f;

        [Description("Интервал пассивной фазы капиллярного, сек")]
        public float LightBleedPassiveInterval { get; set; } = 10f;

        [Description("Мин. урон от гранаты для венозного")]
        public float MediumBleedGrenadeMinDamage { get; set; } = 15f;

        [Description("Макс. урон от гранаты для венозного")]
        public float MediumBleedGrenadeMaxDamage { get; set; } = 50f;

        [Description("Длит. Concussed при венозном, сек")]
        public float MediumBleedConcussedDuration { get; set; } = 10f;

        [Description("Длит. Deafened при венозном, сек")]
        public float MediumBleedDeafenedDuration { get; set; } = 15f;

        [Description("Intensity Blindness при венозном")]
        public byte MediumBleedBlindnessIntensity { get; set; } = 40;

        [Description("Длит. Blindness при венозном, сек")]
        public float MediumBleedBlindnessDuration { get; set; } = 30f;

        [Description("Урон/сек бурной фазы венозного")]
        public float MediumBleedBurstDps { get; set; } = 6f;

        [Description("Длительность бурной фазы венозного, сек")]
        public float MediumBleedBurstDuration { get; set; } = 5f;

        [Description("Урон пассивной фазы венозного за интервал")]
        public float MediumBleedPassiveDps { get; set; } = 3f;

        [Description("Интервал пассивной фазы венозного, сек")]
        public float MediumBleedPassiveInterval { get; set; } = 10f;

        [Description("Длит. Concussed при артериальном, сек")]
        public float HeavyBleedConcussedDuration { get; set; } = 10f;

        [Description("Длит. Deafened при артериальном, сек")]
        public float HeavyBleedDeafenedDuration { get; set; } = 15f;

        [Description("Intensity Blindness при артериальном")]
        public byte HeavyBleedBlindnessIntensity { get; set; } = 40;

        [Description("Длит. Blindness при артериальном, сек")]
        public float HeavyBleedBlindnessDuration { get; set; } = 30f;

        [Description("Урон/сек бурной фазы артериального")]
        public float HeavyBleedBurstDps { get; set; } = 2f;

        [Description("Длительность бурной фазы артериального, сек")]
        public float HeavyBleedBurstDuration { get; set; } = 30f;

        [Description("Урон пассивной фазы артериального за интервал")]
        public float HeavyBleedPassiveDps { get; set; } = 5f;

        [Description("Интервал пассивной фазы артериального, сек")]
        public float HeavyBleedPassiveInterval { get; set; } = 10f;

        [Description("Радиус flashbang для контузии, м")]
        public float ConcussionFlashRadius { get; set; } = 15f;

        [Description("Радиус гранаты для контузии, м")]
        public float ConcussionGrenadeRadius { get; set; } = 30f;

        [Description("Радиус для сильной контузии, м")]
        public float SevereConcussionRadius { get; set; } = 5f;

        [Description("Время рядом с SCP-106 до коррозии, сек")]
        public float CorrosionProximityTime { get; set; } = 3f;

        [Description("Длит. Stained при коррозии, сек")]
        public float CorrosionStainedDuration { get; set; } = 3000f;

        [Description("Intensity Blindness при коррозии")]
        public byte CorrosionBlindnessIntensity { get; set; } = 45;

        [Description("Длит. Concussed при коррозии, сек")]
        public float CorrosionConcussedDuration { get; set; } = 3000f;

        [Description("Время в SCP-244 до химической травмы, сек")]
        public float Scp244ProximityTime { get; set; } = 10f;

        [Description("Длит. Concussed при химической травме, сек")]
        public float ChemicalConcussedDuration { get; set; } = 60f;

        [Description("Длит. Burned при ожоге от Micro-HID, сек")]
        public float BurnDuration { get; set; } = 10f;

        [Description("Intensity Slowness при переломе")]
        public byte FractureSlownessIntensity { get; set; } = 70;

        [Description("Длит. Concussed при переломе, сек")]
        public float FractureConcussedDuration { get; set; } = 30f;

        [Description("Интервал тика травм, сек")]
        public float InjuryTickInterval { get; set; } = 1f;

        [Description("Voffset строки состояния (медицина) 1")]
        public float MedicineHudVoffset { get; set; } = 61f;

        [Description("Voffset строки состояния (медицина) 2")]
        public float MedicineHudVoffset2 { get; set; } = 60f;

        [Description("Indent строки состояния (медицина), %")]
        public float MedicineHudIndent { get; set; } = -29.5f;

        [Description("Текст метки состояния")]
        public string MedicineHudLabel { get; set; } = "Состояние";

        // ===================== Анти-Админ (AntiAdm) =====================

        [Description("Включена ли система AntiAdm")]
        public bool AntiAdmEnabled { get; set; } = true;

        [Description("Максимальное кол-во Dummy на сервере")]
        public int AntiAdmMaxDummies { get; set; } = 40;

        [Description("Максимальный changescale для Dummy")]
        public float AntiAdmDummyMaxScale { get; set; } = 25f;

        [Description("Максимальный changescale для игроков")]
        public float AntiAdmMaxScale { get; set; } = 150f;

        [Description("Максимальный ccolor для игроков (каждый канал)")]
        public float AntiAdmMaxColor { get; set; } = 5000f;

        [Description("Максимум патронов одному игроку за один запрос")]
        public int AntiAdmMaxAmmo { get; set; } = 10000;

        [Description("Максимум патронов у одного игрока (суммарно)")]
        public int AntiAdmMaxTotalAmmo { get; set; } = 3500;

        [Description("Максимум основных предметов в инвентаре одного игрока")]
        public int AntiAdmMaxInventoryItems { get; set; } = 60;

        [Description("Максимум предметов у одного игрока")]
        public int AntiAdmMaxTotalItems { get; set; } = 150;

        [Description("Максимум предметов, выдаваемых админом за 2 секунды")]
        public int AntiAdmMaxItemsPerTwoMinutes { get; set; } = 200;

        [Description("Максимум патронов, выдаваемых админом за 5 секунд")]
        public int AntiAdmMaxAmmoBurst { get; set; } = 2500;

        [Description("Максимум запросов give/ga от админа за 5 секунд")]
        public int AntiAdmMaxGiveRequestsBurst { get; set; } = 100;

        [Description("SteamID, которому разрешена команда mp tg")]
        public string AntiAdmMpTgAllowedSteamId { get; set; } = "76561199687703494@steam";

        [Description("КД на команду mp cr, сек")]
        public float AntiAdmMpCrCooldownSeconds { get; set; } = 5f;

        [Description("Максимум схематиков mp cr")]
        public int AntiAdmMpCrMaxSchematics { get; set; } = 4;

        [Description("Максимум больших схематиков mp cr")]
        public int AntiAdmMpCrMaxLargeSchematics { get; set; } = 2;

        [Description("Максимум использований mp load")]
        public int AntiAdmMpLoadMaxUses { get; set; } = 2;

        [Description("Максимум forceclass за секунду (массовый = 1 раз)")]
        public int AntiAdmMaxForceClassPerSecond { get; set; } = 3;

        [Description("Максимум смен роли Dummy через forceclass за 3 секунды")]
        public int AntiAdmMaxDummyForceClassPerMinute { get; set; } = 8;

        [Description("КД на любую RA-команду, сек")]
        public float AntiAdmCommandCooldown { get; set; } = 0.1f;

        [Description("Максимум спавнов дамми в минуту (spawn→kill→spawn обход AA-01)")]
        public int AntiAdmMaxDummySpawnsPerMinute { get; set; } = 12;

        [Description("Максимум предметов дамми (не более 3, без патронов/гранат/018/фонариков)")]
        public int AntiAdmMaxDummyItems { get; set; } = 3;

        [Description("Максимум специальных предметов одному игроку (22,25,26,31,43,44,45,46)")]
        public int AntiAdmMaxSpecialItems { get; set; } = 2;

        [Description("Максимум пачек патронов одному игроку (19,22,27,28,29)")]
        public int AntiAdmMaxAmmoPacks { get; set; } = 15;

        [Description("Максимум взрывов detonation_instant за игру")]
        public int AntiAdmMaxDetonationInstant { get; set; } = 2;

        [Description("Максимум AdminToy на сервере")]
        public int AntiAdmMaxAdminToys { get; set; } = 20;

        [Description("Лимит смертей дамми в минуту до блокировки")]
        public int AntiAdmDummyDeathLimitPerMinute { get; set; } = 30;

        [Description("Длительность блокировки дамми при спаме смертей, сек")]
        public float AntiAdmDummyDeathBlockDuration { get; set; } = 60f;

        [Description("Радиус очистки предметов при взрыве гранаты, м")]
        public float AntiAdmGrenadeItemCleanRadius { get; set; } = 10f;

        [Description("Мин. кол-во предметов для очистки при взрыве (если меньше — не чистить)")]
        public int AntiAdmGrenadeItemCleanThreshold { get; set; } = 20;

        [Description("Радиус проверки цепных гранат, м")]
        public float AntiAdmGrenadeChainRadius { get; set; } = 15f;

        [Description("Мин. кол-во гранат поблизости для блокировки детонации")]
        public int AntiAdmGrenadeChainThreshold { get; set; } = 3;

        [Description("Радиус проверки плотности гранат, м")]
        public float AntiAdmGrenadeDensityRadius { get; set; } = 20f;

        [Description("Порог гранат в радиусе для срабатывания очистки")]
        public int AntiAdmGrenadeDensityMax { get; set; } = 30;

        [Description("Сколько гранат оставлять после очистки (лимит)")]
        public int AntiAdmGrenadeDensityLimit { get; set; } = 20;

        [Description("Интервал сканирования плотности гранат, сек")]
        public float AntiAdmGrenadeDensityInterval { get; set; } = 3f;

        // ===================== Анти-Лаг (AntiLag) =====================

        [Description("Интервал сканирования рагдоллов и предметов, сек")]
        public float AntiLagScanInterval { get; set; } = 3f;

        // ── Рагдоллы (трупы) ──
        [Description("Макс. рагдоллов в ближнем радиусе (удаляем самые старые)")]
        public int AntiLagRagdollCloseMax { get; set; } = 9;

        [Description("Ближний радиус проверки рагдоллов, м")]
        public float AntiLagRagdollCloseRadius { get; set; } = 10f;

        [Description("Макс. рагдоллов в дальнем радиусе (удаляем самые старые)")]
        public int AntiLagRagdollFarMax { get; set; } = 20;

        [Description("Дальний радиус проверки рагдоллов, м")]
        public float AntiLagRagdollFarRadius { get; set; } = 100f;

        // ── Предметы (выброшенные) ──
        [Description("Макс. предметов в радиусе (удаляем случайно)")]
        public int AntiLagPickupMax { get; set; } = 120;

        [Description("Макс. предметов в радиусе 2 метров")]
        public int AntiLagPickupCloseMax { get; set; } = 40;

        [Description("Радиус проверки предметов, м")]
        public float AntiLagPickupRadius { get; set; } = 10f;

        [Description("Сколько секунд отключать проверку предметов при map editor spawn")]
        public float AntiLagMapEditorDisableSeconds { get; set; } = 5f;

        // ── SCP-018 мячики ──
        [Description("Максимум мячиков SCP-018 на всей карте")]
        public int AntiLagMaxBallsGlobal { get; set; } = 7;

        [Description("Максимум мячиков SCP-018 в радиусе (AntiLagBallRadius)")]
        public int AntiLagMaxBallsPerRadius { get; set; } = 2;

        [Description("Радиус проверки мячиков SCP-018, м")]
        public float AntiLagBallRadius { get; set; } = 30f;

        // ===================== TPS-оптимизация =====================

        [Description("TPS порог для уровня 1 оптимизации")]
        public float TpsLevel1Threshold { get; set; } = 50f;

        [Description("TPS порог для уровня 2 оптимизации")]
        public float TpsLevel2Threshold { get; set; } = 20f;

        [Description("TPS порог для уровня 3 (плотный рестарт)")]
        public float TpsLevel3Threshold { get; set; } = 10f;

        [Description("HUD интервал при TPS-оптимизации уровня 1, сек")]
        public float TpsOptimizedHudInterval { get; set; } = 0.3f;

        [Description("Радиус отображения предметов на уровне 1, м")]
        public float TpsLevel1ItemRadius { get; set; } = 40f;

        [Description("Радиус отображения предметов на уровне 2, м")]
        public float TpsLevel2ItemRadius { get; set; } = 10f;

        [Description("Максимум предметов в кластере 20м для очистки")]
        public int TpsClusterCleanupThreshold { get; set; } = 70;

        [Description("Радиус проверки кластера предметов, м")]
        public float TpsClusterRadius { get; set; } = 20f;

        [Description("Максимум scp-330 мячиков до очистки")]
        public int TpsMaxCandiesBeforeCleanup { get; set; } = 2;

        [Description("Задержка перед плотным рестартом, сек")]
        public float TpsDenseRestartDelaySeconds { get; set; } = 15f;

        // ===================== Анти-DDoS (L7 join-флуд) =====================
        // ВАЖНО: это защита уровня приложения только от флуда подключений.
        // От объёмного DDoS защищает ТОЛЬКО хост/сеть (GRE-туннель, scrubbing).

        [Description("Включена ли система AntiDdos")]
        public bool AntiDdosEnabled { get; set; } = true;

        [Description("IP, которые никогда не блокируются (стафф/доверенные)")]
        public System.Collections.Generic.List<string> AntiDdosWhitelistIps { get; set; } =
            new System.Collections.Generic.List<string> { "127.0.0.1" };

        [Description("Discord webhook для оповещений о слоях атаки (пусто = выкл)")]
        public string AntiDdosWebhookUrl { get; set; } = "";

        [Description("Интервал мониторинга, сек")]
        public float AntiDdosMonitorInterval { get; set; } = 1f;

        // ── Пороги подключений в секунду для слоёв ──
        // ВАЖНО: пороги должны быть выше нормального пика подключений.
        // 8-20 conn/sec — это НОРМА для запуска карты (все заходят одновременно).
        // Ложные срабатывания = сервер блокирует легитимных игроков.
        [Description("Слой 1 (лёгкий): подключений/сек (норма = 8-15 при старте карты)")]
        public int AntiDdosLayer1ConnPerSec { get; set; } = 25;

        [Description("Слой 2 (сильный): подключений/сек")]
        public int AntiDdosLayer2ConnPerSec { get; set; } = 50;

        [Description("Слой 3 (не справляется): подключений/сек")]
        public int AntiDdosLayer3ConnPerSec { get; set; } = 100;

        [Description("Слой 4 (lockdown): подключений/сек")]
        public int AntiDdosLayer4ConnPerSec { get; set; } = 200;

        // ── Пороги TPS (сервер задыхается) ──
        // ВАЖНО: TPS может падать из-за тяжёлых ивентов, медицины, гранат —
        // это НЕ DDoS. Пороги должны быть очень низкими.
        [Description("Слой 3 при SmoothTps ниже этого значения (только при флуде подключений)")]
        public double AntiDdosLayer3TpsThreshold { get; set; } = 3.0;

        [Description("Слой 4 при SmoothTps ниже этого значения (только при флуде подключений)")]
        public double AntiDdosLayer4TpsThreshold { get; set; } = 1.0;

        // ── Пер-IP лимиты ──
        [Description("Макс. подключений с одного IP за окно (сек)")]
        public int AntiDdosMaxConnPerIp { get; set; } = 5;

        [Description("Окно подсчёта подключений с одного IP, сек")]
        public float AntiDdosPerIpWindowSeconds { get; set; } = 60f;

        [Description("Длительность временного бана IP, сек")]
        public float AntiDdosIpTempBanSeconds { get; set; } = 300f;

        // ── Гистерезис ──
        [Description("Сколько секунд метрики должны быть спокойны для понижения слоя")]
        public float AntiDdosDeescalateSeconds { get; set; } = 60f;

        [Description("На Слое 3+ разрешать вход только известным (недавно игравшим) игрокам")]
        public bool AntiDdosLayer3KnownOnly { get; set; } = true;
    }
}