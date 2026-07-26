using System;
using System.Linq;
using EventHUD.AntiAdm;
using EventHUD.AntiDdos;
using EventHUD.Backpack;
using EventHUD.EventHandlers;
using EventHUD.Hud;
using EventHUD.Medicine;
using EventHUD.Radio;
using EventHUD.Elevator;
using EventHUD.Rpm;
using EventHUD.Scp;
using EventHUD.Cube;
using EventHUD.SpecItems;
using Exiled.CustomItems.API;
using Exiled.API.Features;

namespace EventHUD
{
    public class Plugin : Plugin<Config>
    {
        public static Plugin Instance { get; private set; }

        public override string Name    => "EventHUD";
        public override string Author  => "rustam";
        public override Version Version => new Version(1, 2, 0);
        public override Version RequiredExiledVersion => new Version(9, 14, 2);

        public EffectScheduler Effects       { get; private set; }
        public HudCompositor   Hud           { get; private set; }
        public InjuryTickService InjuryTicks  { get; private set; }
        public MedkitHealService MedkitHeals  { get; private set; }
        public CriticalStateService CritState { get; private set; }
        public BodyStunTickService StunTicks   { get; private set; }
        public RegenTickService RegenTicks   { get; private set; }

        private PlayerEventHandlers      _handlers;
        private RadioEventHandlers       _radioHandlers;
        private RadioBroadcastFilter     _radioFilter;
        private ScpProximityVoiceFilter  _scpProximityVoiceFilter;
        private MedicineEventHandlers    _medicineHandlers;
        public AntiAdmCommandHandler    AntiAdmCommands;
        private AntiAdmGrenadeHandler    _antiAdmGrenades;

        public AntiAdmGrenadeDensityService AntiAdmGrenadeDensity { get; private set; }
        public AntiAdmAntiLagService AntiLag { get; private set; }
        public AntiDdosService AntiDdos { get; private set; }
        public TpsOptimizerService TpsOptimizer { get; private set; }
        public Discord.OnlineStatusService OnlineStatus;
        public Discord.BotCommandService BotCommands;
        public Medicine.SimpleMedkitService SimpleMedkit;

        public Scp106Handler Scp106 { get; private set; }
        public Scp049Handler Scp049 { get; private set; }
        public Scp3114Handler Scp3114 { get; private set; }
        public Scp914Handler Scp914 { get; private set; }
        public Scp096Handler Scp096 { get; private set; }
        public AloneDummyService AloneDummy { get; private set; }
        public HczArmoryService HczArmory { get; private set; }
        public HelicopterCrushService HelicopterCrush { get; private set; }
        public ScpTeslaProtectionService ScpTeslaProtection { get; private set; }
        public ElevatorBreakSystem ElevatorBreaks { get; private set; }
        public CubeLootSystem CubeLoot { get; private set; }
        public Radio.RadioFmSystem RadioFm { get; private set; }
        public AntiDdos.TrafficReportService TrafficReport { get; private set; }
        public Cube.SecondLifeSystem SecondLife { get; private set; }
        public Norma.NormaSystem Norma { get; private set; }

        private BreakerGun breakerGun;
        private TowerTeleporter towerTeleporter;
        private GrenadeLauncher grenadeLauncher;
        private GrenadeLauncherRp grenadeLauncherRp;

        public override void OnEnabled()
        {
            Instance = this;

            Ai.AiPermissions.Load();
            Ai.AiModelStore.Load();

            HudToggleService.Initialize(Config.HudEnabledByDefault);

            Effects           = new EffectScheduler(Config);
            Hud               = new HudCompositor(Config, Effects);
            InjuryTicks       = new InjuryTickService();
            MedkitHeals       = new MedkitHealService();
            CritState         = new CriticalStateService();
            StunTicks         = new BodyStunTickService();
            RegenTicks        = new RegenTickService();

            _handlers          = new PlayerEventHandlers();
            _radioHandlers     = new RadioEventHandlers(Config);
            _radioFilter       = new RadioBroadcastFilter();
            _scpProximityVoiceFilter = new ScpProximityVoiceFilter();
            _medicineHandlers  = new MedicineEventHandlers(Config);
            AntiAdmCommands   = new AntiAdmCommandHandler(Config);
            _antiAdmGrenades   = new AntiAdmGrenadeHandler(Config);
            AntiAdmGrenadeDensity = new AntiAdmGrenadeDensityService(Config);
            AntiLag            = new AntiAdmAntiLagService(Config);
            AntiDdos           = new AntiDdosService(Config);
            TpsOptimizer       = new TpsOptimizerService(Config);
            OnlineStatus       = new Discord.OnlineStatusService(Config);
            BotCommands        = new Discord.BotCommandService(Config);
            SimpleMedkit       = new Medicine.SimpleMedkitService();
            Scp106             = new Scp106Handler(Config);
            Scp049             = new Scp049Handler(Config);
            Scp3114            = new Scp3114Handler(Config);
            Scp914             = new Scp914Handler(Config);
            Scp096             = new Scp096Handler();

            // ── Player events ──
            Exiled.Events.Handlers.Player.Left                  += _handlers.OnLeft;
            Exiled.Events.Handlers.Player.SendingValidCommand   += _handlers.OnSendingValidCommand;
            Exiled.Events.Handlers.Player.Verified              += _handlers.OnVerified;
            Exiled.Events.Handlers.Player.Escaping              += _handlers.OnEscaping;
            Exiled.Events.Handlers.Player.TriggeringTesla       += _handlers.OnTriggeringTesla;

            // ── SCP events ──
            Exiled.Events.Handlers.Player.Hurting               += Scp106.OnHurting;
            Exiled.Events.Handlers.Player.EscapingPocketDimension      += Scp106.OnEscapingPocket;
            Exiled.Events.Handlers.Player.FailingEscapePocketDimension += Scp106.OnFailingEscapePocket;
            Exiled.Events.Handlers.Player.ChangingRole          += Scp3114.OnChangingRole;
            Exiled.Events.Handlers.Player.ItemAdded             += Scp3114.OnItemAdded;
            Exiled.Events.Handlers.Player.Shooting              += Scp3114.OnShooting;
            Exiled.Events.Handlers.Player.DroppingItem          += Scp049.OnDroppingItem;
            Exiled.Events.Handlers.Player.UsingItem             += Scp914.OnUsingItem;
            Exiled.Events.Handlers.Player.ChangingRole          += Scp914.OnChangingRole;

            // ── Radio events ──
            Exiled.Events.Handlers.Player.ItemAdded             += _radioHandlers.OnItemAdded;
            Exiled.Events.Handlers.Player.ChangingRadioPreset   += _radioHandlers.OnChangingRadioPreset;
            Exiled.Events.Handlers.Player.UsingRadioBattery     += _radioHandlers.OnUsingRadioBattery;
            Exiled.Events.Handlers.Player.ReceivingVoiceMessage += _radioFilter.OnReceivingVoiceMessage;
            Exiled.Events.Handlers.Player.ReceivingVoiceMessage += _scpProximityVoiceFilter.OnReceivingVoiceMessage;
            Exiled.Events.Handlers.Player.ChangingRole          += _radioHandlers.OnChangingRole;

            // ── Medicine events ──
            Exiled.Events.Handlers.Player.DroppingItem           += _medicineHandlers.OnDroppingItem;
            Exiled.Events.Handlers.Player.Hurting                += _medicineHandlers.OnHurting;
            Exiled.Events.Handlers.Player.UsedItem               += _medicineHandlers.OnUsedItem;
            Exiled.Events.Handlers.Player.UsingItem              += _medicineHandlers.OnUsingItem;
#pragma warning disable CS0612 // Type or member is obsolete

            Exiled.Events.Handlers.Player.ItemAdded              += _medicineHandlers.OnItemAdded;
#pragma warning restore CS0612 // Type or member is obsolete

            Exiled.Events.Handlers.Player.Died                   += _medicineHandlers.OnDied;
            Exiled.Events.Handlers.Player.ChangingRole           += _medicineHandlers.OnChangingRole;
            Exiled.Events.Handlers.Player.ReceivingEffect        += _medicineHandlers.OnReceivingEffect;
            Exiled.Events.Handlers.Player.EnteringPocketDimension += _medicineHandlers.OnEnteringPocketDimension;

            // ── Server events ──
            Logging.CommandLogService.Start();
            Logging.CommandLogPatcher.Register();
            Logging.GameLogService.Start();
            Logging.GameLogHandlers.Register();
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
            Exiled.Events.Handlers.Player.SendingValidCommand += AntiAdmCommands.OnSendingValidCommand;
            Exiled.Events.Handlers.Player.Handcuffing += AntiAdmCommands.OnHandcuffing;

            // ── Map events (AntiAdm) ──
            Exiled.Events.Handlers.Map.ExplodingGrenade += _antiAdmGrenades.OnExplodingGrenade;

            // ── Item cleanup на взрыв гранаты ──
            ExplosionItemCleanup.Register();

            // ── AFK warning ──
            AfkWarning.Register();

            // ── Recoil ──
            Recoil.RecoilSystem.Register();

            // ── Audio ──
            Audio.FileLog.Clear();
            Audio.FileLog.Write("[Plugin] EventHUD запущен, лог очищен.");
            Radio.RadioDebugLog.Clear();
            Radio.RadioDebugLog.Write("[Plugin] EventHUD запущен.");
            Radio.RadioStreamService.ReloadTracks();
            Audio.SoundService.LoadAll();

            // ── Backpack system ──
            new BackpackSystem().Register();

            // ── Elevator break system ──
            ElevatorBreaks = new ElevatorBreakSystem(Config);
            ElevatorBreaks.Register();

            // ── Cube loot system ──
            CubeLoot = new CubeLootSystem();
            CubeLoot.Register();
            SecondLife = new Cube.SecondLifeSystem();
            SecondLife.Register();

            // ── Traffic report ──
            TrafficReport = new AntiDdos.TrafficReportService();
            TrafficReport.Start();

            // ── Radio FM system ──
            RadioFm = new Radio.RadioFmSystem();
            RadioFm.Register();

            // ── Norma system ──
            if (Config.NormaEnabled)
            {
                Norma = new Norma.NormaSystem();
                Norma.Enable();
            }

            // ── AntiLag: детект массового спавна предметов (map editor) ──
            Exiled.Events.Handlers.Map.SpawningItem += OnSpawningItem;

            // ── AntiDdos events ──
            Exiled.Events.Handlers.Player.PreAuthenticating += AntiDdos.OnPreAuthenticating;
            Exiled.Events.Handlers.Player.Verified          += AntiDdos.OnVerified;

            // Server-Specific Settings (порядок важен!)
            ServerSpecificSettingsHandler.Register(Config);
            MedkitSSSHandler.Register();
            Scp106.RegisterSss();
            Scp049ConditionService.Register();
            Scp049.RegisterSss();
            Scp914.RegisterSss();

            EventHUD.Hud.SssRoleSync.Init(); // строго после всех Register/RegisterSss
            Exiled.Events.Handlers.Player.ChangingRole += EventHUD.Hud.SssRoleSync.OnChangingRole;
            Exiled.Events.Handlers.Player.Verified     += EventHUD.Hud.SssRoleSync.OnVerified;

            RoundControl.Register();

            // FullRP — после инициализации списков SSS
            Rpm.FullRpSss.Register();
            Rpm.FullRpState.IsEnabled = Config.FullRpDefault;
            if (Config.FullRpDefault)
                Rpm.FullRpState.ResetConfirmations();

            Effects.Start();
            Hud.Start();
            InjuryTicks.Start();
            StunTicks.Start();
            RegenTicks.Start();
            AntiAdmGrenadeDensity.Start();
            AntiLag.Start();
            AntiDdos.Start();
            TpsOptimizer.Start();
            OnlineStatus.Start();
            BotCommands.Start();
            RoundLockAutoOff.Start();
            Scp049.Start();
            Scp096.Start();
            ScpTeslaProtection = new ScpTeslaProtectionService();
            Exiled.Events.Handlers.Player.TriggeringTesla += ScpTeslaProtection.OnTriggeringTesla;
            Exiled.Events.Handlers.Player.Hurting += ScpTeslaProtection.OnHurting;
            AloneDummy = new AloneDummyService();
            AloneDummy.Start();
            HczArmory = new HczArmoryService();
            HczArmory.Start();
            HelicopterCrush = new HelicopterCrushService();
            Exiled.Events.Handlers.Server.RespawnedTeam += HelicopterCrush.OnRespawnedTeam;

            // ── SpecItems (СПЕЦ-АЙТЕМЫ ДЛЯ АДМИНОВ) ──
            InventoryLock.Enable();

            breakerGun = new BreakerGun();
            towerTeleporter = new TowerTeleporter();
            grenadeLauncher = new GrenadeLauncher();
            grenadeLauncherRp = new GrenadeLauncherRp();

            breakerGun.Register();
            towerTeleporter.Register();
            grenadeLauncher.Register();
            grenadeLauncherRp.Register();

            Exiled.Events.Handlers.Player.AimingDownSight += breakerGun.OnAimingDownSight;
            Exiled.Events.Handlers.Player.AimingDownSight += towerTeleporter.OnAimingDownSight;
            Exiled.Events.Handlers.Player.AimingDownSight += grenadeLauncherRp.OnAimingDownSight;

            SpecDebug.Log("СПЕЦ-АЙТЕМЫ зарегистрированы: 1 Ломатор, 2 Телепортер, 3 Гранатомёт, 4 ГранатомётРП");

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            // ── Player events ──
            Exiled.Events.Handlers.Player.Left                  -= _handlers.OnLeft;
            Exiled.Events.Handlers.Player.SendingValidCommand   -= _handlers.OnSendingValidCommand;
            Exiled.Events.Handlers.Player.Verified              -= _handlers.OnVerified;
            Exiled.Events.Handlers.Player.Escaping              -= _handlers.OnEscaping;
            Exiled.Events.Handlers.Player.TriggeringTesla       -= _handlers.OnTriggeringTesla;

            // ── SCP events ──
            Exiled.Events.Handlers.Player.Hurting               -= Scp106.OnHurting;
            Exiled.Events.Handlers.Player.EscapingPocketDimension      -= Scp106.OnEscapingPocket;
            Exiled.Events.Handlers.Player.FailingEscapePocketDimension -= Scp106.OnFailingEscapePocket;
            Exiled.Events.Handlers.Player.ChangingRole          -= Scp3114.OnChangingRole;
            Exiled.Events.Handlers.Player.ItemAdded             -= Scp3114.OnItemAdded;
            Exiled.Events.Handlers.Player.Shooting              -= Scp3114.OnShooting;
            Exiled.Events.Handlers.Player.DroppingItem          -= Scp049.OnDroppingItem;
            Exiled.Events.Handlers.Player.UsingItem             -= Scp914.OnUsingItem;
            Exiled.Events.Handlers.Player.ChangingRole          -= Scp914.OnChangingRole;

            // ── Radio events ──
            Exiled.Events.Handlers.Player.ItemAdded             -= _radioHandlers.OnItemAdded;
            Exiled.Events.Handlers.Player.ChangingRadioPreset   -= _radioHandlers.OnChangingRadioPreset;
            Exiled.Events.Handlers.Player.UsingRadioBattery     -= _radioHandlers.OnUsingRadioBattery;
            Exiled.Events.Handlers.Player.ReceivingVoiceMessage -= _radioFilter.OnReceivingVoiceMessage;
            Exiled.Events.Handlers.Player.ReceivingVoiceMessage -= _scpProximityVoiceFilter.OnReceivingVoiceMessage;
            Exiled.Events.Handlers.Player.ChangingRole          -= _radioHandlers.OnChangingRole;

            // ── Medicine events ──
            Exiled.Events.Handlers.Player.DroppingItem           -= _medicineHandlers.OnDroppingItem;
            Exiled.Events.Handlers.Player.Hurting                -= _medicineHandlers.OnHurting;
            Exiled.Events.Handlers.Player.UsedItem               -= _medicineHandlers.OnUsedItem;
            Exiled.Events.Handlers.Player.UsingItem              -= _medicineHandlers.OnUsingItem;
#pragma warning disable CS0612 // Type or member is obsolete

            Exiled.Events.Handlers.Player.ItemAdded              -= _medicineHandlers.OnItemAdded;
#pragma warning restore CS0612 // Type or member is obsolete

            Exiled.Events.Handlers.Player.Died                   -= _medicineHandlers.OnDied;
            Exiled.Events.Handlers.Player.ChangingRole           -= _medicineHandlers.OnChangingRole;
            Exiled.Events.Handlers.Player.ReceivingEffect        -= _medicineHandlers.OnReceivingEffect;
            Exiled.Events.Handlers.Player.EnteringPocketDimension -= _medicineHandlers.OnEnteringPocketDimension;

            // ── Server events ──
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
            Exiled.Events.Handlers.Player.SendingValidCommand -= AntiAdmCommands.OnSendingValidCommand;
            Exiled.Events.Handlers.Player.Handcuffing -= AntiAdmCommands.OnHandcuffing;

            // ── Map events (AntiAdm) ──
            Exiled.Events.Handlers.Map.ExplodingGrenade -= _antiAdmGrenades.OnExplodingGrenade;

            // ── Item cleanup на взрыв гранаты ──
            ExplosionItemCleanup.Unregister();

            // ── AFK warning ──
            AfkWarning.Unregister();

            // ── Recoil ──
            Recoil.RecoilSystem.Unregister();

            // ── SpecItems cleanup ──
            if (!(breakerGun is null))
                Exiled.Events.Handlers.Player.AimingDownSight -= breakerGun.OnAimingDownSight;

            if (!(towerTeleporter is null))
                Exiled.Events.Handlers.Player.AimingDownSight -= towerTeleporter.OnAimingDownSight;

            if (!(grenadeLauncherRp is null))
                Exiled.Events.Handlers.Player.AimingDownSight -= grenadeLauncherRp.OnAimingDownSight;

            if (!(breakerGun is null))
                breakerGun.Unregister();

            if (!(towerTeleporter is null))
                towerTeleporter.Unregister();

            if (!(grenadeLauncher is null))
                grenadeLauncher.Unregister();

            if (!(grenadeLauncherRp is null))
                grenadeLauncherRp.Unregister();

            InventoryLock.Disable();

            breakerGun = null;
            towerTeleporter = null;
            grenadeLauncher = null;
            grenadeLauncherRp = null;

            // ── Tripwire cleanup ──
            Tripwire.TripwireSystem.ClearAll();

            // ── AntiLag ──
            Exiled.Events.Handlers.Map.SpawningItem -= OnSpawningItem;

            // ── AntiDdos events ──
            Exiled.Events.Handlers.Player.PreAuthenticating -= AntiDdos.OnPreAuthenticating;
            Exiled.Events.Handlers.Player.Verified          -= AntiDdos.OnVerified;

            Exiled.Events.Handlers.Player.ChangingRole -= EventHUD.Hud.SssRoleSync.OnChangingRole;
            Exiled.Events.Handlers.Player.Verified     -= EventHUD.Hud.SssRoleSync.OnVerified;
            EventHUD.Hud.SssRoleSync.Shutdown();

            ServerSpecificSettingsHandler.Unregister();
            MedkitSSSHandler.Unregister();
            Scp106.UnregisterSss();
            Scp049.UnregisterSss();
            Scp914.UnregisterSss();

            MedkitHeals.ClearAll();
            CritState.CancelAll();
            Effects.Stop();
            Hud.Stop();
            InjuryTicks.Stop();
            StunTicks.Stop();
            RegenTicks.Stop();
            AntiAdmGrenadeDensity.Stop();
            AntiLag.Stop();
            AntiDdos.Stop();
            TpsOptimizer.Stop();
            OnlineStatus?.Stop();
            OnlineStatus = null;
            RoundLockAutoOff.Stop();
            Logging.GameLogHandlers.Unregister();
            Logging.GameLogService.Stop();
            Logging.CommandLogService.Stop();
            Logging.CommandLogPatcher.Unregister();
            BotCommands?.Stop();
            BotCommands = null;
            SimpleMedkit?.ClearAll();
            SimpleMedkit = null;
            RoundControl.Unregister();
            Rpm.FullRpSss.Unregister();
            Scp049ConditionService.Unregister();
            Scp049.Stop();
            Scp096.Stop();
            AloneDummy?.Stop();
            Exiled.Events.Handlers.Server.RespawnedTeam -= HelicopterCrush.OnRespawnedTeam;
            Exiled.Events.Handlers.Player.TriggeringTesla -= ScpTeslaProtection.OnTriggeringTesla;
            Exiled.Events.Handlers.Player.Hurting -= ScpTeslaProtection.OnHurting;

            BackpackSystem.Instance?.Unregister();

            ElevatorBreaks?.Unregister();
            ElevatorBreaks = null;

            CubeLoot?.Unregister();
            CubeLoot = null;

            SecondLife?.Unregister();
            SecondLife = null;

            TrafficReport?.Stop();
            TrafficReport = null;

            RadioFm?.Unregister();
            RadioFm = null;

            Norma?.Disable();
            Norma = null;

            HudToggleService.Reset();
            HudNoticeService.Reset();

            Instance = null;

            base.OnDisabled();
        }

        private void OnRoundStarted()
        {
            Radio.EventWaveStorage.ClearAll();
            RadioFrequencyStorage.ClearAll();
            RadioCustomFrequencyStorage.ClearAll();
            MedicalStorage.ClearAll();
            MedkitStorage.ClearAll();
            MedkitInventoryStorage.ClearAll();
            ArmorStorage.ClearAll();
            RegenStorage.ClearAll();
            ArmorRemovalStorage.ClearAll();
            AntiDdos.Reset();
            HudNoticeService.Reset();
            Scp049?.ResetAll();
            ScpProximityChat.Clear();
            ArmorItemDurabilityStorage.ClearAll();
            Tripwire.TripwireSystem.ClearAll();
            Recoil.RecoilSystem.GlobalEnabled = false;
            Recoil.RecoilSystem.Individual.Clear();
            TpsOptimizer?.SnapshotMapItems();
            SimpleMedkit?.ClearAll();
            Rpm.FullRpState.ResetConfirmations();
            EventHUD.Commands.TeslaCommand.Reset();
            EventHUD.Commands.EscapeCommand.Reset();
            AloneDummy?.Reset();
            ScpTeslaProtection?.OnRoundRestart();
        }

        // ── AntiLag: при массовом спавне предметов (map editor) —
        // временно отключаем проверку, чтобы не удалить заспавненные предметы.
        private DateTime _lastItemSpawn = DateTime.MinValue;
        private int _itemSpawnBurst = 0;

        private void OnSpawningItem(Exiled.Events.EventArgs.Map.SpawningItemEventArgs ev)
        {
            if (!Config.AntiAdmEnabled) return;

            DateTime now = DateTime.UtcNow;
            if ((now - _lastItemSpawn).TotalSeconds <= 1.0)
            {
                _itemSpawnBurst++;
                if (_itemSpawnBurst >= 10)
                {
                    AntiLag.TemporarilyDisable(Config.AntiLagMapEditorDisableSeconds);
                    _itemSpawnBurst = 0;
                }
            }
            else
            {
                _itemSpawnBurst = 1;
            }
            _lastItemSpawn = now;
        }
    }
}