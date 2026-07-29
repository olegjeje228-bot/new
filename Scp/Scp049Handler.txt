using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Roles;
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
    /// Чумка SCP-049: подбор предметов с прогресс-баром (один за раз),
    /// меню выбора (← → Enter, как у аптечки) и быстрые слоты:
    /// Ctrl - карточка, X - аптечка, G - граната, 1/2 - оружие.
    /// Макс. оружия: 1 (2 если есть бронежилет).
    /// </summary>
    public class Scp049Handler
    {
        private readonly Config _config;
        private static bool _registered;

        private static int _pickupKeybindId  = 9030;
        private static int _keycardKeybindId = 9031;
        private static int _medkitKeybindId  = 9032;
        private static int _grenadeKeybindId = 9033;
        private static int _weapon1KeybindId = 9034;
        private static int _weapon2KeybindId = 9035;

        // Навигация меню — те же клавиши, что у меню аптечки
        private const int MenuLeftId  = 9010;
        private const int MenuRightId = 9011;
        private const int MenuEnterId = 9012;

        private class PickupState
        {
            public bool IsPicking;
            public float Elapsed;
            public float Duration;
            public string ItemName;
        }

        private class MenuState
        {
            public bool IsOpen;
            public int SelectedIndex;
        }

        private readonly Dictionary<string, List<ItemType>> _inventory = new();
        private readonly Dictionary<string, PickupState> _pickups = new();
        private readonly Dictionary<string, MenuState> _menus = new();
        private readonly Dictionary<string, ushort> _equippedSerial = new();
        private readonly Dictionary<string, int> _equippedIndex = new();
        private CoroutineHandle _abilityLoop;

        public Scp049Handler(Config config) => _config = config;

        public void Start()
        {
            _abilityLoop = Timing.RunCoroutine(AbilityLoop());
        }

        public void Stop()
        {
            Timing.KillCoroutines(_abilityLoop);
        }

        private IEnumerator<float> AbilityLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(0.2f);

                if (!RpModuleManager.Instance.IsEnabled(
                        RpModuleType.Medicine))
                {
                    continue;
                }

                foreach (Player player in Player.List)
                {
                    if (player == null || !player.IsAlive)
                        continue;

                    if (!(player.Role is Scp049Role scp049))
                        continue;

                    Scp049ConditionService.Tick(player, 0.2f);

                    // Если ранение - принудительно завершаем способность
                    if (Scp049ConditionService.IsAbilityBlocked(player))
                    {
                        if (scp049.IsCallActive)
                            scp049.RemainingCallDuration = 0.1f;

                        continue;
                    }

                    // Не трогаем способность в цикле.
                    // Она запускается только нажатием F и живёт стандартное время.
                    // Не продлеваем RemainingCallDuration, не сбрасываем CallCooldown.
                }
            }
        }

        // ═════════════ SSS ═════════════

        public void RegisterSss()
        {
            if (_registered) return;
            _registered = true;

            var pickup = new SSKeybindSetting(_pickupKeybindId, "Взять предмет (SCP-049)", UnityEngine.KeyCode.E);
            var keycard = new SSKeybindSetting(_keycardKeybindId, "Карточка (SCP-049)", UnityEngine.KeyCode.LeftControl);
            var medkit = new SSKeybindSetting(_medkitKeybindId, "Аптечка (SCP-049)", UnityEngine.KeyCode.X);
            var grenade = new SSKeybindSetting(_grenadeKeybindId, "Граната (SCP-049)", UnityEngine.KeyCode.G);
            var weapon1 = new SSKeybindSetting(_weapon1KeybindId, "Оружие 1 (SCP-049)", UnityEngine.KeyCode.Alpha1);
            var weapon2 = new SSKeybindSetting(_weapon2KeybindId, "Оружие 2 (SCP-049)", UnityEngine.KeyCode.Alpha2);

            SssRoleSync.Scp049Settings.Add(pickup);
            SssRoleSync.Scp049Settings.Add(keycard);
            SssRoleSync.Scp049Settings.Add(medkit);
            SssRoleSync.Scp049Settings.Add(grenade);
            SssRoleSync.Scp049Settings.Add(weapon1);
            SssRoleSync.Scp049Settings.Add(weapon2);

            ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnSettingChanged;
        }

        public void UnregisterSss()
        {
            if (!_registered) return;
            _registered = false;
            ServerSpecificSettingsSync.ServerOnSettingValueReceived -= OnSettingChanged;
        }

        private void OnSettingChanged(ReferenceHub hub, ServerSpecificSettingBase setting)
        {
            int id = setting.SettingId;
            bool ours = id == _pickupKeybindId || id == _keycardKeybindId || id == _medkitKeybindId ||
                        id == _grenadeKeybindId || id == _weapon1KeybindId || id == _weapon2KeybindId ||
                        id == MenuLeftId || id == MenuRightId || id == MenuEnterId;
            if (!ours) return;
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine)) return;
            if (setting is not SSKeybindSetting keybind || !keybind.SyncIsPressed) return;

            var player = Player.Get(hub);
            if (player == null || !player.IsAlive) return;
            if (player.Role.Type != RoleTypeId.Scp049) return;

            if (id == _pickupKeybindId)  { TryStartPickup(player); return; }
            if (id == MenuLeftId)        { NavigateMenu(player, -1); return; }
            if (id == MenuRightId)       { NavigateMenu(player, 1); return; }
            if (id == MenuEnterId)       { MenuEnter(player); return; }
            if (id == _keycardKeybindId) { QuickToggle(player, t => t.ToString().StartsWith("Keycard"), 0); return; }
            if (id == _medkitKeybindId)  { QuickToggle(player, t => t == ItemType.Medkit, 0); return; }
            if (id == _grenadeKeybindId) { QuickToggle(player, t => t == ItemType.GrenadeHE || t == ItemType.GrenadeFlash, 0); return; }
            if (id == _weapon1KeybindId) { QuickToggle(player, IsWeaponItem, 0); return; }
            if (id == _weapon2KeybindId) { QuickToggle(player, IsWeaponItem, 1); return; }
        }

        // ═════════════ Подбор ═════════════

        private void TryStartPickup(Player player)
        {
            string uid = player.UserId;
            var ps = GetPickupState(uid);

            // Только один предмет за раз
            if (ps.IsPicking)
            {
                HudNoticeService.Show(player, "<color=red>Вы уже берёте предмет</color>", 1f);
                return;
            }

            Pickup targetPickup = GetPickupInCrosshair(player);
            if (targetPickup == null)
            {
                HudNoticeService.Show(player, "<color=red>Нет предмета в прицеле</color>", 1f);
                return;
            }

            var inv = GetInventory(uid);

            // Нельзя взять второй такой же предмет (патроны стакать можно)
            if (!IsAmmoType(targetPickup.Type) && inv != null && inv.Count > 0)
            {
                if (inv.Contains(targetPickup.Type))
                {
                    HudNoticeService.Show(player, "<color=red>У вас уже есть такой предмет</color>", 1.5f);
                    return;
                }
            }

            // Лимиты
            if (IsAmmoType(targetPickup.Type))
            {
                if (inv.Count(t => IsAmmoType(t)) >= 5)
                {
                    HudNoticeService.Show(player, "<color=red>Макс. 5 пачек патронов</color>", 1f);
                    return;
                }
            }
            else if (IsWeaponItem(targetPickup.Type))
            {
                int maxWeapons = HasArmor(player) ? 2 : 1;
                if (inv.Count(t => IsWeaponItem(t)) >= maxWeapons)
                {
                    HudNoticeService.Show(player, $"<color=red>Макс. {maxWeapons} оружия</color>", 1.5f);
                    return;
                }
                if (inv.Count >= 8)
                {
                    HudNoticeService.Show(player, "<color=red>Макс. 8 предметов</color>", 1f);
                    return;
                }
            }
            else if (inv.Count >= 8)
            {
                HudNoticeService.Show(player, "<color=red>Макс. 8 предметов</color>", 1f);
                return;
            }

            ps.IsPicking = true;
            ps.Elapsed = 0f;
            ps.Duration = GetPickupTime(targetPickup.Type);
            ps.ItemName = ShortName(targetPickup.Type);

            Timing.RunCoroutine(PickupCoroutine(player, ps, targetPickup));
        }

        private IEnumerator<float> PickupCoroutine(Player player, PickupState ps, Pickup target)
        {
            Vector3 itemPos = target.Position;

            while (ps.Elapsed < ps.Duration)
            {
                yield return Timing.WaitForSeconds(0.25f);
                ps.Elapsed += 0.25f;

                if (player == null || !player.IsAlive || player.Role.Type != RoleTypeId.Scp049)
                {
                    ps.IsPicking = false;
                    yield break;
                }
                if (target == null)
                {
                    ps.IsPicking = false;
                    HudNoticeService.Show(player, "<color=red>Предмет пропал</color>", 1f);
                    yield break;
                }
                if (Vector3.Distance(player.Position, itemPos) > 4f)
                {
                    ps.IsPicking = false;
                    HudNoticeService.Show(player, "<color=red>Вы отошли — подбор прерван</color>", 1.5f);
                    yield break;
                }
            }

            ps.IsPicking = false;
            if (target == null) yield break;

            var inv = GetInventory(player.UserId);
            inv.Add(target.Type);
            try { target.Destroy(); } catch { }
            HudNoticeService.Show(player, $"<color=#00FF00>Предмет {ps.ItemName} взят</color>", 1f);
        }

        private Pickup GetPickupInCrosshair(Player player)
        {
            try
            {
                // ВАЖНО: берём камеру, а не Transform тела.
                // Transform.forward — горизонтальное направление, а предмет лежит на полу,
                // игрок смотрит вниз — поэтому старый код никогда его не находил.
                Transform cam = player.CameraTransform;
                Vector3 origin  = cam != null ? cam.position : player.Position + Vector3.up * 1.5f;
                Vector3 forward = cam != null ? cam.forward  : player.Transform.forward;

                float maxDist = 5f;

                Pickup best = null;
                float bestDot = 0.35f; // порог «в прицеле», мягче старого 0.5

                Pickup nearest = null;
                float nearestDist = 2.2f; // запасной вариант: предмет прямо под ногами

                foreach (var pickup in Pickup.List)
                {
                    if (pickup == null) continue;

                    float dist = Vector3.Distance(origin, pickup.Position);
                    if (dist > maxDist) continue;

                    Vector3 dir = (pickup.Position - origin).normalized;
                    float dot = Vector3.Dot(forward, dir);
                    if (dot > bestDot)
                    {
                        bestDot = dot;
                        best = pickup;
                    }

                    float distToFeet = Vector3.Distance(player.Position, pickup.Position);
                    if (distToFeet < nearestDist)
                    {
                        nearestDist = distToFeet;
                        nearest = pickup;
                    }
                }

                // Если ничего нет строго в прицеле — берём ближайший предмет рядом с ногами.
                return best ?? nearest;
            }
            catch { return null; }
        }

        private float GetPickupTime(ItemType type)
        {
            if (type == ItemType.Coin) return 0.4f;
            if (type == ItemType.Flashlight) return 0.5f;
            if (IsAmmoType(type)) return 0.5f;
            if (type.ToString().StartsWith("Keycard")) return 0.8f;
            if (type == ItemType.Radio) return 1f;
            if (type == ItemType.Medkit) return 1.5f;
            if (type == ItemType.Adrenaline || type == ItemType.Painkillers || type == ItemType.SCP500) return 0.8f;
            if (IsWeaponItem(type)) return 2.5f;
            if (type == ItemType.MicroHID) return 4f;
            return 1f;
        }

        // ═════════════ Меню и экипировка ═════════════

        private void NavigateMenu(Player player, int direction)
        {
            var menu = GetMenu(player.UserId);
            var inv = GetInventory(player.UserId);
            int total = inv.Count + 1; // + [Назад]

            if (!menu.IsOpen)
            {
                menu.IsOpen = true;
                menu.SelectedIndex = 0;
                return;
            }

            menu.SelectedIndex += direction;
            if (menu.SelectedIndex < 0) menu.SelectedIndex = total - 1;
            if (menu.SelectedIndex >= total) menu.SelectedIndex = 0;
        }

        private void MenuEnter(Player player)
        {
            var menu = GetMenu(player.UserId);
            if (!menu.IsOpen) return;

            var inv = GetInventory(player.UserId);

            // [Назад] — выйти из меню обратно к инвентарю
            if (menu.SelectedIndex >= inv.Count)
            {
                menu.IsOpen = false;
                HudNoticeService.Show(player, "<color=#AAAAAA>[ Обратно ]</color>", 1f);
                return;
            }

            ToggleEquipAt(player, menu.SelectedIndex);
        }

        private void QuickToggle(Player player, Func<ItemType, bool> predicate, int occurrence)
        {
            var inv = GetInventory(player.UserId);
            int seen = 0;
            for (int i = 0; i < inv.Count; i++)
            {
                if (!predicate(inv[i])) continue;
                if (seen == occurrence)
                {
                    ToggleEquipAt(player, i);
                    return;
                }
                seen++;
            }
            HudNoticeService.Show(player, "<color=red>Нет такого предмета в чумке</color>", 1f);
        }

        public void OnDroppingItem(DroppingItemEventArgs ev)
        {
            if (ev.Player == null || ev.Item == null)
                return;

            if (ev.Player.Role.Type != RoleTypeId.Scp049)
                return;

            string uid = ev.Player.UserId;

            // Реальный предмет, созданный из виртуального инвентаря 049.
            if (!_equippedSerial.TryGetValue(uid, out ushort serial) || serial != ev.Item.Serial)
                return;

            if (!_equippedIndex.TryGetValue(uid, out int index))
                return;

            var inventory = GetInventory(uid);

            // Игрок выбросил предмет — он остаётся на карте,
            // но пропадает из виртуального инвентаря чумного доктора.
            if (index >= 0 && index < inventory.Count)
                inventory.RemoveAt(index);

            _equippedSerial.Remove(uid);
            _equippedIndex.Remove(uid);

            var menu = GetMenu(uid);
            if (menu.SelectedIndex >= inventory.Count)
                menu.SelectedIndex = Math.Max(0, inventory.Count - 1);

            HudNoticeService.Show(ev.Player,
                $"<color=yellow>Выброшено: {ShortName(ev.Item.Type)} ",
                1.5f);
        }

        private void ToggleEquipAt(Player player, int index)
        {
            string uid = player.UserId;
            var inv = GetInventory(uid);
            if (index < 0 || index >= inv.Count) return;

            // MicroHID уже в руках — нельзя переключиться на другой предмет
            if (_equippedIndex.TryGetValue(uid, out int currentIndex) &&
                currentIndex >= 0 &&
                currentIndex < inv.Count &&
                inv[currentIndex] == ItemType.MicroHID &&
                currentIndex != index)
            {
                HudNoticeService.Show(
                    player,
                    "<color=red>MicroHID уже в руках. Сначала выбросьте его. ",
                    2f);

                return;
            }

            // Этот предмет уже в руках — убираем (жёлтый)
            if (_equippedIndex.TryGetValue(uid, out int eqIdx) && eqIdx == index)
            {
                UnequipCurrent(player);
                HudNoticeService.Show(player, $"<color=yellow>Убрано: {ShortName(inv[index])}</color>", 1.5f);
                return;
            }

            // Снимаем предыдущее и force equip новое (зелёный)
            UnequipCurrent(player);

            ItemType type = inv[index];
            try
            {
                Item item = player.AddItem(type);
                if (item == null)
                {
                    HudNoticeService.Show(player, "<color=red>Не удалось взять в руки</color>", 1.5f);
                    return;
                }

                _equippedSerial[uid] = item.Serial;
                _equippedIndex[uid] = index;

                Timing.CallDelayed(0.15f, () =>
                {
                    if (player == null || !player.IsAlive || player.Role.Type != RoleTypeId.Scp049) return;
                    try { player.CurrentItem = item; } catch { }
                });

                HudNoticeService.Show(player, $"<color=#00FF00>В руках: {ShortName(type)}</color>", 1.5f);
            }
            catch
            {
                HudNoticeService.Show(player, "<color=red>Не удалось взять в руки</color>", 1.5f);
            }
        }

        private void UnequipCurrent(Player player)
        {
            string uid = player.UserId;
            if (!_equippedSerial.TryGetValue(uid, out ushort serial))
            {
                _equippedIndex.Remove(uid);
                return;
            }
            _equippedSerial.Remove(uid);
            _equippedIndex.Remove(uid);
            try
            {
                var item = player.Items.FirstOrDefault(i => i != null && i.Serial == serial);
                if (item != null) player.RemoveItem(item);
            }
            catch { }
        }

        // ═════════════ HUD ═════════════

        public string BuildHud(Player player, float voffset)
        {
            if (player == null || player.Role.Type != RoleTypeId.Scp049)
                return string.Empty;

            string uid = player.UserId;
            _inventory.TryGetValue(uid, out var inv);
            bool hasEquipped = _equippedIndex.TryGetValue(uid, out int eqIdx);

            var sb = new StringBuilder();
            sb.Append($"<voffset={voffset}em><indent=-29.5%><color=#888888>Чумка: [</color>");

            for (int i = 0; i < 8; i++)
            {
                if (i > 0) sb.Append(" ");
                if (inv != null && i < inv.Count)
                {
                    string name = ShortName(inv[i]);
                    if (name.Length > 6) name = name.Substring(0, 6);
                    string clr = (hasEquipped && i == eqIdx) ? "#00FF00" : "#AAFFAA";
                    sb.Append($"<color={clr}>{name}</color>");
                }
                else
                {
                    sb.Append("<color=#444444>--</color>");
                }
            }

            sb.Append("<color=#888888>]</color>");

            if (inv != null)
            {
                int ammoCount = inv.Count(t => IsAmmoType(t));
                if (ammoCount > 0)
                    sb.Append($" <color=#888888>П:{ammoCount}/5</color>");
            }

            float line = voffset - 1f;

            // Прогресс-бар подбора (как у лечения аптечкой)
            if (_pickups.TryGetValue(uid, out var ps) && ps.IsPicking)
            {
                float progress = Mathf.Clamp01(ps.Elapsed / Mathf.Max(ps.Duration, 0.1f));
                int filled = (int)(progress * 10);
                string bar = new string('█', filled) + new string('░', 10 - filled);
                sb.Append($"<voffset={line}em><indent=-29.5%><color=yellow>Вы берете предмет: {ps.ItemName} {bar} {ps.Elapsed:0.#}/{ps.Duration:0.#}с</color>");
                line -= 1f;
            }

            // Меню выбора предмета
            if (_menus.TryGetValue(uid, out var menu) &&
                menu.IsOpen &&
                inv != null)
            {
                sb.Append(
                    $"<voffset={line}em><indent=-29.5%>");

                sb.Append(
                    BuildMenuLine(
                        inv,
                        menu.SelectedIndex,
                        hasEquipped ? eqIdx : -1));

                line -= 1f;
            }

            string conditionLine =
                Scp049ConditionService.GetHudLine(player);

            if (!string.IsNullOrEmpty(conditionLine))
            {
                sb.Append(
                    $"<voffset={line}em><indent=-29.5%>");

                sb.Append(conditionLine);
            }

            return sb.ToString();
        }

        private string BuildMenuLine(List<ItemType> inv, int selected, int equippedIndex)
        {
            int total = inv.Count + 1;
            if (selected < 0 || selected >= total) selected = 0;

            var indices = new List<int>();
            if (total <= 3)
            {
                for (int i = 0; i < total; i++) indices.Add(i);
            }
            else
            {
                indices.Add((selected - 1 + total) % total);
                indices.Add(selected);
                indices.Add((selected + 1) % total);
            }

            var sb = new StringBuilder();
            for (int k = 0; k < indices.Count; k++)
            {
                if (k > 0) sb.Append(" <color=#555555>|</color> ");
                int i = indices[k];
                bool isSel = i == selected;

                if (i == inv.Count)
                {
                    string backClr = isSel ? "#FFFFFF" : "#AA4444";
                    sb.Append($"<color={backClr}>[Назад]</color>");
                    continue;
                }

                string name = ShortName(inv[i]);
                string color;
                if (i == equippedIndex) color = "#00FF00";      // экипирован — зелёный
                else if (isSel)         color = "#FFFFFF";      // курсор — белый
                else                    color = "#FF5555";      // не выбран — красный
                string marker = isSel ? "›" : "";
                sb.Append($"<color={color}>{marker}{name}</color>");
            }
            return sb.ToString();
        }

        // ═════════════ Хелперы ═════════════

        private List<ItemType> GetInventory(string uid)
        {
            if (!_inventory.TryGetValue(uid, out var inv))
            {
                inv = new List<ItemType>();
                _inventory[uid] = inv;
            }
            return inv;
        }

        private PickupState GetPickupState(string uid)
        {
            if (!_pickups.TryGetValue(uid, out var ps))
            {
                ps = new PickupState();
                _pickups[uid] = ps;
            }
            return ps;
        }

        private MenuState GetMenu(string uid)
        {
            if (!_menus.TryGetValue(uid, out var m))
            {
                m = new MenuState();
                _menus[uid] = m;
            }
            return m;
        }

        private bool HasArmor(Player player) =>
            ArmorStorage.TryGet(player.UserId, out var armor) && armor.Type != ArmorType.None;

        private string ShortName(ItemType type) =>
            type.ToString().Replace("Gun", "").Replace("Keycard", "Key");

        private bool IsAmmoType(ItemType type) =>
            type == ItemType.Ammo556x45 || type == ItemType.Ammo762x39 ||
            type == ItemType.Ammo9x19 || type == ItemType.Ammo12gauge || type == ItemType.Ammo44cal;

        private bool IsWeaponItem(ItemType type) =>
            type.ToString().StartsWith("Gun") || type == ItemType.MicroHID || type == ItemType.Jailbird;

        public void ClearPlayer(string userId)
        {
            _inventory.Remove(userId);
            _pickups.Remove(userId);
            _menus.Remove(userId);
            _equippedSerial.Remove(userId);
            _equippedIndex.Remove(userId);
        }

        public void ResetAll()
        {
            _inventory.Clear();
            _pickups.Clear();
            _menus.Clear();
            _equippedSerial.Clear();
            _equippedIndex.Clear();
        }
    }
}