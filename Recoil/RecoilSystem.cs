using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using PlayerEvents = Exiled.Events.Handlers.Player;
using UnityEngine;

namespace EventHUD.Recoil
{
    public static class RecoilSystem
    {
        public static bool GlobalEnabled;
        public static readonly HashSet<string> Individual = new HashSet<string>();

        private static readonly System.Random Rng = new System.Random();

        // kick, side, dropChance (2-4%), slowness%
        private static readonly Dictionary<ItemType, (float kick, float side, float drop, float slow)> Weapons =
            new Dictionary<ItemType, (float, float, float, float)>
        {
            { ItemType.GunCOM15,          (1.5f, 1.0f, 0f,     4f)  },
            { ItemType.GunCOM18,          (1.5f, 1.0f, 0f,     4f)  },
            { ItemType.GunCom45,          (2.0f, 1.5f, 0f,     4f)  },
            { ItemType.GunFSP9,           (2.0f, 1.5f, 0f,     6f)  },
            { ItemType.GunCrossvec,       (2.5f, 2.0f, 0f,     7f)  },
            { ItemType.GunE11SR,          (3.5f, 2.0f, 0.02f,  10f) },
            { ItemType.GunAK,             (4.0f, 2.5f, 0.02f,  11f) },
            { ItemType.GunA7,             (4.5f, 3.0f, 0.02f,  12f) },
            { ItemType.GunFRMG0,          (4.5f, 3.0f, 0.03f,  16f) },
            { ItemType.GunRevolver,       (5.0f, 3.0f, 0.02f,  10f) },
            { ItemType.GunLogicer,        (5.0f, 3.5f, 0.03f,  14f) },
            { ItemType.GunShotgun,        (6.0f, 4.0f, 0.03f,  12f) },
            { ItemType.ParticleDisruptor, (8.0f, 5.0f, 0.04f,  12f) },
        };

        // Series tracking for drop condition
        private static readonly Dictionary<string, double> _lastShot = new();
        private static readonly Dictionary<string, double> _seriesStart = new();
        private static readonly Dictionary<string, int> _seriesShots = new();

        public static void Register() => PlayerEvents.Shooting += OnShooting;

        public static void Unregister()
        {
            PlayerEvents.Shooting -= OnShooting;
            GlobalEnabled = false;
            Individual.Clear();
            _lastShot.Clear();
            _seriesStart.Clear();
            _seriesShots.Clear();
        }

        private static float GetMultiplier(Player p)
        {
            bool byId = Individual.Contains(p.UserId);
            bool byAll = GlobalEnabled &&
                         (p.Role.Type == RoleTypeId.ClassD || p.Role.Type == RoleTypeId.Scientist ||
                          p.Role.Type == RoleTypeId.Scp049);

            if (!byId && !byAll)
                return 0f;

            if (p.Role.Type == RoleTypeId.ClassD) return 1.0f;
            if (p.Role.Type == RoleTypeId.Scp049) return 0.2f;
            return 0.6f;
        }

        private static float GetSlowMultiplier(Player p)
        {
            if (p.Role.Type == RoleTypeId.ClassD) return 1.0f;
            if (p.Role.Type == RoleTypeId.Scp049) return 0.625f;
            return 0.8f;
        }

        private static void OnShooting(ShootingEventArgs ev)
        {
            try
            {
                float mult = GetMultiplier(ev.Player);
                if (mult <= 0f || ev.Firearm == null)
                    return;

                if (!Weapons.TryGetValue(ev.Firearm.Type, out var w))
                    return;

                string uid = ev.Player.UserId;
                double now = Timing.LocalTime;

                // ── Series tracking ──
                if (_lastShot.TryGetValue(uid, out double last) && now - last < 0.7)
                {
                    _seriesShots.TryGetValue(uid, out int shots);
                    _seriesShots[uid] = shots + 1;
                }
                else
                {
                    _seriesStart[uid] = now;
                    _seriesShots[uid] = 1;
                }
                _lastShot[uid] = now;

                // ── Drop check: 1s series + at least 3 shots ──
                float seriesDuration = (float)(now - _seriesStart.GetValueOrDefault(uid, now));
                int seriesCount = _seriesShots.GetValueOrDefault(uid, 0);
                bool canDrop = seriesDuration >= 1.0f && seriesCount >= 3;

                if (canDrop && w.drop > 0f)
                {
                    float drop = w.drop * (mult >= 1f ? 1f : 0.5f);
                    if (Rng.NextDouble() < drop)
                    {
                        ev.IsAllowed = false;
                        ev.Player.DropHeldItem();
                        return;
                    }
                }

                // ── Slowness ──
                float slowMult = GetSlowMultiplier(ev.Player);
                byte intensity = (byte)Mathf.RoundToInt(w.slow * slowMult);
                if (intensity > 0)
                {
                    ev.Player.EnableEffect(EffectType.Slowness, 1.2f);
                    ev.Player.ChangeEffectIntensity(EffectType.Slowness, intensity, 1.2f);
                }

                // ── Recoil ──
                float up = w.kick * mult * (0.8f + (float)Rng.NextDouble() * 0.4f);
                float side = w.side * mult * ((float)Rng.NextDouble() * 2f - 1f);

                Vector3 currentRot = ev.Player.Rotation.eulerAngles;
                ev.Player.Rotation = Quaternion.Euler(
                    currentRot.x - up,
                    currentRot.y + side,
                    currentRot.z);
            }
            catch { }
        }
    }
}