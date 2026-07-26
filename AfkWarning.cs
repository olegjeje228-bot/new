using System;
using System.Collections.Generic;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using UnityEngine;
using PlayerEvents = Exiled.Events.Handlers.Player;

namespace EventHUD
{
    /// <summary>Предупреждение об АФК: 5 минут без движения (0.3м) — бродкаст с обратным отсчётом до кика (кик в игре на 10 мин).</summary>
    public static class AfkWarning
    {
        private const float WarnAfterSeconds = 300f;
        private const float KickAtSeconds = 600f;
        private const float MoveThreshold = 0.3f;

        private class State { public Vector3 LastPos; public double IdleSince; }

        private static readonly Dictionary<string, State> States = new Dictionary<string, State>();
        private static CoroutineHandle _loop;

        public static void Register()
        {
            PlayerEvents.Left += OnLeft;
            if (!_loop.IsRunning)
                _loop = Timing.RunCoroutine(Loop());
        }

        public static void Unregister()
        {
            PlayerEvents.Left -= OnLeft;
            if (_loop.IsRunning)
                Timing.KillCoroutines(_loop);
            States.Clear();
        }

        private static void OnLeft(Exiled.Events.EventArgs.Player.LeftEventArgs ev) =>
            States.Remove(ev.Player.UserId);

        private static IEnumerator<float> Loop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(1f);
                try { Tick(); } catch { }
            }
        }

        private static void Tick()
        {
            double now = Timing.LocalTime;

            foreach (Player player in Player.List)
            {
                // 079 не двигается, мёртвые/наблюдатели не в счёт
                if (player.IsNPC || player.IsDead ||
                    player.Role.Type == RoleTypeId.Scp079 || player.Role.Type == RoleTypeId.Overwatch)
                {
                    States.Remove(player.UserId);
                    continue;
                }

                if (!States.TryGetValue(player.UserId, out State st))
                {
                    States[player.UserId] = new State { LastPos = player.Position, IdleSince = now };
                    continue;
                }

                // Сдвинулся на 0.3м в любую сторону — таймер снимается
                if (Vector3.Distance(player.Position, st.LastPos) >= MoveThreshold)
                {
                    st.LastPos = player.Position;
                    st.IdleSince = now;
                    continue;
                }

                double idle = now - st.IdleSince;
                if (idle >= WarnAfterSeconds)
                {
                    int left = Math.Max(0, (int)(KickAtSeconds - idle));
                    player.Broadcast(1, $"<color=red> AFK </color>Вы будете кикнуты через {left} секунд, подвигайтесь.",
                        global::Broadcast.BroadcastFlags.Normal, true);
                }
            }
        }
    }
}