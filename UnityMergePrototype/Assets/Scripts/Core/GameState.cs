using System;
using System.Collections.Generic;

namespace MergePrototype.Core
{
    /// <summary>全局玩法状态：棋盘、订单、解锁记录、等级、能量、金额与随机状态。</summary>
    public sealed class GameState
    {
        public const int SaveVersion = 3;
        public const int OrderRefreshMinSeconds = 5;
        public const int OrderRefreshMaxSeconds = 10;

        public ItemCatalog Catalog { get; }
        public GameVariant Variant => Catalog.Variant;
        public GameVariantConfig VariantConfig => Catalog.Config;
        public BoardModel Board { get; }
        public EnergyModel Energy { get; private set; }
        public OrderModel Orders { get; }
        public DeterministicRandom Rng { get; }
        public int Coins { get; private set; }
        public int Level { get; private set; } = 1;
        public int LevelProgress { get; private set; }
        public int PendingLevelUpReward { get; private set; }
        public int PendingLevelUpDirectReward => OrderRewardRules.CalculateDirectReward(PendingLevelUpReward);
        public bool HasPendingLevelUpReward => PendingLevelUpReward > 0;
        public DateTimeOffset NextOrderRefreshUtc { get; private set; }
        public event Action StateChanged;

        private readonly HashSet<int> _seenItemIds = new HashSet<int>();
        private readonly HashSet<string> _claimedEncyclopediaChainIds = new HashSet<string>();
        private bool _encyclopediaAllRewardClaimed;
        private readonly List<int> _lastLuckyGeneratedCellIndices = new List<int>();

        public IReadOnlyCollection<int> SeenItemIds => _seenItemIds;
        public IReadOnlyCollection<string> ClaimedEncyclopediaChainIds => _claimedEncyclopediaChainIds;
        public IReadOnlyList<int> LastLuckyGeneratedCellIndices => _lastLuckyGeneratedCellIndices;
        public int CurrentLevelExperienceRequirement => ExperienceRequiredForNextLevel(Level);
        public int EncyclopediaUnlockedElementCount => _seenItemIds.Count;

        public int EncyclopediaCompletedChainCount
        {
            get
            {
                int completed = 0;
                for (int chain = 1; chain <= Catalog.RegularChainCount; chain++)
                {
                    bool complete = true;
                    for (int level = 1; level <= Catalog.MaxRegularLevel; level++)
                    {
                        if (!_seenItemIds.Contains(chain * 100 + level))
                        {
                            complete = false;
                            break;
                        }
                    }
                    if (complete) completed++;
                }
                return completed;
            }
        }

        public bool EncyclopediaAllChainsCompleted =>
            EncyclopediaCompletedChainCount >= Catalog.RegularChainCount;

        public bool IsEncyclopediaAllRewardClaimed => _encyclopediaAllRewardClaimed;

        public bool CanClaimEncyclopediaAllReward()
        {
            return EncyclopediaAllChainsCompleted && !_encyclopediaAllRewardClaimed;
        }

        public int EncyclopediaRewardTotal => EncyclopediaRewardRules.CalculateTotalReward(
            EncyclopediaUnlockedElementCount,
            EncyclopediaCompletedChainCount,
            EncyclopediaAllChainsCompleted);

        public bool IsEncyclopediaChainCompleted(string chainId)
        {
            if (!IsKnownEncyclopediaChain(chainId)) return false;
            foreach (var def in Catalog.AllItems)
            {
                if (def.ChainId == chainId && !_seenItemIds.Contains(def.Id)) return false;
            }
            return true;
        }

        public bool IsEncyclopediaChainRewardClaimed(string chainId)
        {
            return IsKnownEncyclopediaChain(chainId) && _claimedEncyclopediaChainIds.Contains(chainId);
        }

        public bool CanClaimEncyclopediaChainReward(string chainId)
        {
            return IsEncyclopediaChainCompleted(chainId)
                && !IsEncyclopediaChainRewardClaimed(chainId);
        }

        public bool TryClaimEncyclopediaChainReward(string chainId, out int reward, out string error)
        {
            reward = 0;
            error = string.Empty;
            if (!IsKnownEncyclopediaChain(chainId))
            {
                error = "Unknown collection chain";
                return false;
            }
            if (!IsEncyclopediaChainCompleted(chainId))
            {
                error = "Complete this collection first";
                return false;
            }
            if (IsEncyclopediaChainRewardClaimed(chainId))
            {
                error = "Collection reward already claimed";
                return false;
            }

            reward = EncyclopediaRewardRules.ChainCompletionReward;
            Coins += reward;
            _claimedEncyclopediaChainIds.Add(chainId);
            StateChanged?.Invoke();
            return true;
        }

        public bool TryClaimEncyclopediaAllReward(out int reward, out string error)
        {
            reward = 0;
            error = string.Empty;
            if (!EncyclopediaAllChainsCompleted)
            {
                error = "Complete the collection first";
                return false;
            }
            if (_encyclopediaAllRewardClaimed)
            {
                error = "Collection completion reward already claimed";
                return false;
            }

            reward = EncyclopediaRewardRules.AllChainsCompletionReward;
            Coins += reward;
            _encyclopediaAllRewardClaimed = true;
            StateChanged?.Invoke();
            return true;
        }

        private sealed class WeightedItem
        {
            public int ItemId;
            public double Weight;
        }

        private GameState(ItemCatalog catalog, DeterministicRandom rng)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Board = new BoardModel(catalog);
            Orders = new OrderModel(catalog);
        }

        public static GameState CreateNew(uint? seed = null)
        {
            return CreateNew(GameVariant.Current, seed);
        }

        public static GameState CreateNew(GameVariant variant, uint? seed = null)
        {
            uint actualSeed = seed ?? (uint)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0x7FFFFFFF);
            var state = new GameState(new ItemCatalog(variant), new DeterministicRandom(actualSeed));
            state.Energy = new EnergyModel(EnergyModel.InitialEnergy, DateTimeOffset.UtcNow);
            state.ApplyInitialBoard();
            state._seenItemIds.Clear();
            state.Orders.Initialize(state.Rng, state.Level, state.GetOrderDemandCandidates());
            state.ScheduleNextOrderRefresh(DateTimeOffset.UtcNow);
            return state;
        }

        public static GameState CreateForRestore(ItemCatalog catalog, uint rngState,
            IReadOnlyList<CellUnlockState> unlockStates = null, int defaultOrderProgress = 3)
        {
            var state = new GameState(catalog, new DeterministicRandom(rngState));
            state.Energy = new EnergyModel(0, DateTimeOffset.UtcNow);
            state.InitializeSeenItems();
            if (unlockStates == null)
                state.Board.RestoreUnlockStates(InitialBoardLayout.UnlockStates);
            else
                state.Board.RestoreUnlockStates(unlockStates);
            state.Orders.SetDefaultOrderProgress(defaultOrderProgress);
            // Restoring a save must not consume another RNG value before the
            // persisted RNG state is used by the next gameplay action. Older
            // saves do not persist a refresh timestamp, so use the minimum
            // delay as a deterministic fallback; current saves overwrite it
            // with their persisted timestamp in SaveService.
            state.NextOrderRefreshUtc = DateTimeOffset.UtcNow.AddSeconds(OrderRefreshMinSeconds);
            return state;
        }

        public void ApplyInitialBoard()
        {
            InitialBoardLayout.Build(Board, VariantConfig.InitialBoardItemIds);
        }

        public static int ExperienceRequiredForNextLevel(int level)
        {
            if (level <= 1) return 3;
            if (level == 2) return 4;
            if (level <= 6) return 5;
            if (level <= 9) return 6;
            if (level <= 12) return 7;
            if (level <= 16) return 8;
            if (level <= 20) return 9;
            return 10;
        }

        public static int AvailableChainCountForLevel(int level)
        {
            if (level <= 3) return 1;
            return Math.Min(ItemCatalog.ChainCount, 2 + (level - 3) / 2);
        }

        public void SetCoins(int coins) => Coins = Math.Max(0, coins);

        public void SetLevel(int level, int levelProgress)
        {
            Level = Math.Max(1, Math.Min(level, VariantConfig.MaxPlayerLevel));
            int requirement = ExperienceRequiredForNextLevel(Level);
            LevelProgress = Math.Max(0, Math.Min(levelProgress, requirement - 1));
        }

        public void RestorePendingLevelUpReward(int reward)
        {
            PendingLevelUpReward = Math.Max(0, reward);
        }

        public bool TryClaimPendingLevelUpReward(OrderRewardClaimMode claimMode,
            out int rewardCoins, out string error)
        {
            rewardCoins = 0;
            error = string.Empty;
            if (!HasPendingLevelUpReward)
            {
                error = "没有待领取的升级奖励";
                return false;
            }

            rewardCoins = claimMode == OrderRewardClaimMode.RewardedVideo
                ? PendingLevelUpReward
                : PendingLevelUpDirectReward;
            Coins += rewardCoins;
            PendingLevelUpReward = 0;
            StateChanged?.Invoke();
            return true;
        }

        public void SetNextOrderRefreshUtc(DateTimeOffset nextRefreshUtc)
        {
            NextOrderRefreshUtc = nextRefreshUtc;
        }

        private void ScheduleNextOrderRefresh(DateTimeOffset baseUtc)
        {
            int delay = OrderRefreshMinSeconds + Rng.Next(OrderRefreshMaxSeconds - OrderRefreshMinSeconds + 1);
            NextOrderRefreshUtc = baseUtc.AddSeconds(delay);
        }

        public bool TryCashOutSpecialItem(int cellIndex, out int amount, out string error)
        {
            amount = 0;
            error = string.Empty;
            if (!Board.IsValidIndex(cellIndex))
            {
                error = "无效的格子";
                return false;
            }

            int itemId = Board.GetItemAt(cellIndex);
            amount = Catalog.GetSpecialCashoutAmount(itemId);
            if (amount <= 0)
            {
                amount = 0;
                error = "只有特殊金额元素可以兑现";
                return false;
            }

            Board.SetItemAt(cellIndex, ItemCatalog.EmptyId);
            Coins += amount;
            StateChanged?.Invoke();
            return true;
        }

        public bool TryDeleteItem(int cellIndex, out string error)
        {
            error = string.Empty;
            if (!Board.IsValidIndex(cellIndex))
            {
                error = "无效的格子";
                return false;
            }

            if (Board.GetItemAt(cellIndex) == ItemCatalog.EmptyId)
            {
                error = "这个位置没有合成物";
                return false;
            }

            Board.SetItemAt(cellIndex, ItemCatalog.EmptyId);
            StateChanged?.Invoke();
            return true;
        }

        public bool HasSeenItem(int itemId) => _seenItemIds.Contains(itemId);

        public void RestoreSeenItems(IEnumerable<int> seenItemIds)
        {
            InitializeSeenItems();
            if (seenItemIds == null) return;
            foreach (int itemId in seenItemIds)
            {
                if (Catalog.IsAvailableItem(itemId) && !Catalog.IsSpecialItem(itemId))
                    _seenItemIds.Add(itemId);
            }
        }

        public void RestoreClaimedEncyclopediaChainIds(IEnumerable<string> chainIds)
        {
            _claimedEncyclopediaChainIds.Clear();
            if (chainIds == null) return;
            foreach (string chainId in chainIds)
            {
                if (IsKnownEncyclopediaChain(chainId))
                    _claimedEncyclopediaChainIds.Add(chainId);
            }
        }

        public void RestoreEncyclopediaAllRewardClaimed(bool claimed)
        {
            _encyclopediaAllRewardClaimed = claimed && EncyclopediaAllChainsCompleted;
        }

        private bool IsKnownEncyclopediaChain(string chainId)
        {
            if (string.IsNullOrEmpty(chainId) || chainId == "special") return false;
            foreach (var def in Catalog.AllItems)
            {
                if (def.ChainId == chainId && !Catalog.IsSpecialItem(def.Id)) return true;
            }
            return false;
        }

        public List<int> GetOrderDemandCandidates()
        {
            var candidates = new List<int>();
            int availableChains = Math.Min(
                Catalog.RegularChainCount,
                AvailableChainCountForLevel(Level));
            for (int chain = 1; chain <= availableChains; chain++)
            {
                // 每条链的 1 级元素只作为发牌起点，不进入订单需求池。
                int highestSeen = 1;
                for (int level = 2; level <= Catalog.MaxRegularLevel; level++)
                {
                    int itemId = chain * 100 + level;
                    if (_seenItemIds.Contains(itemId))
                    {
                        highestSeen = level;
                        candidates.Add(itemId);
                    }
                }

                if (highestSeen < Catalog.MaxRegularLevel)
                    candidates.Add(chain * 100 + highestSeen + 1);
            }
            return candidates;
        }

        public bool TryGenerate(out string error)
        {
            error = string.Empty;
            _lastLuckyGeneratedCellIndices.Clear();
            var empties = Board.GetEmptyIndices();
            if (empties.Count == 0)
            {
                error = "棋盘已满";
                return false;
            }

            var pool = BuildGenerationPool();
            if (pool.Count == 0)
            {
                foreach (int itemId in Catalog.LevelOneIds)
                    pool.Add(new WeightedItem { ItemId = itemId, Weight = 1d });
            }

            for (int i = 0; i < empties.Count; i++)
            {
                int itemId = PickWeighted(pool);
                Board.SetItemAt(empties[i], itemId);
                MarkSeen(itemId);
                if (Catalog.TryGet(itemId, out var generatedDef)
                    && LuckyMarkerPolicy.IsLuckyLevel(generatedDef.Level))
                {
                    _lastLuckyGeneratedCellIndices.Add(empties[i]);
                }
            }

            StateChanged?.Invoke();
            return true;
        }

        public DropResult HandleDrop(int sourceIndex, int targetIndex)
        {
            var result = Board.HandleDrop(sourceIndex, targetIndex);
            if (result.Type != DropResultType.Rejected)
            {
                if (result.Type == DropResultType.Merged)
                {
                    int mergedItemId = Board.GetItemAt(targetIndex);
                    HandleMergedItem(mergedItemId);
                }
                StateChanged?.Invoke();
            }
            return result;
        }

        public int RunAutoComplete(int maxActions, out int mergeCount,
            out int submittedOrderCount, out int generatedBatchCount)
        {
            mergeCount = 0;
            submittedOrderCount = 0;
            generatedBatchCount = 0;
            if (maxActions <= 0) return 0;

            int actions = 0;
            while (actions < maxActions)
            {
                bool acted = false;
                for (int source = 0; source < BoardModel.CellCount && !acted; source++)
                {
                    if (Board.GetUnlockState(source) != CellUnlockState.Unlocked) continue;
                    int sourceItem = Board.GetItemAt(source);
                    if (sourceItem == ItemCatalog.EmptyId) continue;

                    for (int target = 0; target < BoardModel.CellCount; target++)
                    {
                        if (target == source) continue;
                        if (Board.GetUnlockState(target) == CellUnlockState.Locked) continue;
                        if (Board.GetItemAt(target) != sourceItem
                            || !Catalog.CanMerge(sourceItem, sourceItem)) continue;

                        var result = HandleDrop(source, target);
                        if (result.Type != DropResultType.Merged) continue;
                        mergeCount++;
                        actions++;
                        acted = true;
                        break;
                    }
                }
                if (acted) continue;

                for (int slot = 0; slot < Orders.ActiveCount; slot++)
                {
                    if (!Orders.IsFulfilled(slot, Board)) continue;
                    if (!TrySubmitOrder(slot, out _, out _)) continue;
                    submittedOrderCount++;
                    actions++;
                    acted = true;
                    break;
                }
                if (acted) continue;

                if (Board.HasEmptyCell() && TryGenerate(out _))
                {
                    generatedBatchCount++;
                    actions++;
                    continue;
                }

                break;
            }

            return actions;
        }

        public bool TrySubmitOrder(int slot, out int rewardCoins, out string error)
        {
            return TrySubmitOrder(slot, OrderRewardClaimMode.Direct,
                out rewardCoins, out error);
        }

        public bool TrySubmitOrderWithRewardedVideo(int slot, out int rewardCoins, out string error)
        {
            return TrySubmitOrder(slot, OrderRewardClaimMode.RewardedVideo,
                out rewardCoins, out error);
        }

        public bool TrySubmitOrder(int slot, OrderRewardClaimMode claimMode,
            out int rewardCoins, out string error)
        {
            rewardCoins = 0;
            error = string.Empty;
            var order = slot >= 0 && slot < OrderModel.SlotCount ? Orders.GetOrder(slot) : null;
            if (order == null || slot >= Orders.ActiveCount)
            {
                error = "无效的订单";
                return false;
            }

            bool grantsExperience = order.GrantsExperience;
            bool ok = Orders.TrySubmit(slot, Board, Rng, GetOrderDemandCandidates(), Level,
                out int fullOrderReward, out error);
            if (!ok) return false;

            int awardedOrderReward = order.RequiresRewardedVideo
                && claimMode != OrderRewardClaimMode.RewardedVideo
                ? OrderRewardRules.CalculateDirectReward(fullOrderReward)
                : fullOrderReward;
            rewardCoins = awardedOrderReward;
            Coins += awardedOrderReward;
            if (grantsExperience && Level < VariantConfig.MaxPlayerLevel)
            {
                LevelProgress++;
                while (Level < VariantConfig.MaxPlayerLevel
                    && LevelProgress >= ExperienceRequiredForNextLevel(Level))
                {
                    LevelProgress -= ExperienceRequiredForNextLevel(Level);
                    Level++;
                    int upgradeReward = 30 + Rng.Next(21);
                    PendingLevelUpReward += upgradeReward;
                }
            }

            ScheduleNextOrderRefresh(DateTimeOffset.UtcNow);
            StateChanged?.Invoke();
            return true;
        }

        public int TickOrders(DateTimeOffset nowUtc)
        {
            if (nowUtc < NextOrderRefreshUtc) return 0;
            int added = Orders.RefreshMissing(Rng, Level, GetOrderDemandCandidates());
            ScheduleNextOrderRefresh(nowUtc);
            if (added > 0) StateChanged?.Invoke();
            return added;
        }

        public int TickEnergy(DateTimeOffset nowUtc)
        {
            int gained = Energy.Regenerate(nowUtc);
            if (gained > 0) StateChanged?.Invoke();
            return gained;
        }

        public void ResetEnergy(int current, DateTimeOffset lastTickUtc)
        {
            Energy.Restore(current, lastTickUtc);
        }

        private void InitializeSeenItems()
        {
            _seenItemIds.Clear();
            foreach (int itemId in Catalog.LevelOneIds) _seenItemIds.Add(itemId);
        }

        private void MarkSeen(int itemId)
        {
            if (itemId != ItemCatalog.EmptyId && Catalog.IsAvailableItem(itemId) && !Catalog.IsSpecialItem(itemId))
                _seenItemIds.Add(itemId);
        }

        private void HandleMergedItem(int mergedItemId)
        {
            if (!Catalog.TryGet(mergedItemId, out var def) || Catalog.IsSpecialItem(mergedItemId)) return;

            bool wasSeen = HasSeenItem(mergedItemId);
            MarkSeen(mergedItemId);
            int specialLevel = SpecialSpawnRules.Roll(def.Level, !wasSeen, Rng);

            if (specialLevel > 0) TrySpawnSpecial(specialLevel);
        }

        private bool TrySpawnSpecial(int level)
        {
            int empty = Board.GetEmptyIndices().Count > 0 ? Board.GetEmptyIndices()[0] : -1;
            if (empty < 0) return false;
            Board.SetItemAt(empty, 1100 + Math.Max(1, Math.Min(ItemCatalog.SpecialMaxLevel, level)));
            return true;
        }

        private List<WeightedItem> BuildGenerationPool()
        {
            var chainWeights = new Dictionary<string, int>();
            for (int slot = 0; slot < Orders.ActiveCount; slot++)
            {
                var order = Orders.GetOrder(slot);
                if (order == null) continue;
                var required = order.GetRequiredItemIds();
                for (int i = 0; i < required.Count; i++)
                {
                    var def = Catalog.Get(required[i]);
                    if (!chainWeights.ContainsKey(def.ChainId)) chainWeights[def.ChainId] = 0;
                    chainWeights[def.ChainId] += 10;
                }
            }

            var levelWeights = new Dictionary<int, double>
            {
                { 1, 100d }, { 2, 20d }, { 3, 5d }, { 4, 0.5d }
            };
            var pool = new List<WeightedItem>();
            foreach (var def in Catalog.AllItems)
            {
                if (Catalog.IsSpecialItem(def.Id) || def.Level > 4) continue;
                if (!chainWeights.TryGetValue(def.ChainId, out int chainWeight)) continue;
                pool.Add(new WeightedItem
                {
                    ItemId = def.Id,
                    Weight = levelWeights[def.Level] * chainWeight
                });
            }
            return pool;
        }

        private int PickWeighted(List<WeightedItem> pool)
        {
            double total = 0d;
            for (int i = 0; i < pool.Count; i++) total += pool[i].Weight;
            double roll = Rng.NextDouble() * total;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= pool[i].Weight;
                if (roll < 0d) return pool[i].ItemId;
            }
            return pool[pool.Count - 1].ItemId;
        }
    }
}
