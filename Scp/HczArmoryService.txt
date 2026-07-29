using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using EventHUD.Medicine;
using MEC;

namespace EventHUD.Scp
{
    public class HczArmoryService
    {
        private bool _spawned;

        public void Start()
        {
            Timing.CallDelayed(3f, SpawnMilitaryMedkit);
        }

        public void Reset()
        {
            _spawned = false;
        }

        private void SpawnMilitaryMedkit()
        {
            if (_spawned)
                return;

            Room armory = Room.Get(RoomType.HczArmory);
            if (armory == null)
            {
                Log.Warn("[HczArmory] Комната HczArmory не найдена.");
                return;
            }

            // Создаём обычный Medkit
            Item medkit = Item.Create(ItemType.Medkit);
            if (medkit == null)
                return;

            ushort serial = medkit.Serial;

            // Регистрируем как военный
            MedkitInventoryStorage.GetOrCreate(serial, MedkitType.Military);

            // Спавним в центре комнаты с небольшим смещением вверх
            medkit.CreatePickup(armory.Position + UnityEngine.Vector3.up * 0.5f);

            _spawned = true;
            Log.Debug("[HczArmory] Военная аптечка заспавнена в HczArmory.");
        }
    }
}