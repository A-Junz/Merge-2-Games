using System;

namespace MergePrototype.Core
{
    /// <summary>
    /// 能量模型：上限 30，生成消耗 1，每 30 秒恢复 1 点。
    /// 通过 UTC 时间戳计算在线与离线恢复；到达上限后重置累计时间。
    /// </summary>
    public sealed class EnergyModel
    {
        public const int MaxEnergy = 30;
        public const int InitialEnergy = 30;
        public const int GenerateCost = 1;
        public const double SecondsPerPoint = 30.0;

        public int Current { get; private set; }

        /// <summary>最近一次能量结算的 UTC 时间。</summary>
        public DateTimeOffset LastTickUtc { get; private set; }

        public EnergyModel(int initial, DateTimeOffset nowUtc)
        {
            Current = Math.Clamp(initial, 0, MaxEnergy);
            LastTickUtc = nowUtc;
        }

        public bool CanSpend(int amount) => Current >= amount;

        /// <summary>
        /// 消耗能量。只有从满能量消耗时才把恢复基准重置为 now；
        /// 未满时保留已累计的恢复进度。
        /// </summary>
        public bool TrySpend(int amount, DateTimeOffset nowUtc)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (Current < amount) return false;
            bool wasFull = Current >= MaxEnergy;
            Current -= amount;
            if (wasFull)
            {
                LastTickUtc = nowUtc;
            }
            return true;
        }

        /// <summary>
        /// 按经过时间结算恢复，返回本次恢复的点数。
        /// 到达上限后把基准时间重置为 now，防止重复领取离线能量。
        /// </summary>
        public int Regenerate(DateTimeOffset nowUtc)
        {
            if (Current >= MaxEnergy)
            {
                LastTickUtc = nowUtc;
                return 0;
            }

            double elapsed = (nowUtc - LastTickUtc).TotalSeconds;
            if (elapsed < SecondsPerPoint) return 0;

            int points = (int)(elapsed / SecondsPerPoint);
            int before = Current;
            Current = Math.Min(MaxEnergy, Current + points);

            if (Current >= MaxEnergy)
            {
                // 已满：重置基准，结余时间作废。
                LastTickUtc = nowUtc;
            }
            else
            {
                // 未满：基准向前推进已消费的整周期，保留不足一周期的余数。
                LastTickUtc = LastTickUtc.AddSeconds(points * SecondsPerPoint);
            }
            return Current - before;
        }

        public void Restore(int current, DateTimeOffset lastTickUtc)
        {
            Current = Math.Clamp(current, 0, MaxEnergy);
            LastTickUtc = lastTickUtc;
        }
    }
}
