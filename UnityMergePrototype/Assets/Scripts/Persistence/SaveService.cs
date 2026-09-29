using System;
using System.Collections.Generic;
using System.IO;
using MergePrototype.Core;
using UnityEngine;

namespace MergePrototype.Persistence
{
    /// <summary>
    /// JSON 存档服务：读写 merge-prototype-save.json。
    /// 存档包含版本号、棋盘格、能量、金币、最多 5 个订单、能量结算 UTC 时间与随机状态。
    /// 损坏 / 版本不支持 / 格数不匹配时回退到新游戏，绝不让启动失败。
    /// </summary>
    public sealed class SaveService
    {
        public const string FileName = "merge-prototype-save.json";

        [Serializable]
        private sealed class OrderSave
        {
            public int itemId;
            public int count;
            public List<int> requiredItemIds = new List<int>();
            public int rewardAmount;
            public bool grantsExperience = true;
            public bool requiresRewardedVideo;
            public int characterId;
        }

        [Serializable]
        private sealed class SaveData
        {
            public int version;
            public List<int> board = new List<int>();
            public int energy;
            public int coins;
            public List<OrderSave> orders = new List<OrderSave>();
            public string lastEnergyUtcTicks; // JsonUtility 不支持 long，用字符串存 UTC ticks。
            public int rngState;              // JsonUtility 不支持 uint，用 int 存。
            public int level = 1;             // 旧存档无此字段时 JsonUtility 保留默认值。
            public int levelProgress;
            public int pendingLevelUpReward;
            public int activeOrderCount = OrderModel.InitialOrderCount;
            public List<int> seenItemIds = new List<int>();
            public List<string> claimedEncyclopediaChainIds = new List<string>();
            public bool encyclopediaAllRewardClaimed;
            public string nextOrderRefreshUtcTicks;
            public List<int> boardUnlockStates;
            public int defaultOrderProgress = 3;
        }

        private readonly string _filePath;
        private readonly GameVariant _variant;

        /// <summary>最近一次读取失败并回退新游戏的原因；无失败为 null。</summary>
        public string LastFallbackReason { get; private set; }

        public SaveService(string filePath, GameVariant variant = GameVariant.Current)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("存档路径不能为空");
            _filePath = filePath;
            _variant = variant;
        }

        public static string DefaultPath =>
            DefaultPathFor(GameVariant.Current);

        public static string DefaultPathFor(GameVariant variant) =>
            Path.Combine(Application.persistentDataPath, GameVariantConfig.For(variant).SaveFileName);

        public GameVariant Variant => _variant;

        public string FilePath => _filePath;

        public bool SaveFileExists() => File.Exists(_filePath);

        /// <summary>
        /// 读取存档；不存在、无法解析、版本不支持或格数不匹配时返回新游戏并记录原因。
        /// 成功时按 UTC 时间结算离线能量。
        /// </summary>
        public GameState LoadOrCreateNew()
        {
            LastFallbackReason = null;
            if (!File.Exists(_filePath))
            {
                return GameState.CreateNew(_variant);
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return Fallback("存档 JSON 无法解析");
                if (data.version != GameState.SaveVersion && data.version != 1 && data.version != 2)
                    return Fallback($"存档版本不支持: {data.version}");
                if (data.board == null || data.board.Count != BoardModel.CellCount)
                    return Fallback($"棋盘格数不匹配: {(data.board == null ? 0 : data.board.Count)}");
                if (data.orders == null || data.orders.Count < 1 || data.orders.Count > OrderModel.SlotCount)
                    return Fallback($"订单数不匹配: {(data.orders == null ? 0 : data.orders.Count)}");
                if (!long.TryParse(data.lastEnergyUtcTicks, out long ticks))
                    return Fallback("能量结算时间损坏");

                var catalog = new ItemCatalog(_variant);
                for (int i = 0; i < data.board.Count; i++)
                {
                    int itemId = data.board[i];
                    if (itemId != ItemCatalog.EmptyId && !catalog.IsAvailableItem(itemId))
                        return Fallback($"棋盘物品非法: {itemId}");
                }
                for (int i = 0; i < data.orders.Count; i++)
                {
                    var savedOrder = data.orders[i];
                    if (savedOrder == null) return Fallback("订单物品非法: null");
                    if (data.version == 1 && (savedOrder.count < 1 || savedOrder.count > 2))
                        return Fallback($"订单数量非法: {savedOrder.count}");
                    var ids = savedOrder.requiredItemIds != null && savedOrder.requiredItemIds.Count > 0
                        ? savedOrder.requiredItemIds
                        : new List<int> { savedOrder.itemId };
                    if (ids.Count == 0 || ids.Count > 3)
                        return Fallback($"订单需求种类非法: {ids.Count}");
                    for (int j = 0; j < ids.Count; j++)
                    {
                        if (!catalog.IsAvailableItem(ids[j]) || catalog.IsSpecialItem(ids[j]) || catalog.IsMaxLevel(ids[j]))
                            return Fallback($"订单物品非法: {ids[j]}");
                    }
                }

                List<CellUnlockState> unlockStates = null;
                // JsonUtility may deserialize an omitted list field as an
                // empty list. Treat that as the pre-unlock-state legacy save
                // shape instead of rejecting an otherwise valid old save.
                if (data.boardUnlockStates != null && data.boardUnlockStates.Count > 0)
                {
                    if (data.boardUnlockStates.Count != BoardModel.CellCount)
                        return Fallback($"解锁状态格数不匹配: {data.boardUnlockStates.Count}");
                    unlockStates = new List<CellUnlockState>(BoardModel.CellCount);
                    for (int i = 0; i < data.boardUnlockStates.Count; i++)
                    {
                        int raw = data.boardUnlockStates[i];
                        if (raw < (int)CellUnlockState.Locked || raw > (int)CellUnlockState.Unlocked)
                            return Fallback($"解锁状态非法: {raw}");
                        unlockStates.Add((CellUnlockState)raw);
                    }
                }

                int defaultOrderProgress = data.version >= 3
                    ? Math.Max(1, Math.Min(3, data.defaultOrderProgress))
                    : 3;
                var state = GameState.CreateForRestore(catalog, unchecked((uint)data.rngState),
                    unlockStates, defaultOrderProgress);
                state.RestoreSeenItems(data.seenItemIds);
                state.RestoreClaimedEncyclopediaChainIds(data.claimedEncyclopediaChainIds);
                state.RestoreEncyclopediaAllRewardClaimed(data.encyclopediaAllRewardClaimed);
                state.Board.Restore(data.board.ToArray());
                state.ResetEnergy(data.energy, new DateTimeOffset(ticks, TimeSpan.Zero));
                state.SetCoins(data.coins);
                state.SetLevel(data.level, data.levelProgress);
                state.RestorePendingLevelUpReward(data.pendingLevelUpReward);
                int activeCount = data.version == 1
                    ? Math.Min(data.orders.Count, OrderModel.SlotCount)
                    : Math.Max(0, Math.Min(data.activeOrderCount, OrderModel.SlotCount));
                state.Orders.SetActiveCount(activeCount);
                for (int i = 0; i < data.orders.Count; i++)
                {
                    var o = data.orders[i];
                    var ids = o.requiredItemIds != null && o.requiredItemIds.Count > 0
                        ? new List<int>(o.requiredItemIds)
                        : new List<int> { o.itemId };
                    state.Orders.SetOrder(i, new Order
                    {
                        ItemId = ids[0],
                        Count = ids.Count,
                        RequiredItemIds = ids,
                        RewardAmount = o.rewardAmount,
                        GrantsExperience = o.grantsExperience,
                        RequiresRewardedVideo = o.requiresRewardedVideo,
                        CharacterId = o.characterId
                    });
                }

                if (long.TryParse(data.nextOrderRefreshUtcTicks, out long nextRefreshTicks))
                    state.SetNextOrderRefreshUtc(new DateTimeOffset(nextRefreshTicks, TimeSpan.Zero));

                // 离线能量结算。
                state.TickEnergy(DateTimeOffset.UtcNow);
                return state;
            }
            catch (Exception e)
            {
                return Fallback($"存档读取异常: {e.Message}");
            }
        }

        private GameState Fallback(string reason)
        {
            LastFallbackReason = reason;
            Debug.LogWarning($"[SaveService] 存档回退到新游戏：{reason}");
            return GameState.CreateNew(_variant);
        }

        /// <summary>保存当前状态。写入失败时保留内存状态，返回 false 并输出原因。</summary>
        public bool Save(GameState state, out string error)
        {
            error = string.Empty;
            try
            {
                var data = new SaveData
                {
                    version = GameState.SaveVersion,
                    energy = state.Energy.Current,
                    coins = state.Coins,
                    lastEnergyUtcTicks = state.Energy.LastTickUtc.UtcTicks.ToString(),
                    rngState = unchecked((int)state.Rng.State),
                    level = state.Level,
                    levelProgress = state.LevelProgress,
                    pendingLevelUpReward = state.PendingLevelUpReward,
                    activeOrderCount = state.Orders.ActiveCount,
                    nextOrderRefreshUtcTicks = state.NextOrderRefreshUtc.UtcTicks.ToString(),
                    defaultOrderProgress = state.Orders.DefaultOrderProgress,
                    boardUnlockStates = new List<int>(BoardModel.CellCount)
                };
                data.board.AddRange(state.Board.Snapshot());
                foreach (CellUnlockState unlockState in state.Board.UnlockStateSnapshot())
                    data.boardUnlockStates.Add((int)unlockState);
                foreach (int itemId in state.SeenItemIds) data.seenItemIds.Add(itemId);
                foreach (string chainId in state.ClaimedEncyclopediaChainIds)
                    data.claimedEncyclopediaChainIds.Add(chainId);
                data.encyclopediaAllRewardClaimed = state.IsEncyclopediaAllRewardClaimed;
                for (int i = 0; i < OrderModel.SlotCount; i++)
                {
                    var o = state.Orders.GetOrder(i);
                    if (o == null) continue;
                    data.orders.Add(new OrderSave
                    {
                        itemId = o.ItemId,
                        count = o.Count,
                        requiredItemIds = new List<int>(o.GetRequiredItemIds()),
                        rewardAmount = o.RewardAmount,
                        grantsExperience = o.GrantsExperience,
                        requiresRewardedVideo = o.RequiresRewardedVideo,
                        characterId = o.CharacterId
                    });
                }

                string json = JsonUtility.ToJson(data, true);
                string dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string tmp = _filePath + ".tmp";
                File.WriteAllText(tmp, json);
                // 在目标文件存在时使用替换语义，失败时保留原存档；新文件则直接移动。
                if (File.Exists(_filePath))
                {
                    File.Replace(tmp, _filePath, null, true);
                }
                else
                {
                    File.Move(tmp, _filePath);
                }
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError($"[SaveService] 存档写入失败：{e.Message}");
                return false;
            }
        }

        /// <summary>删除存档（测试用）。</summary>
        public void DeleteSave()
        {
            try
            {
                if (File.Exists(_filePath)) File.Delete(_filePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] 删除存档失败：{e.Message}");
            }
        }
    }
}
