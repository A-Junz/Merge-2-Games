namespace MergePrototype.Core
{
    /// <summary>普通合成触发特殊金额元素的等级与概率规则。</summary>
    public static class SpecialSpawnRules
    {
        /// <summary>
        /// 返回本次普通合成应生成的特殊链等级；0 表示不生成。
        /// 首次出现的 4～8 级普通元素固定生成特殊 1 级，重复合成使用等级概率表。
        /// </summary>
        public static int Roll(int normalLevel, bool isFirstNewElement, DeterministicRandom rng)
        {
            if (rng == null) throw new System.ArgumentNullException(nameof(rng));
            if (normalLevel < 4 || normalLevel > 8) return 0;
            if (isFirstNewElement) return 1;

            int roll = rng.Next(100);
            switch (normalLevel)
            {
                case 6:
                    return roll < 50 ? 1 : 0;
                case 7:
                    if (roll < 25) return 1;
                    if (roll < 50) return 2;
                    return 0;
                case 8:
                    if (roll < 40) return 2;
                    if (roll < 50) return 3;
                    return 0;
                default:
                    return 0;
            }
        }
    }
}
