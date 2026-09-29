using System;
using System.Collections.Generic;
using System.Linq;

namespace MergePrototype.Core
{
    /// <summary>一个订单中的单项需求。首个版本每种元素数量固定为 1。</summary>
    public sealed class OrderRequirement
    {
        public int ItemId;
        public int Count = 1;
    }

    /// <summary>订单：人物由表现层绑定，核心只保存需求、金额奖励和经验奖励。</summary>
    public sealed class Order
    {
        // 保留这两个字段兼容旧存档和旧测试；多元素订单使用 RequiredItemIds。
        public int ItemId;
        public int Count = 1;
        public List<int> RequiredItemIds = new List<int>();
        public int RewardAmount;
        public bool GrantsExperience = true;
        public bool RequiresRewardedVideo;
        public int CharacterId;

        public IReadOnlyList<int> GetRequiredItemIds()
        {
            if (RequiredItemIds != null && RequiredItemIds.Count > 0)
                return RequiredItemIds;

            var legacy = new List<int>();
            int count = Math.Max(Count, 1);
            for (int i = 0; i < count; i++) legacy.Add(ItemId);
            return legacy;
        }

        public void NormalizeLegacyFields()
        {
            if ((RequiredItemIds == null || RequiredItemIds.Count == 0) && ItemId != ItemCatalog.EmptyId)
            {
                RequiredItemIds = new List<int>();
                int count = Math.Max(Count, 1);
                for (int i = 0; i < count; i++) RequiredItemIds.Add(ItemId);
            }

            if (RequiredItemIds != null && RequiredItemIds.Count > 0)
            {
                ItemId = RequiredItemIds[0];
                Count = RequiredItemIds.Count;
            }
        }

        public int RewardCoins(ItemCatalog catalog)
        {
            int requirementCount = Math.Max(1, Math.Min(3, GetRequiredItemIds().Count));
            int baseAmount;
            if (RewardAmount > 0)
            {
                baseAmount = RewardAmount;
            }
            else
            {
                baseAmount = BaseRewardMinForItemLevel(HighestItemLevel(catalog, GetRequiredItemIds()));
            }

            double multiplier = requirementCount == 1
                ? 1d
                : requirementCount == 2 ? 1.5d : 2d;
            return (int)Math.Round(baseAmount * multiplier, MidpointRounding.AwayFromZero);
        }

        public static int BaseRewardMinForItemLevel(int itemLevel)
        {
            int level = Math.Max(1, Math.Min(ItemCatalog.MaxLevel, itemLevel));
            switch (level)
            {
                case 1: return 5;
                case 2: return 10;
                case 3: return 15;
                case 4: return 20;
                case 5: return 25;
                case 6: return 30;
                case 7: return 35;
                default: return 40;
            }
        }

        public static int BaseRewardMaxForItemLevel(int itemLevel)
        {
            int level = Math.Max(1, Math.Min(ItemCatalog.MaxLevel, itemLevel));
            switch (level)
            {
                case 1: return 10;
                case 2: return 15;
                case 3: return 20;
                case 4: return 25;
                case 5: return 30;
                case 6: return 40;
                case 7: return 45;
                default: return 50;
            }
        }

        public static int RollBaseRewardAmount(int itemLevel, DeterministicRandom rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            int min = BaseRewardMinForItemLevel(itemLevel);
            int max = BaseRewardMaxForItemLevel(itemLevel);
            return min + rng.Next(max - min + 1);
        }

        internal static int HighestItemLevel(ItemCatalog catalog, IReadOnlyList<int> itemIds)
        {
            int highest = 1;
            for (int i = 0; i < itemIds.Count; i++)
                highest = Math.Max(highest, catalog.Get(itemIds[i]).Level);
            return highest;
        }

        public bool IsFulfilled(BoardModel board)
        {
            var required = GetRequiredItemIds();
            if (required.Count == 0 || required[0] == ItemCatalog.EmptyId) return false;
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < required.Count; i++)
            {
                if (!counts.ContainsKey(required[i])) counts[required[i]] = 0;
                counts[required[i]]++;
            }
            foreach (var pair in counts)
                if (board.CountItemInFullyUnlockedCells(pair.Key) < pair.Value) return false;
            return true;
        }

        public int FulfilledCount(BoardModel board)
        {
            int fulfilled = 0;
            var required = GetRequiredItemIds();
            var consumed = new Dictionary<int, int>();
            for (int i = 0; i < required.Count; i++)
            {
                int available = board.CountItemInFullyUnlockedCells(required[i]);
                int used = consumed.ContainsKey(required[i]) ? consumed[required[i]] : 0;
                if (available > used)
                {
                    consumed[required[i]] = used + 1;
                    fulfilled++;
                }
            }
            return fulfilled;
        }
    }

    /// <summary>
    /// 订单模型：最多 5 个订单，初始激活 1 个；缺口按 5 秒刷新规则由 GameState 驱动。
    /// </summary>
    public sealed class OrderModel
    {
        public const int SlotCount = 5;
        public const int CustomerPortraitCount = 7;
        public const int InitialOrderCount = 1;
        public const int MinCount = 1;
        public const int MaxCount = 1;

        private readonly ItemCatalog _catalog;
        private readonly Order[] _orders = new Order[SlotCount];

        public int ActiveCount { get; private set; }
        /// <summary>已完成的固定首发订单数量，范围 1～3；3 表示已进入随机订单阶段。</summary>
        public int DefaultOrderProgress { get; private set; }

        public OrderModel(ItemCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public static int MaxOrdersForLevel(int level)
        {
            if (level <= 1) return 1;
            if (level == 2) return 2;
            if (level <= 5) return 3;
            if (level <= 10) return 4;
            return 5;
        }

        public void SetDefaultOrderProgress(int progress)
        {
            DefaultOrderProgress = Math.Max(0, Math.Min(3, progress));
        }

        public Order GetOrder(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return _orders[slot];
        }

        public void SetOrder(int slot, Order order)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            _orders[slot] = order ?? throw new ArgumentNullException(nameof(order));
            _orders[slot].NormalizeLegacyFields();
            if (slot >= ActiveCount) ActiveCount = slot + 1;
        }

        public void SetActiveCount(int count)
        {
            ActiveCount = Math.Max(0, Math.Min(SlotCount, count));
        }

        public Order RollOrder(int slot, DeterministicRandom rng, Order exclude,
            IReadOnlyList<int> demandCandidates, int playerLevel)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            if (demandCandidates == null || demandCandidates.Count == 0)
                throw new ArgumentException("订单候选池不能为空", nameof(demandCandidates));

            Order result = null;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                result = CreateRandomOrder(slot, rng, demandCandidates, playerLevel);
                if (!HasDuplicateRequirements(slot, result, exclude)) break;
            }

            _orders[slot] = result;
            return result;
        }

        private Order CreateRandomOrder(int slot, DeterministicRandom rng,
            IReadOnlyList<int> demandCandidates, int playerLevel)
        {
            int maxTypes = playerLevel <= 5 ? 1 : playerLevel <= 10 ? 2 : 3;
            int typeCount = maxTypes == 1 ? 1 : 1 + rng.Next(maxTypes);
            var ids = new List<int>(typeCount);
            int guard = 0;
            while (ids.Count < typeCount && guard++ < 100)
            {
                int candidate = demandCandidates[rng.Next(demandCandidates.Count)];
                if (!ids.Contains(candidate)) ids.Add(candidate);
            }
            if (ids.Count == 0) ids.Add(demandCandidates[0]);

            int rewardLevel = Order.HighestItemLevel(_catalog, ids);
            bool grantsExperience = !(playerLevel >= 11 && rng.Next(100) < 5);
            int rewardAmount = Order.RollBaseRewardAmount(rewardLevel, rng);
            var result = new Order
            {
                RequiredItemIds = ids,
                ItemId = ids[0],
                Count = ids.Count,
                RewardAmount = rewardAmount,
                GrantsExperience = grantsExperience,
                CharacterId = PickCharacterId(slot, rng)
            };
            result.RequiresRewardedVideo = OrderRewardRules.ShouldRequireRewardedVideo(
                Order.HighestItemLevel(_catalog, result.GetRequiredItemIds()),
                result.GetRequiredItemIds().Count,
                rng);
            return result;
        }

        private bool HasDuplicateRequirements(int slot, Order candidate, Order exclude)
        {
            if (exclude != null && SameRequirements(candidate, exclude)) return true;
            if (HasMaxLevelConflict(slot, candidate)) return true;
            for (int i = 0; i < _orders.Length; i++)
            {
                if (i == slot || _orders[i] == null) continue;
                if (SameRequirements(candidate, _orders[i])) return true;
            }
            return false;
        }

        private bool HasMaxLevelConflict(int slot, Order candidate)
        {
            var candidateIds = candidate.GetRequiredItemIds();
            for (int c = 0; c < candidateIds.Count; c++)
            {
                int candidateId = candidateIds[c];
                if (!_catalog.IsMaxLevel(candidateId) || _catalog.IsSpecialItem(candidateId)) continue;

                for (int i = 0; i < _orders.Length; i++)
                {
                    if (i == slot || _orders[i] == null) continue;
                    var existingIds = _orders[i].GetRequiredItemIds();
                    for (int e = 0; e < existingIds.Count; e++)
                    {
                        int existingId = existingIds[e];
                        if (_catalog.IsMaxLevel(existingId) && !_catalog.IsSpecialItem(existingId))
                            return true;
                    }
                }
            }
            return false;
        }

        public Order RollOrder(int slot, DeterministicRandom rng, Order exclude)
        {
            var candidates = new List<int>();
            foreach (var def in _catalog.GetOrderCandidates()) candidates.Add(def.Id);
            return RollOrder(slot, rng, exclude, candidates, 1);
        }

        public void RollAll(DeterministicRandom rng)
        {
            var candidates = new List<int>();
            foreach (var def in _catalog.GetOrderCandidates()) candidates.Add(def.Id);
            ActiveCount = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                RollOrder(i, rng, null, candidates, 1);
                ActiveCount++;
            }
        }

        public void Initialize(DeterministicRandom rng, int playerLevel, IReadOnlyList<int> demandCandidates)
        {
            ActiveCount = 0;
            DefaultOrderProgress = 1;
            for (int i = 0; i < SlotCount; i++) _orders[i] = null;
            _orders[0] = CreateDefaultOrder(0, rng);
            ActiveCount = InitialOrderCount;
        }

        public Order CreateDefaultOrder(int index, DeterministicRandom rng)
        {
            if (index < 0 || index >= 3) throw new ArgumentOutOfRangeException(nameof(index));
            int[] ids = { 102, 103, 104 };
            int itemId = ids[index];
            return new Order
            {
                ItemId = itemId,
                Count = 1,
                RequiredItemIds = new List<int> { itemId },
                RewardAmount = Order.RollBaseRewardAmount(_catalog.Get(itemId).Level, rng),
                GrantsExperience = true,
                RequiresRewardedVideo = false,
                CharacterId = PickCharacterId(index, rng)
            };
        }

        public int GetProgress(int slot, BoardModel board)
        {
            return GetOrder(slot)?.FulfilledCount(board) ?? 0;
        }

        public bool IsFulfilled(int slot, BoardModel board)
        {
            var order = GetOrder(slot);
            return order != null && slot < ActiveCount && order.IsFulfilled(board);
        }

        public bool TrySubmit(int slot, BoardModel board, DeterministicRandom rng,
            IReadOnlyList<int> demandCandidates, int playerLevel,
            out int rewardCoins, out string error)
        {
            rewardCoins = 0;
            error = string.Empty;
            if (slot < 0 || slot >= ActiveCount || _orders[slot] == null)
            {
                error = "无效的订单";
                return false;
            }

            var order = _orders[slot];
            if (!order.IsFulfilled(board))
            {
                error = "订单物品不足";
                return false;
            }

            var required = order.GetRequiredItemIds();
            for (int i = 0; i < required.Count; i++)
                board.RemoveItemsFromFullyUnlockedCells(required[i], 1);
            rewardCoins = order.RewardCoins(_catalog);

            if (ActiveCount == 1)
            {
                if (DefaultOrderProgress < 3)
                {
                    _orders[slot] = CreateDefaultOrder(DefaultOrderProgress, rng);
                    DefaultOrderProgress++;
                }
                else
                {
                    RollOrder(slot, rng, order, demandCandidates, playerLevel);
                }
            }
            else
            {
                // 保持激活订单连续排列，避免删除中间订单后高位订单被 ActiveCount 截断。
                for (int i = slot; i < ActiveCount - 1; i++)
                    _orders[i] = _orders[i + 1];
                _orders[ActiveCount - 1] = null;
                ActiveCount--;
            }
            return true;
        }

        public bool TrySubmit(int slot, BoardModel board, DeterministicRandom rng,
            out int rewardCoins, out string error)
        {
            var candidates = new List<int>();
            foreach (var def in _catalog.GetOrderCandidates()) candidates.Add(def.Id);
            return TrySubmit(slot, board, rng, candidates, 1, out rewardCoins, out error);
        }

        public int RefreshMissing(DeterministicRandom rng, int playerLevel, IReadOnlyList<int> demandCandidates)
        {
            int maxOrders = MaxOrdersForLevel(playerLevel);
            if (ActiveCount >= maxOrders) return 0;

            int requested = 1 + rng.Next(3);
            int added = Math.Min(requested, maxOrders - ActiveCount);
            for (int n = 0; n < added; n++)
            {
                int slot = FindInactiveSlot();
                if (slot < 0) break;
                RollOrder(slot, rng, null, demandCandidates, playerLevel);
                ActiveCount++;
            }
            return added;
        }

        private int FindInactiveSlot()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_orders[i] == null) return i;
            }
            return -1;
        }

        private int PickCharacterId(int slot, DeterministicRandom rng)
        {
            for (int attempt = 0; attempt < 32; attempt++)
            {
                int candidate = 1 + rng.Next(CustomerPortraitCount);
                bool used = false;
                for (int i = 0; i < ActiveCount; i++)
                {
                    if (i == slot || _orders[i] == null) continue;
                    if (_orders[i].CharacterId == candidate)
                    {
                        used = true;
                        break;
                    }
                }
                if (!used) return candidate;
            }
            return (slot % CustomerPortraitCount) + 1;
        }

        private static bool SameRequirements(Order a, Order b)
        {
            var left = a.GetRequiredItemIds();
            var right = b.GetRequiredItemIds();
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                if (!right.Contains(left[i])) return false;
            }
            return true;
        }
    }
}
