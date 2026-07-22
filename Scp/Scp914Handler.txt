using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using UnityEngine;
using UserSettings.ServerSpecific;
using EventHUD.Hud;
using EventHUD.Medicine;
using EventHUD.Rpm;

namespace EventHUD.Scp
{
    /// <summary>
    /// SCP-914: прокачка аптечек и обработка игроков.
    /// 5 режимов: Rough, Coarse, 1:1, Fine, Very Fine.
    /// </summary>
    public class Scp914Handler
    {
        private readonly Config _config;
        private static bool _registered;

        // Отслеживание игроков в 914
        private readonly Dictionary<string, DateTime> _playersIn914 = new();

        public Scp914Handler(Config config)
        {
            _config = config;
        }

        public void RegisterSss() { }
        public void UnregisterSss() { }

        /// <summary>
        /// Обработка использования предмета в SCP-914.
        /// </summary>
        public void OnUsingItem(UsingItemEventArgs ev)
        {
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine))
                return;

            if (ev.Player == null || ev.Item == null) return;

            // Проверяем, находится ли игрок в 914
            if (!IsInScp914(ev.Player)) return;

            // Только медкиты обрабатываем
            if (ev.Item.Type != ItemType.Medkit) return;

            ev.IsAllowed = false; // Отменяем использование

            // Получаем режим 914
            Scp914Mode mode = GetScp914Mode();
            if (mode == Scp914Mode.Rough || mode == Scp914Mode.Coarse)
            {
                // Rough/Coarse: 80% смерть, 20% негативный эффект
                ProcessPlayerIn914(ev.Player, mode);
                return;
            }

            // Прокачка аптечки
            ushort serial = ev.Item.Serial;
            if (!MedkitInventoryStorage.TryGet(serial, out var kit))
            {
                // Если аптечка не зарегистрирована — создаём гражданскую
                kit = MedkitInventory.Create(MedkitType.Civilian);
                MedkitInventoryStorage.Set(serial, kit);
            }

            MedkitType result = UpgradeMedkit(kit.Type, mode);
            if (result != kit.Type)
            {
                // Заменяем тип аптечки
                MedkitInventoryStorage.Set(serial, MedkitInventory.Create(result));
                HudNoticeService.Show(ev.Player, $"<color=#00FF00>Аптечка улучшена до {result.GetDisplayName()}</color>", 2f);
            }
            else
            {
                // Изменяем содержимое
                ModifyKitContents(kit, mode);
                HudNoticeService.Show(ev.Player, $"<color=#FFAA00>Содержимое аптечки изменено</color>", 2f);
            }
        }

        /// <summary>
        /// Обработка смены роли (для отслеживания входа в 914).
        /// </summary>
        public void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (ev.Player == null) return;
            _playersIn914.Remove(ev.Player.UserId);
        }

        private bool IsInScp914(Player player)
        {
            if (player.CurrentRoom == null) return false;
            return player.CurrentRoom.Type == RoomType.Lcz914;
        }

        private Scp914Mode GetScp914Mode()
        {
            // Exiled 9.x: ищем компонент Scp914Controller на сервере
            try
            {
                var scp914Obj = GameObject.Find("SCP-914");
                if (scp914Obj == null) return Scp914Mode.OneToOne;
                var controller = scp914Obj.GetComponent(Scp914ControllerType);
                if (controller == null) return Scp914Mode.OneToOne;
                var field = controller.GetType().GetField("_knobSetting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field == null) return Scp914Mode.OneToOne;
                int val = (int)field.GetValue(controller);
                if (val == 0) return Scp914Mode.Rough;
                if (val == 1) return Scp914Mode.Coarse;
                if (val == 2) return Scp914Mode.OneToOne;
                if (val == 3) return Scp914Mode.Fine;
                if (val == 4) return Scp914Mode.VeryFine;
            }
            catch { }
            return Scp914Mode.OneToOne;
        }

        private static Type Scp914ControllerType
        {
            get
            {
                try
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var type = asm.GetType("InventorySystem.Items.Firearms.Modules.Scp914Controller");
                        if (type != null) return type;
                        type = asm.GetType("Scp914.Scp914Controller");
                        if (type != null) return type;
                        type = asm.GetType("CustomPlayerEffects.Scp914Controller");
                        if (type != null) return type;
                    }
                }
                catch { }
                return null;
            }
        }

        private MedkitType UpgradeMedkit(MedkitType current, Scp914Mode mode)
        {
            switch (mode)
            {
                case Scp914Mode.Coarse:
                    // На 1 ниже
                    return current switch
                    {
                        MedkitType.Paramedic => MedkitType.Military,
                        MedkitType.Military => MedkitType.Industrial,
                        MedkitType.Industrial => MedkitType.Civilian,
                        _ => MedkitType.Civilian
                    };

                case Scp914Mode.Fine:
                    // 50% предмет, 30% улучшение, 15% ничего, 5% даунгрейд
                    float fineRoll = UnityEngine.Random.value;
                    if (fineRoll < 0.50f) return current; // предмет
                    if (fineRoll < 0.80f) // улучшение
                    {
                        return current switch
                        {
                            MedkitType.Civilian => MedkitType.Industrial,
                            MedkitType.Industrial => MedkitType.Military,
                            MedkitType.Military => MedkitType.Paramedic,
                            _ => MedkitType.Paramedic
                        };
                    }
                    if (fineRoll < 0.95f) return current; // ничего
                    // 5% даунгрейд
                    return current switch
                    {
                        MedkitType.Paramedic => MedkitType.Military,
                        MedkitType.Military => MedkitType.Industrial,
                        MedkitType.Industrial => MedkitType.Civilian,
                        _ => MedkitType.Civilian
                    };

                case Scp914Mode.VeryFine:
                    // 20% прокачка на 1, 10% до макс, 50% обычная, 20% гражданская
                    float vfRoll = UnityEngine.Random.value;
                    if (vfRoll < 0.20f) // +1 уровень
                    {
                        return current switch
                        {
                            MedkitType.Civilian => MedkitType.Industrial,
                            MedkitType.Industrial => MedkitType.Military,
                            MedkitType.Military => MedkitType.Paramedic,
                            _ => MedkitType.Paramedic
                        };
                    }
                    if (vfRoll < 0.30f) return MedkitType.Paramedic; // макс
                    if (vfRoll < 0.80f) return current; // обычная (все вещи вернутся)
                    return MedkitType.Civilian; // гражданская

                default:
                    return current;
            }
        }

        private void ModifyKitContents(MedkitInventory kit, Scp914Mode mode)
        {
            switch (mode)
            {
                case Scp914Mode.Rough:
                case Scp914Mode.Coarse:
                    // Исчезает случайный предмет
                    if (kit.Bandages > 0) kit.Bandages--;
                    else if (kit.Tourniquets > 0) kit.Tourniquets--;
                    else if (kit.HemostaticPads > 0) kit.HemostaticPads--;
                    else if (kit.ColdPacks > 0) kit.ColdPacks--;
                    else if (kit.Splints > 0) kit.Splints--;
                    else if (kit.Antiseptic > 0) kit.Antiseptic--;
                    else if (kit.Saline > 0) kit.Saline--;
                    else if (kit.Painkillers > 0) kit.Painkillers--;
                    break;

                case Scp914Mode.OneToOne:
                    // Либо исчезает, либо появляется предмет
                    if (UnityEngine.Random.value < 0.5f)
                    {
                        // Исчезает
                        if (kit.Bandages > 0) kit.Bandages--;
                        else if (kit.Tourniquets > 0) kit.Tourniquets--;
                        else if (kit.HemostaticPads > 0) kit.HemostaticPads--;
                        else if (kit.ColdPacks > 0) kit.ColdPacks--;
                        else if (kit.Splints > 0) kit.Splints--;
                        else if (kit.Antiseptic > 0) kit.Antiseptic--;
                        else if (kit.Saline > 0) kit.Saline--;
                        else if (kit.Painkillers > 0) kit.Painkillers--;
                    }
                    else
                    {
                        // Появляется
                        kit.Bandages++;
                    }
                    break;
            }
        }

        private void ProcessPlayerIn914(Player player, Scp914Mode mode)
        {
            float deathChance = (mode == Scp914Mode.Rough || mode == Scp914Mode.Coarse) ? 0.8f : 0.5f;

            if (UnityEngine.Random.value < deathChance)
            {
                // Смерть
                player.Kill(DamageType.Crushed);
                return;
            }

            // Выжил — выдаём случайный негативный статус
            var state = MedicalStorage.GetOrCreate(player.UserId);
            var conditions = new[]
            {
                GlobalCondition.BleedingLight,
                GlobalCondition.BleedingMedium,
                GlobalCondition.BleedingHeavy,
                GlobalCondition.AdrenalineOverdose,
                GlobalCondition.PainkillerOverdose,
                GlobalCondition.CardiacArrest
            };

            var randomCondition = conditions[UnityEngine.Random.Range(0, conditions.Length)];
            state.AddCondition(randomCondition);

            // Случайная травма
            var injuries = new[]
            {
                LocalInjuryType.Bruise,
                LocalInjuryType.Gunshot,
                LocalInjuryType.Stab,
                LocalInjuryType.Chemical,
                LocalInjuryType.Burn,
                LocalInjuryType.Fracture,
                LocalInjuryType.Corrosion
            };

            var randomInjury = injuries[UnityEngine.Random.Range(0, injuries.Length)];
            var randomPart = (BodyPart)UnityEngine.Random.Range(0, 6);
            state.AddInjury(randomInjury, randomPart);

            HudNoticeService.Show(player, "<color=red>SCP-914: Вы выжили, но получили травмы!</color>", 3f);
        }

        private enum Scp914Mode
        {
            Rough,
            Coarse,
            OneToOne,
            Fine,
            VeryFine
        }
    }
}