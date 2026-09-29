using System.Collections.Generic;

namespace MergePrototype.Core
{
    /// <summary>
    /// 版本级玩法边界。普通目录仍共享同一套物品定义，配置只决定哪些内容对该版本可用。
    /// </summary>
    public sealed class GameVariantConfig
    {
        private GameVariantConfig(GameVariant variant, string saveFileName,
            int regularChainCount, int maxRegularLevel, int maxPlayerLevel,
            IReadOnlyList<int> initialBoardItemIds)
        {
            Variant = variant;
            SaveFileName = saveFileName;
            RegularChainCount = regularChainCount;
            MaxRegularLevel = maxRegularLevel;
            MaxPlayerLevel = maxPlayerLevel;
            InitialBoardItemIds = initialBoardItemIds;
        }

        public GameVariant Variant { get; }
        public string SaveFileName { get; }
        public int RegularChainCount { get; }
        public int MaxRegularLevel { get; }
        public int MaxPlayerLevel { get; }
        public IReadOnlyList<int> InitialBoardItemIds { get; }

        public bool IsAvailableItem(ItemDef def)
        {
            if (def == null) return false;
            return def.ChainId == "special"
                || (def.Id / 100 >= 1
                    && def.Id / 100 <= RegularChainCount
                    && def.Level <= MaxRegularLevel);
        }

        public static GameVariantConfig For(GameVariant variant)
        {
            switch (variant)
            {
                case GameVariant.TestA:
                    return new GameVariantConfig(
                        GameVariant.TestA,
                        "merge-prototype-save-test-a.json",
                        regularChainCount: 5,
                        maxRegularLevel: 7,
                        maxPlayerLevel: int.MaxValue,
                        TestAInitialBoardLayout.ItemIds);
                default:
                    return new GameVariantConfig(
                        GameVariant.Current,
                        "merge-prototype-save.json",
                        regularChainCount: ItemCatalog.ChainCount,
                        maxRegularLevel: ItemCatalog.MaxLevel,
                        maxPlayerLevel: int.MaxValue,
                        InitialBoardLayout.ItemIds);
            }
        }
    }
}
