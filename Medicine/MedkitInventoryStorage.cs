using System.Collections.Generic;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Хранилище содержимого аптечек. Ключ — serial предмета.
    /// При спавне/подборе помечается типом по роли/месту.
    /// </summary>
    public static class MedkitInventoryStorage
    {
        private static readonly Dictionary<ushort, MedkitInventory> _kits = new();

        public static MedkitInventory GetOrCreate(ushort serial, MedkitType defaultType = MedkitType.Civilian)
        {
            if (!_kits.TryGetValue(serial, out var inv))
            {
                inv = MedkitInventory.Create(defaultType);
                _kits[serial] = inv;
            }
            return inv;
        }

        public static bool TryGet(ushort serial, out MedkitInventory inv) =>
            _kits.TryGetValue(serial, out inv);

        public static void Set(ushort serial, MedkitInventory inv) =>
            _kits[serial] = inv;

        public static void Remove(ushort serial) =>
            _kits.Remove(serial);

        public static void ClearAll() => _kits.Clear();
    }
}
 