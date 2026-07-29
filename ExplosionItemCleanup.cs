using System;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Map;
using MapEvents = Exiled.Events.Handlers.Map;

namespace EventHUD
{
    /// <summary>
    /// Анти-лаг: при взрыве осколочной гранаты убирает часть валяющихся предметов,
    /// чтобы физика взрыва не расшвыривала сотни объектов.
    /// 100+ предметов -> удаляем 50%, 250+ -> 75%, 500+ -> 90%.
    /// </summary>
    public static class ExplosionItemCleanup
    {
        private static readonly Random Rng = new Random();

        public static void Register() => MapEvents.ExplodingGrenade += OnExplodingGrenade;

        public static void Unregister() => MapEvents.ExplodingGrenade -= OnExplodingGrenade;

        private static void OnExplodingGrenade(ExplodingGrenadeEventArgs ev)
        {
            try
            {
                // Только осколочная (флешки и SCP-018 не трогаем)
                if (ev.Projectile == null || ev.Projectile.Type != ItemType.GrenadeHE)
                    return;

                var pickups = Pickup.List.ToList();
                int total = pickups.Count;

                float fraction;
                if (total >= 500)
                    fraction = 0.90f;
                else if (total >= 250)
                    fraction = 0.75f;
                else if (total >= 100)
                    fraction = 0.50f;
                else
                    return;

                int toRemove = (int)(total * fraction);

                // Перемешиваем, чтобы удалялись случайные, а не первые попавшиеся
                for (int i = pickups.Count - 1; i > 0; i--)
                {
                    int j = Rng.Next(i + 1);
                    var tmp = pickups[i];
                    pickups[i] = pickups[j];
                    pickups[j] = tmp;
                }

                int removed = 0;
                for (int i = 0; i < toRemove && i < pickups.Count; i++)
                {
                    try
                    {
                        pickups[i].Destroy();
                        removed++;
                    }
                    catch
                    {
                        // отдельный битый пикап не должен ломать чистку
                    }
                }

                Log.Info($"[ItemCleanup] Взрыв гранаты: предметов на карте {total}, удалено {removed}");
            }
            catch (Exception e)
            {
                Log.Warn($"[ItemCleanup] Ошибка: {e.Message}");
            }
        }
    }
}