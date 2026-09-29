namespace MergePrototype.Core
{
    /// <summary>订单金额领取规则。</summary>
    public static class OrderRewardRules
    {
        /// <summary>
        /// 仅当订单包含 5 级以上元素且需求数量为 2 或 3 时，按 50% 概率启用激励视频全额领取。
        /// 其他订单不需要观看视频即可领取完整订单金额。
        /// </summary>
        public static bool ShouldRequireRewardedVideo(int highestItemLevel, int requirementCount,
            DeterministicRandom rng)
        {
            if (rng == null) throw new System.ArgumentNullException(nameof(rng));
            if (highestItemLevel < 5 || requirementCount < 2 || requirementCount > 3)
                return false;
            return rng.Next(100) < 50;
        }

        /// <summary>
        /// 未观看激励视频时领取完整订单金额的十分之一，金额为整数时向下取整，
        /// 但任何有效订单至少获得 1 金币。
        /// </summary>
        public static int CalculateDirectReward(int fullReward)
        {
            if (fullReward <= 0) return 0;
            return System.Math.Max(1, fullReward / 10);
        }
    }
}
