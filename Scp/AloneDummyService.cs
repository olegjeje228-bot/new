using System.Collections.Generic;
using System.Linq;
using EventHUD.Models;
using Exiled.API.Features;
using MEC;
using PlayerRoles;

namespace EventHUD.Scp
{
    public class AloneDummyService
    {
        private CoroutineHandle _loop;
        private int _dummyId = -1;

        public bool IsEnabled { get; set; } = false;

        public void Start()
        {
            _loop = Timing.RunCoroutine(CheckLoop());
        }

        public void Stop()
        {
            Timing.KillCoroutines(_loop);
            RemoveDummy();
        }

        private IEnumerator<float> CheckLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(3f);

                // Если фича отключена — удаляем дамми если есть
                if (!IsEnabled)
                {
                    if (_dummyId >= 0)
                        RemoveDummy();
                    continue;
                }

                // Проверяем — идёт ли ивент
                EventSession session = EventManager.Instance.Session;
                bool eventRunning = session.State == Enums.EventState.Running || session.State == Enums.EventState.Starting;

                // Если ивент не идёт — дамми не нужен
                if (!eventRunning)
                {
                    if (_dummyId >= 0)
                        RemoveDummy();
                    continue;
                }

                // Живые игроки (не NPC)
                List<Player> realAlive = Player.List
                    .Where(p => p != null && p.IsAlive && !p.IsNPC)
                    .ToList();

                if (realAlive.Count == 1)
                {
                    Player onlyAlive = realAlive[0];

                    // Если единственный живой — это проводящий ивента → не спавним дамми
                    if (onlyAlive.UserId == session.HostUserId)
                    {
                        if (_dummyId >= 0)
                            RemoveDummy();
                        continue;
                    }

                    // Спавним дамми только если его ещё нет
                    if (_dummyId < 0 || Player.Get(_dummyId) == null)
                        SpawnDummy(onlyAlive);
                }
                else
                {
                    if (_dummyId >= 0)
                        RemoveDummy();
                }
            }
        }

        private void SpawnDummy(Player target)
        {
            Npc dummy = Npc.Spawn("Dummy", RoleTypeId.Tutorial);
            if (dummy == null)
                return;

            _dummyId = dummy.Id;

            // Телепорт к реальному игроку
            dummy.Position = target.Position;

            // Дать бесконечное здоровье
            dummy.MaxHealth = 99999f;
            dummy.Health = 99999f;

            // Убрать все предметы
            foreach (var item in dummy.Items.ToList())
                dummy.RemoveItem(item);

            Log.Debug($"[AloneDummy] Создан dummy {dummy.Id} для {target.Nickname}");
        }

        private void RemoveDummy()
        {
            if (_dummyId < 0)
                return;

            Player dummy = Player.Get(_dummyId);
            if (dummy != null)
            {
                try { dummy.Disconnect(); } catch { }
            }

            _dummyId = -1;
            Log.Debug("[AloneDummy] Dummy удалён");
        }

        public void Reset()
        {
            RemoveDummy();
        }
    }
}