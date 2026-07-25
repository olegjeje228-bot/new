using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using EventHUD.Audio;
using Exiled.API.Features;
using MEC;
using UnityEngine;

namespace EventHUD.Radio
{
    public sealed class RadioFmSystem
    {
        public static RadioFmSystem Instance { get; private set; }

        private readonly List<RadioUnit> radios = new List<RadioUnit>();
        private CoroutineHandle tickHandle;
        private int nextNumber = 1;

        private static Config Cfg => Plugin.Instance?.Config;

        public void Register()
        {
            Instance = this;
            tickHandle = Timing.RunCoroutine(Tick());
            Exiled.Events.Handlers.Server.RoundStarted += ClearAll;
        }

        public void Unregister()
        {
            if (tickHandle.IsRunning)
                Timing.KillCoroutines(tickHandle);

            foreach (RadioUnit u in radios.ToList())
                RadioStreamService.StopUnit(u);
            RemoveAll();
            Exiled.Events.Handlers.Server.RoundStarted -= ClearAll;
            Instance = null;
        }

        private void ClearAll()
        {
            RemoveAll();
        }

        private static object SpawnSchematic(string name, Vector3 position)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            try
            {
                Type spawnerType = Type.GetType("ProjectMER.Features.ObjectSpawner, ProjectMER");
                Type schematicType = Type.GetType("ProjectMER.Features.Objects.SchematicObject, ProjectMER");
                if (spawnerType == null || schematicType == null)
                {
                    Log.Error("[RadioFM] ProjectMER ne nayden.");
                    return null;
                }

                var method = spawnerType.GetMethod("TrySpawnSchematic",
                    new[] { typeof(string), typeof(Vector3), typeof(Quaternion), schematicType.MakeByRefType() });
                if (method == null)
                {
                    Log.Error("[RadioFM] TrySpawnSchematic ne nayden.");
                    return null;
                }

                object[] args = { name, position, Quaternion.identity, null };
                bool result = (bool)method.Invoke(null, args);
                if (!result)
                {
                    Log.Error($"[RadioFM] Ne udalos sozdat {name}.");
                    return null;
                }
                return args[3];
            }
            catch (Exception ex)
            {
                Log.Warn($"[RadioFM] Oshibka sozdaniya {name}: {ex.Message}");
                return null;
            }
        }

        private static void DestroyObject(object obj)
        {
            if (obj == null) return;
            try { obj.GetType().GetMethod("Destroy", Type.EmptyTypes)?.Invoke(obj, null); }
            catch { }
        }

        public RadioUnit Spawn(Player player)
        {
            Vector3 point = FindGround(player);

            var radio = new RadioUnit
            {
                Number = nextNumber++,
                Position = point,
                Volume = Mathf.Clamp(Cfg?.RadioFmDefaultVolume ?? 1, 0, 5),
                BatteryLeft = 100f,
                IsOn = false,
            };

            radio.Schematic = SpawnSchematic(Cfg?.RadioFmSchematicOff ?? "Radio", point);
            radios.Add(radio);

            FileLog.Write($"[Radio] Zaspavneno radio {radio.Number} v tochke {point}, komnata {(Room.Get(point)?.Type.ToString() ?? "net")}");
            return radio;
        }

        private static Vector3 FindGround(Player player)
        {
            Vector3 dir = player.ReferenceHub.PlayerCameraReference.forward;
            dir.y = 0f;
            dir = dir.sqrMagnitude < 0.001f ? Vector3.forward : dir.normalized;

            Vector3 origin = player.Position + dir * (Cfg?.RadioFmSpawnDistance ?? 1.2f);

            if (Physics.Raycast(origin + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 6f, ~0))
                return hit.point + Vector3.up * (Cfg?.RadioFmSpawnYOffset ?? 0f);

            return player.Position - Vector3.up * 0.9f;
        }

        private void UpdateSchematic(RadioUnit radio, bool? on = null)
        {
            if (radio.Schematic != null)
                DestroyObject(radio.Schematic);

            bool isOn = on ?? radio.IsOn;

            string name = isOn
                ? (Cfg?.RadioFmSchematicOn ?? "Radio2")
                : (Cfg?.RadioFmSchematicOff ?? "Radio");

            radio.Schematic = SpawnSchematic(name, radio.Position);
        }

        public bool TurnOn(RadioUnit radio, out string error)
        {
            error = null;

            if (radio.Disabled)
            {
                error = "Eto radio otklyucheno administratsiey.";
                return false;
            }

            if (radio.BatteryLeft <= 0f)
            {
                error = "Batareyka sela. Nuzhno pomenyat monetku.";
                return false;
            }

            if (radio.IsOn)
            {
                error = "Radio uzhe vklyucheno.";
                return false;
            }

            radio.IsOn = true;
            UpdateSchematic(radio, true);

            float range = GetRange(radio.Volume);
            string clipName = "radio_avtoradio";
            RadioStreamService.Play(radio, clipName, range);

            FileLog.Write($"[Radio] {radio.Number} vklyucheno, gromkost {radio.Volume}");
            return true;
        }

        public void TurnOff(RadioUnit radio)
        {
            if (!radio.IsOn)
                return;

            radio.IsOn = false;
            RadioStreamService.Stop(radio);
            UpdateSchematic(radio, false);

            FileLog.Write($"[Radio] {radio.Number} vyklyucheno");
        }

        private IEnumerator<float> Tick()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(1f);

                foreach (RadioUnit radio in radios.ToList())
                {
                    if (!radio.IsOn)
                        continue;

                    float seconds = GetBatterySeconds(radio.Volume);

                    if (seconds > 0f)
                    {
                        radio.BatteryLeft -= 100f / seconds;

                        if (radio.BatteryLeft <= 0f)
                        {
                            radio.BatteryLeft = 0f;
                            TurnOff(radio);

                            foreach (Player p in Player.List)
                            {
                                if (Vector3.Distance(p.Position, radio.Position) <= GetRange(radio.Volume))
                                    p.ShowHint("Radio zamolchalo. Batareyka sela.", 4f);
                            }
                        }
                    }
                }
            }
        }

        public float GetRange(int volume)
        {
            volume = Mathf.Clamp(volume, 0, 5);
            List<float> ranges = Cfg?.RadioFmRange;
            return (ranges != null && volume < ranges.Count) ? ranges[volume] : 15f;
        }

        public float GetBatterySeconds(int volume)
        {
            volume = Mathf.Clamp(volume, 0, 5);
            List<float> bats = Cfg?.RadioFmBatterySeconds;
            return (bats != null && volume < bats.Count) ? bats[volume] : 0f;
        }

        public float GetBatterySecondsPublic(int volume)
        {
            return GetBatterySeconds(volume);
        }

        public RadioUnit GetNearest(Vector3 position, float maxDistance)
        {
            RadioUnit closest = null;
            float minDist = maxDistance;

            foreach (RadioUnit r in radios)
            {
                float dist = Vector3.Distance(position, r.Position);
                if (dist <= minDist)
                {
                    minDist = dist;
                    closest = r;
                }
            }

            return closest;
        }

        public IReadOnlyList<RadioUnit> AllRadios => radios.AsReadOnly();

        public void SetMaxVolume(int maxVol)
        {
            if (Cfg == null) return;
            Cfg.RadioFmMaxVolume = Mathf.Clamp(maxVol, 0, 5);

            foreach (RadioUnit r in radios)
            {
                if (r.Volume > maxVol)
                {
                    r.Volume = maxVol;
                    if (r.IsOn)
                        RadioStreamService.Stop(r);
                }
            }
        }

        public void SetBaseRange(float baseRange)
        {
            if (Cfg?.RadioFmRange == null) return;

            baseRange = Mathf.Clamp(baseRange, 0f, 50f);
            float top = Mathf.Min(50f, baseRange / 15f * 40f);

            Cfg.RadioFmRange[0] = 0f;

            for (int v = 1; v <= 5; v++)
                Cfg.RadioFmRange[v] = Mathf.Lerp(baseRange, top, (v - 1) / 4f);

            foreach (RadioUnit radio in radios.Where(r => r.IsOn))
                RadioStreamService.Stop(radio);
        }

        public RadioUnit GetByNumber(int number)
        {
            return radios.FirstOrDefault(r => r.Number == number);
        }

        public int RemoveAll()
        {
            int count = radios.Count;

            foreach (RadioUnit radio in radios.ToList())
            {
                RadioStreamService.Stop(radio);
                DestroyObject(radio.Schematic);
            }

            radios.Clear();
            nextNumber = 1;

            FileLog.Write($"[Radio] Udaleni vse radio: {count}");
            return count;
        }

        public bool Delete(int number)
        {
            RadioUnit radio = GetByNumber(number);

            if (radio == null)
                return false;

            RadioStreamService.Stop(radio);
            DestroyObject(radio.Schematic);
            radios.Remove(radio);

            FileLog.Write($"[Radio] Udalen radio {number}");
            return true;
        }

        public bool SetDisabled(int number, bool disabled)
        {
            RadioUnit radio = GetByNumber(number);

            if (radio == null)
                return false;

            radio.Disabled = disabled;

            if (disabled && radio.IsOn)
                RadioStreamService.Stop(radio);

            return true;
        }

        public int SetDisabledAll(bool disabled)
        {
            foreach (RadioUnit radio in radios)
            {
                radio.Disabled = disabled;

                if (disabled && radio.IsOn)
                    RadioStreamService.Stop(radio);
            }

            return radios.Count;
        }

        public bool ChangeBattery(int number)
        {
            RadioUnit radio = GetByNumber(number);

            if (radio == null)
                return false;

            radio.BatteryLeft = 100f;
            return true;
        }

        public int ChangeBatteryAll()
        {
            foreach (RadioUnit radio in radios)
                radio.BatteryLeft = 100f;

            return radios.Count;
        }

        public int ClampAllVolumes(int max)
        {
            int lowered = 0;

            foreach (RadioUnit radio in radios)
            {
                if (radio.Volume <= max)
                    continue;

                radio.Volume = max;
                lowered++;

                if (radio.IsOn)
                    RadioStreamService.Stop(radio);
            }

            return lowered;
        }

        public string BuildList()
        {
            if (radios.Count == 0)
                return "Radio net. Zaspavnit: radiofm spawn";

            var sb = new StringBuilder();
            sb.AppendLine($"Radio: {radios.Count}");

            foreach (RadioUnit radio in radios.OrderBy(r => r.Number))
            {
                string state = radio.Disabled
                    ? "otklyucheno adminom"
                    : radio.IsOn ? "igraet" : "vyklyucheno";

                Room room = Room.Get(radio.Position);
                string place = room != null ? room.Type.ToString() : "neizvestno";

                sb.AppendLine(
                    $"{radio.Number}. {state}, gromkost {radio.Volume}, " +
                    $"zaryad {radio.BatteryLeft:0}%, slyshno {GetRange(radio.Volume):0} m, {place}");
            }

            return sb.ToString();
        }
    }
}