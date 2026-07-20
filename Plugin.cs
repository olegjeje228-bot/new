using System;
using EventHUD.AntiAdm;
using EventHUD.AntiDdos;
using EventHUD.EventHandlers;
using EventHUD.Hud;
using EventHUD.Medicine;
using EventHUD.Radio;
using EventHUD.Rpm;
using EventHUD.Scp;
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

        public Scp106Handler Scp106 { get; private set; }
        public Scp049Handler Scp049 { get; private set; }
        public Scp3114Handler Scp3114 { get; private set; }
        public Scp914Handler Scp914 { get; private set; }
        public Scp096Handler Scp096 { get; private set; }
        public AloneDummyService AloneDummy { get; private set; }
        public HczArmoryService HczArmory { get; private set; }
        public HelicopterCrushService HelicopterCrush { get; private set; }
        public ScpTeslaProtectionService ScpTeslaProtection { get; private set; }

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
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
            Exiled.Events.Handlers.Player.SendingValidCommand += AntiAdmCommands.OnSendingValidCommand;
            Exiled.Events.Handlers.Player.Handcuffing += AntiAdmCommands.OnHandcuffing;

            // ── Map events (AntiAdm) ──
            Exiled.Events.Handlers.Map.ExplodingGrenade += _antiAdmGrenades.OnExplodingGrenade;

            // ── AntiLag: детект массового спавна предметов (map editor) ──
            Exiled.Events.Handlers.Map.SpawningItem += OnSpawningItem;

            // ── AntiDdos events ──
            Exiled.Events.Handlers.Player.PreAuthenticating += AntiDdos.OnPreAuthenticating;
            Exiled.Events.Handlers.Player.Verified          += AntiDdos.OnVerified;

            // Server-Specific Settings
            ServerSpecificSettingsHandler.Register(Config);
            MedkitSSSHandler.Register();
            Scp106.RegisterSss();
            Scp049ConditionService.Register();
            Scp049.RegisterSss();
            Scp914.RegisterSss();

            EventHUD.Hud.SssRoleSync.Init(); // строго после всех Register/RegisterSss
            Exiled.Events.Handlers.Player.ChangingRole += EventHUD.Hud.SssRoleSync.OnChangingRole;
            Exiled.Events.Handlers.Player.Verified     += EventHUD.Hud.SssRoleSync.OnVerified;

            Effects.Start();
            Hud.Start();
            InjuryTicks.Start();
            StunTicks.Start();
            RegenTicks.Start();
            AntiAdmGrenadeDensity.Start();
            AntiLag.Start();
            AntiDdos.Start();
            TpsOptimizer.Start();
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
            Scp049ConditionService.Unregister();
            Scp049.Stop();
            Scp096.Stop();
            AloneDummy?.Stop();
            Exiled.Events.Handlers.Server.RespawnedTeam -= HelicopterCrush.OnRespawnedTeam;
            Exiled.Events.Handlers.Player.TriggeringTesla -= ScpTeslaProtection.OnTriggeringTesla;
            Exiled.Events.Handlers.Player.Hurting -= ScpTeslaProtection.OnHurting;

            HudToggleService.Reset();
            HudNoticeService.Reset();

            Instance = null;

            base.OnDisabled();
        }

        private void OnRoundStarted()
        {
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
            TpsOptimizer?.SnapshotMapItems();
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
            // Если предметы спавнятся часто (более 10 за секунду) — это map editor
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