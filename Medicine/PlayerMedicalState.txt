using System;
using System.Collections.Generic;
using System.Linq;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Полное медицинское состояние одного игрока.
    /// </summary>
    public class PlayerMedicalState
    {
        // ── Глобальные состояния ──
        public HashSet<GlobalCondition> Conditions { get; } = new HashSet<GlobalCondition>();

        // ── Локальные травмы (части тела) ──
        public List<LocalInjury> Injuries { get; } = new List<LocalInjury>();

        // ── Счётчики медикаментов за текущую жизнь ──
        public int AdrenalineUsed  { get; set; }
        public int PainkillerUsed  { get; set; }

        // ── Таймеры кровотечений ──
        /// <summary>Время начала текущего кровотечения (для фазы бурного урона).</summary>
        public DateTime BleedingStartedAt { get; set; }
        /// <summary>true = бурная фаза кровотечения закончилась, идёт постоянный слабый урон.</summary>
        public bool BleedingBurstFinished { get; set; }

        // ── Таймер коррозии SCP-106 ──
        /// <summary>Время, когда 106 начал стоять рядом. null = не рядом.</summary>
        public DateTime? CorrosionProximityStart { get; set; }

        // ── Таймер SCP-244 ──
        public DateTime? Scp244ProximityStart { get; set; }

        // ── Первая помощь (временная повязка) ──
        public bool FirstAidUsed { get; set; }
        public DateTime FirstAidExpiresAt { get; set; }
        public GlobalCondition? FirstAidOriginalBleeding { get; set; }

        /// <summary>
        /// Есть ли хоть одно активное состояние (кроме Normal).
        /// </summary>
        public bool HasAnyCondition =>
            Conditions.Count > 0 && !(Conditions.Count == 1 && Conditions.Contains(GlobalCondition.Normal));

        /// <summary>
        /// Есть ли хоть одна травма.
        /// </summary>
        public bool HasAnyInjury => Injuries.Count > 0;

        /// <summary>
        /// Есть ли что-то для отображения в HUD.
        /// </summary>
        public bool HasAnything => HasAnyCondition || HasAnyInjury;

        // ── Методы ──

        public void AddCondition(GlobalCondition condition)
        {
            // Normal убирается при добавлении любого другого состояния
            if (condition != GlobalCondition.Normal)
                Conditions.Remove(GlobalCondition.Normal);

            Conditions.Add(condition);
        }

        public void RemoveCondition(GlobalCondition condition)
        {
            Conditions.Remove(condition);

            if (Conditions.Count == 0)
                Conditions.Add(GlobalCondition.Normal);

            // Если убрали кровотечение — сбрасываем первую помощь
            if (condition == GlobalCondition.BleedingLight ||
                condition == GlobalCondition.BleedingMedium ||
                condition == GlobalCondition.BleedingHeavy)
            {
                if (GetBleedingLevel() == null)
                {
                    FirstAidUsed = false;
                    FirstAidExpiresAt = default;
                    FirstAidOriginalBleeding = null;
                }
            }
        }

        public bool HasCondition(GlobalCondition condition) =>
            Conditions.Contains(condition);

        public void AddInjury(LocalInjuryType type, BodyPart part)
        {
            // Не дублируем одинаковую травму на одной части тела
            if (Injuries.Any(i => i.Type == type && i.Part == part))
                return;

            Injuries.Add(new LocalInjury(type, part));
        }

        public void RemoveInjury(LocalInjuryType type, BodyPart part)
        {
            Injuries.RemoveAll(i => i.Type == type && i.Part == part);
        }

        public bool HasInjury(LocalInjuryType type, BodyPart part) =>
            Injuries.Any(i => i.Type == type && i.Part == part);

        public bool HasInjuryType(LocalInjuryType type) =>
            Injuries.Any(i => i.Type == type);

        /// <summary>
        /// Полный сброс — смерть / новый раунд.
        /// </summary>
        public void Reset()
        {
            Conditions.Clear();
            Conditions.Add(GlobalCondition.Normal);
            Injuries.Clear();
            AdrenalineUsed  = 0;
            PainkillerUsed  = 0;
            BleedingStartedAt   = default;
            BleedingBurstFinished = false;
            CorrosionProximityStart = null;
            Scp244ProximityStart   = null;
            FirstAidUsed = false;
            FirstAidExpiresAt = default;
            FirstAidOriginalBleeding = null;
        }

        /// <summary>
        /// Получить текущий уровень кровотечения (самый тяжёлый).
        /// </summary>
        public GlobalCondition? GetBleedingLevel()
        {
            if (HasCondition(GlobalCondition.BleedingHeavy))  return GlobalCondition.BleedingHeavy;
            if (HasCondition(GlobalCondition.BleedingMedium)) return GlobalCondition.BleedingMedium;
            if (HasCondition(GlobalCondition.BleedingLight))  return GlobalCondition.BleedingLight;
            return null;
        }

        /// <summary>
        /// Повысить уровень кровотечения. Если уже артериальное — ничего не делает.
        /// </summary>
        public void EscalateBleeding(GlobalCondition newLevel)
        {
            var current = GetBleedingLevel();

            // Убираем предыдущий уровень
            if (current.HasValue)
            {
                if ((int)newLevel <= (int)current.Value)
                    return; // Уже равный или более тяжёлый уровень

                Conditions.Remove(current.Value);
            }

            AddCondition(newLevel);
            BleedingStartedAt     = DateTime.UtcNow;
            BleedingBurstFinished = false;
        }

        /// <summary>
        /// Первая помощь: понизить уровень кровотечения на 1 ступень на 60 сек.
        /// Тратит 1 бинт. Одноразово до полного излечения.
        /// </summary>
        public bool TryApplyFirstAid(out GlobalCondition? originalLevel)
        {
            originalLevel = GetBleedingLevel();
            if (!originalLevel.HasValue) return false;
            if (FirstAidUsed) return false;

            GlobalCondition? downgraded = originalLevel.Value switch
            {
                GlobalCondition.BleedingHeavy  => GlobalCondition.BleedingMedium,
                GlobalCondition.BleedingMedium => GlobalCondition.BleedingLight,
                GlobalCondition.BleedingLight  => (GlobalCondition?)null,
                _ => null
            };

            // Убираем текущее кровотечение
            RemoveCondition(originalLevel.Value);

            if (downgraded.HasValue)
            {
                AddCondition(downgraded.Value);
                BleedingStartedAt = DateTime.UtcNow;
                BleedingBurstFinished = false;
            }

            FirstAidUsed = true;
            FirstAidOriginalBleeding = originalLevel;
            FirstAidExpiresAt = DateTime.UtcNow.AddSeconds(60);

            return true;
        }

        /// <summary>
        /// Проверка истечения первой помощи — восстановить исходное кровотечение.
        /// Возвращает true если кровотечение было восстановлено.
        /// </summary>
        public bool CheckFirstAidExpiry()
        {
            if (!FirstAidUsed || FirstAidOriginalBleeding == null)
                return false;
            if (DateTime.UtcNow < FirstAidExpiresAt)
                return false;

            // Восстанавливаем исходное кровотечение
            var current = GetBleedingLevel();
            if (current.HasValue)
                RemoveCondition(current.Value);

            AddCondition(FirstAidOriginalBleeding.Value);
            BleedingStartedAt = DateTime.UtcNow;
            BleedingBurstFinished = false;

            // Сбрасываем флаг — можно применять первую помощь снова?
            // По ТЗ — одноразово до полного излечения, оставляем Used=true
            // Чтобы можно было повторно — раскомментируй:
            // FirstAidUsed = false;
            FirstAidOriginalBleeding = null;
            FirstAidExpiresAt = default;

            return true;
        }
    }
}
 