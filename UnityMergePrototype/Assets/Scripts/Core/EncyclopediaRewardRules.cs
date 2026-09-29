namespace MergePrototype.Core
{
    /// <summary>图鉴解锁奖励规则。奖励金额用于图鉴展示与领取逻辑，按一次性解锁进度计算。</summary>
    public static class EncyclopediaRewardRules
    {
        public const int ElementReward = 1;
        public const int ChainCompletionReward = 10;
        public const int AllChainsCompletionReward = 100;

        public static int CalculateTotalReward(int unlockedElementCount, int completedChainCount,
            bool allChainsCompleted)
        {
            int elements = unlockedElementCount < 0 ? 0 : unlockedElementCount;
            int chains = completedChainCount < 0 ? 0 : completedChainCount;
            int reward = elements * ElementReward + chains * ChainCompletionReward;
            if (allChainsCompleted) reward += AllChainsCompletionReward;
            return reward;
        }
    }
}
