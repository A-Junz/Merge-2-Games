using System;
using System.Linq;
using MergePrototype.Core;
using NUnit.Framework;

namespace MergePrototype.Tests.EditMode
{
    public class RulesSystemTests
    {
        [Test]
        public void LevelExperience_IsRequiredForCurrentLevelToAdvance()
        {
            Assert.AreEqual(3, GameState.ExperienceRequiredForNextLevel(1));
            Assert.AreEqual(4, GameState.ExperienceRequiredForNextLevel(2));
            Assert.AreEqual(5, GameState.ExperienceRequiredForNextLevel(3));
            Assert.AreEqual(5, GameState.ExperienceRequiredForNextLevel(6));
            Assert.AreEqual(6, GameState.ExperienceRequiredForNextLevel(7));
            Assert.AreEqual(7, GameState.ExperienceRequiredForNextLevel(10));
            Assert.AreEqual(8, GameState.ExperienceRequiredForNextLevel(13));
            Assert.AreEqual(9, GameState.ExperienceRequiredForNextLevel(17));
            Assert.AreEqual(10, GameState.ExperienceRequiredForNextLevel(21));
        }

        [Test]
        public void NewGame_StartsWithOneFixedOrder_AndOnlyNextUnlockCandidates()
        {
            var state = GameState.CreateNew(seed: 17u);
            Assert.AreEqual(1, state.Orders.ActiveCount);
            Assert.AreEqual(102, state.Orders.GetOrder(0).ItemId);

            var candidates = state.GetOrderDemandCandidates().ToList();
            Assert.IsFalse(candidates.Any(id => id % 100 == 1));
            Assert.IsTrue(candidates.Contains(102));
            Assert.IsFalse(candidates.Contains(103));
            Assert.IsFalse(candidates.Contains(301));
            Assert.IsFalse(candidates.Contains(302));
            Assert.IsFalse(candidates.Contains(303));
            Assert.IsFalse(candidates.Contains(1001));
        }

        [Test]
        public void OrderCaps_FollowLevelRanges()
        {
            Assert.AreEqual(1, OrderModel.MaxOrdersForLevel(1));
            Assert.AreEqual(2, OrderModel.MaxOrdersForLevel(2));
            Assert.AreEqual(3, OrderModel.MaxOrdersForLevel(3));
            Assert.AreEqual(3, OrderModel.MaxOrdersForLevel(5));
            Assert.AreEqual(4, OrderModel.MaxOrdersForLevel(6));
            Assert.AreEqual(4, OrderModel.MaxOrdersForLevel(10));
            Assert.AreEqual(5, OrderModel.MaxOrdersForLevel(11));
        }

        [Test]
        public void GeneratedOrders_UseDistinctIdsForAllSevenCustomerPortraits()
        {
            var state = GameState.CreateNew(seed: 31u);
            state.Orders.RollAll(state.Rng);

            var characterIds = Enumerable.Range(0, state.Orders.ActiveCount)
                .Select(slot => state.Orders.GetOrder(slot).CharacterId)
                .ToList();

            Assert.IsTrue(characterIds.All(id => id >= 1 && id <= 7),
                "新增四位人物后，订单人物 ID 应使用 7 个唯一头像位");
            Assert.AreEqual(characterIds.Count, characterIds.Distinct().Count(),
                "同时存在的订单不应复用人物头像");
        }

        [Test]
        public void AutoComplete_MergesSubmitsAndReportsEachAutomaticAction()
        {
            var state = GameState.CreateNew(seed: 71u);
            state.Board.Clear();
            for (int i = 0; i < BoardModel.CellCount; i++)
                state.Board.SetUnlockState(i, CellUnlockState.Unlocked);
            state.Orders.SetActiveCount(1);
            state.Orders.SetOrder(0, new Order
            {
                RequiredItemIds = new System.Collections.Generic.List<int> { 102 },
                RewardAmount = 5,
                CharacterId = 1
            });
            state.Board.SetItemAt(0, 101);
            state.Board.SetItemAt(1, 101);

            int actions = state.RunAutoComplete(2, out int merges, out int submitted, out int generated);

            Assert.AreEqual(2, actions);
            Assert.AreEqual(1, merges);
            Assert.AreEqual(1, submitted);
            Assert.AreEqual(0, generated);
            Assert.AreEqual(5, state.Coins);
        }

        [Test]
        public void GeneratedOrders_DoNotDuplicateExistingRequirements()
        {
            var catalog = new ItemCatalog();
            var model = new OrderModel(catalog);
            model.SetOrder(0, new Order
            {
                RequiredItemIds = new System.Collections.Generic.List<int> { 101 }
            });

            var generated = model.RollOrder(1, new DeterministicRandom(2u), null,
                new[] { 101, 102 }, 1);

            Assert.AreNotEqual(101, generated.ItemId,
                "订单栏中不应同时出现需求元素和数量完全相同的订单");
        }

        [Test]
        public void GeneratedOrders_DoNotDuplicateMaxLevelElementsAcrossActiveOrders()
        {
            var catalog = new ItemCatalog();
            var model = new OrderModel(catalog);
            model.SetOrder(0, new Order
            {
                RequiredItemIds = new System.Collections.Generic.List<int> { 108 }
            });

            var generated = model.RollOrder(1, new DeterministicRandom(2u), null,
                new[] { 108, 107 }, 1);

            Assert.AreNotEqual(108, generated.ItemId,
                "同一普通合成链的 8 级元素不能同时出现在两个激活订单中");
        }

        [Test]
        public void GeneratedOrders_DoNotAllowDifferentChainsMaxLevelElementsTogether()
        {
            var catalog = new ItemCatalog();
            var model = new OrderModel(catalog);
            model.SetOrder(0, new Order
            {
                RequiredItemIds = new System.Collections.Generic.List<int> { 108 }
            });

            var generated = model.RollOrder(1, new DeterministicRandom(2u), null,
                new[] { 208, 207 }, 1);

            Assert.AreNotEqual(208, generated.ItemId,
                "不同普通合成链的 8 级元素也不能同时出现在订单栏");
        }

        [Test]
        public void GeneratedOrders_DoNotRepeatTheSameChainMaxLevelItem()
        {
            var catalog = new ItemCatalog();
            var model = new OrderModel(catalog);
            model.SetOrder(0, new Order
            {
                RequiredItemIds = new System.Collections.Generic.List<int> { 108, 101 }
            });

            var generated = model.RollOrder(1, new DeterministicRandom(2u), null,
                new[] { 108, 102 }, 1);

            Assert.AreNotEqual(108, generated.ItemId,
                "同一合成链的最高等级元素不能同时出现在多个激活订单中");
        }

        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(5, 3)]
        [TestCase(6, 3)]
        [TestCase(7, 4)]
        [TestCase(8, 4)]
        [TestCase(11, 6)]
        [TestCase(12, 6)]
        [TestCase(20, 10)]
        public void AvailableChains_FollowNewLevelRanges(int level, int expectedCount)
        {
            Assert.AreEqual(expectedCount, GameState.AvailableChainCountForLevel(level));
        }

        [Test]
        public void FixedOrders_Advance102To103To104ThenRandom()
        {
            var state = GameState.CreateNew(seed: 17u);
            for (int i = 0; i < BoardModel.CellCount; i++)
                state.Board.SetUnlockState(i, CellUnlockState.Unlocked);
            Assert.AreEqual(1, state.Orders.DefaultOrderProgress);
            Assert.AreEqual(102, state.Orders.GetOrder(0).ItemId);

            Assert.IsTrue(state.TrySubmitOrder(0, out _, out string error1), error1);
            Assert.AreEqual(103, state.Orders.GetOrder(0).ItemId);
            Assert.AreEqual(2, state.Orders.DefaultOrderProgress);

            Assert.IsTrue(state.TrySubmitOrder(0, out _, out string error2), error2);
            Assert.AreEqual(104, state.Orders.GetOrder(0).ItemId);
            Assert.AreEqual(3, state.Orders.DefaultOrderProgress);

            Assert.IsTrue(state.TrySubmitOrder(0, out _, out string error3), error3);
            Assert.AreNotEqual(104, state.Orders.GetOrder(0).ItemId);
            Assert.AreEqual(3, state.Orders.DefaultOrderProgress);
        }

        [Test]
        public void MultiItemOrder_RequiresOneOfEachItem()
        {
            var state = GameState.CreateNew(seed: 5u);
            state.Board.Clear();
            for (int i = 0; i < BoardModel.CellCount; i++)
                state.Board.SetUnlockState(i, CellUnlockState.Unlocked);
            state.Orders.SetOrder(0, new Order
            {
                RequiredItemIds = new System.Collections.Generic.List<int> { 101, 201 },
                RewardAmount = 25,
                GrantsExperience = true
            });
            state.Board.SetItemAt(0, 101);
            Assert.IsFalse(state.TrySubmitOrder(0, out _, out _));
            state.Board.SetItemAt(1, 201);
            Assert.IsTrue(state.TrySubmitOrder(0, out int reward, out _));
            Assert.AreEqual(38, reward); // 1～4 级多需求订单不触发激励限制，直接领取完整奖励
            Assert.AreEqual(0, state.Board.CountItem(101));
            Assert.AreEqual(0, state.Board.CountItem(201));
        }

        [Test]
        public void RewardedVideoPolicy_OnlyAppliesToHighLevelMultiItemOrders()
        {
            Assert.IsFalse(OrderRewardRules.ShouldRequireRewardedVideo(4, 2, new DeterministicRandom(2u)));
            Assert.IsFalse(OrderRewardRules.ShouldRequireRewardedVideo(5, 1, new DeterministicRandom(2u)));
            Assert.IsFalse(OrderRewardRules.ShouldRequireRewardedVideo(5, 4, new DeterministicRandom(2u)));
            Assert.IsTrue(OrderRewardRules.ShouldRequireRewardedVideo(5, 2, new DeterministicRandom(2u)));
            Assert.IsFalse(OrderRewardRules.ShouldRequireRewardedVideo(5, 2, new DeterministicRandom(1u)));
        }

        [Test]
        public void RolledOrder_PersistsRewardedVideoFlagFromEligibilityAndChance()
        {
            var catalog = new ItemCatalog();
            var candidates = new[] { 105, 205 }; // 两个 5 级元素，保证满足等级与数量条件。
            var model = new OrderModel(catalog);

            var gated = model.RollOrder(0, new DeterministicRandom(53u), null, candidates, 11);
            Assert.AreEqual(2, gated.GetRequiredItemIds().Count);
            Assert.IsTrue(gated.RequiresRewardedVideo);

            var ordinaryModel = new OrderModel(catalog);
            var ordinary = ordinaryModel.RollOrder(0, new DeterministicRandom(49u), null, candidates, 11);
            Assert.AreEqual(2, ordinary.GetRequiredItemIds().Count);
            Assert.IsFalse(ordinary.RequiresRewardedVideo);
        }

        [Test]
        public void NonRewardGatedMultiItemOrder_DirectSubmissionPaysFullAmount()
        {
            var state = GameState.CreateNew(seed: 5u);
            state.Board.Clear();
            for (int i = 0; i < BoardModel.CellCount; i++)
                state.Board.SetUnlockState(i, CellUnlockState.Unlocked);
            state.Orders.SetOrder(0, new Order
            {
                RequiredItemIds = new System.Collections.Generic.List<int> { 101, 201 },
                RewardAmount = 25,
                GrantsExperience = true
            });
            state.Board.SetItemAt(0, 101);
            state.Board.SetItemAt(1, 201);

            Assert.IsTrue(state.TrySubmitOrder(0, out int reward, out string error), error);
            Assert.AreEqual(38, reward);
        }

        [Test]
        public void RewardedVideoOrderCompletionPaysFullAmountAndExperience()
        {
            var state = GameState.CreateNew(seed: 5u);
            state.Board.Clear();
            for (int i = 0; i < BoardModel.CellCount; i++)
                state.Board.SetUnlockState(i, CellUnlockState.Unlocked);
            state.Orders.SetOrder(0, new Order
            {
                ItemId = 101,
                RewardAmount = 32,
                GrantsExperience = true
            });
            state.Board.SetItemAt(0, 101);

            Assert.IsTrue(state.TrySubmitOrderWithRewardedVideo(0, out int reward, out string error), error);
            Assert.AreEqual(32, reward);
            Assert.AreEqual(1, state.LevelProgress);
            Assert.AreEqual(32, state.Coins);
        }

        [Test]
        public void FirstNewLevelFourMerge_SpawnsOneSpecialLevelOne()
        {
            var state = GameState.CreateNew(seed: 3u);
            for (int i = 0; i < BoardModel.CellCount; i++)
                state.Board.SetUnlockState(i, CellUnlockState.Unlocked);
            state.Board.SetItemAt(0, 101);
            state.Board.SetItemAt(1, 101);
            Assert.IsTrue(state.HandleDrop(0, 1).Type == DropResultType.Merged);
            Assert.AreEqual(0, state.Board.CountItem(ItemCatalog.SpecialLevelOneId));

            state.Board.SetItemAt(2, 102);
            state.Board.SetItemAt(3, 102);
            Assert.IsTrue(state.HandleDrop(2, 3).Type == DropResultType.Merged);
            Assert.AreEqual(0, state.Board.CountItem(ItemCatalog.SpecialLevelOneId));

            state.Board.SetItemAt(2, 103);
            state.Board.SetItemAt(3, 103);
            Assert.IsTrue(state.HandleDrop(2, 3).Type == DropResultType.Merged);
            Assert.AreEqual(1, state.Board.CountItem(ItemCatalog.SpecialLevelOneId));
        }

        [Test]
        public void SpecialSpawnRules_FirstNewLevelsFourToEightAlwaysSpawnLevelOne()
        {
            for (int level = 4; level <= 8; level++)
                Assert.AreEqual(1, SpecialSpawnRules.Roll(level, true, new DeterministicRandom(1u)),
                    $"首次合成新普通 L{level} 应生成特殊链 1 级");
        }

        [Test]
        public void SpecialSpawnRules_RepeatLevelSixUsesFiftyPercentLevelOne()
        {
            Assert.AreEqual(1, SpecialSpawnRules.Roll(6, false, new DeterministicRandom(2u)));
            Assert.AreEqual(0, SpecialSpawnRules.Roll(6, false, new DeterministicRandom(1u)));
        }

        [Test]
        public void SpecialSpawnRules_RepeatLevelSevenUsesTwentyFivePercentEach()
        {
            Assert.AreEqual(1, SpecialSpawnRules.Roll(7, false, new DeterministicRandom(3u)));
            Assert.AreEqual(2, SpecialSpawnRules.Roll(7, false, new DeterministicRandom(2u)));
            Assert.AreEqual(0, SpecialSpawnRules.Roll(7, false, new DeterministicRandom(1u)));
        }

        [Test]
        public void SpecialSpawnRules_RepeatLevelEightUsesFortyAndTenPercent()
        {
            Assert.AreEqual(2, SpecialSpawnRules.Roll(8, false, new DeterministicRandom(2u)));
            Assert.AreEqual(3, SpecialSpawnRules.Roll(8, false, new DeterministicRandom(17u)));
            Assert.AreEqual(0, SpecialSpawnRules.Roll(8, false, new DeterministicRandom(1u)));
        }

        [Test]
        public void SpecialChain_HasFiveLevels_AndNeverEntersOrderCandidates()
        {
            var catalog = new ItemCatalog();
            Assert.AreEqual(5, ItemCatalog.SpecialMaxLevel);
            Assert.IsTrue(catalog.IsSpecialItem(ItemCatalog.SpecialLevelOneId));
            Assert.AreEqual(ItemCatalog.EmptyId, catalog.GetNextId(ItemCatalog.SpecialLevelFiveId));
            Assert.IsFalse(catalog.GetOrderCandidates().Any(item => catalog.IsSpecialItem(item.Id)));
        }

        [TestCase(1, 2)]
        [TestCase(2, 5)]
        [TestCase(3, 12)]
        [TestCase(4, 28)]
        [TestCase(5, 60)]
        public void SpecialCashoutAmounts_FollowUpdatedTable(int level, int expectedAmount)
        {
            var catalog = new ItemCatalog();
            Assert.AreEqual(expectedAmount, catalog.GetSpecialCashoutAmount(1100 + level));
        }

        [Test]
        public void OrderRefresh_IsCappedByRemainingSlots()
        {
            var state = GameState.CreateNew(seed: 9u);
            state.SetLevel(3, 0);
            state.SetNextOrderRefreshUtc(System.DateTimeOffset.UtcNow.AddSeconds(-1));
            int added = state.TickOrders(System.DateTimeOffset.UtcNow);
            Assert.GreaterOrEqual(added, 1);
            Assert.LessOrEqual(state.Orders.ActiveCount, 3);

            state.SetNextOrderRefreshUtc(System.DateTimeOffset.UtcNow.AddSeconds(-1));
            state.TickOrders(System.DateTimeOffset.UtcNow);
            state.SetNextOrderRefreshUtc(System.DateTimeOffset.UtcNow.AddSeconds(-1));
            state.TickOrders(System.DateTimeOffset.UtcNow);
            Assert.AreEqual(3, state.Orders.ActiveCount);
            state.SetNextOrderRefreshUtc(System.DateTimeOffset.UtcNow.AddSeconds(-1));
            Assert.AreEqual(0, state.TickOrders(System.DateTimeOffset.UtcNow));
        }

        [Test]
        public void SpecialItem_DoubleClickCashout_AddsMappedCoinsAndRemovesItem()
        {
            var state = GameState.CreateNew(seed: 19u);
            state.Board.SetItemAt(0, 1103);

            Assert.IsTrue(state.TryCashOutSpecialItem(0, out int amount, out _));
            Assert.AreEqual(12, amount);
            Assert.AreEqual(12, state.Coins);
            Assert.AreEqual(ItemCatalog.EmptyId, state.Board.GetItemAt(0));
        }

        [Test]
        public void NormalItem_DoubleClickCashout_IsRejectedWithoutMutation()
        {
            var state = GameState.CreateNew(seed: 19u);
            state.Board.SetItemAt(0, 101);

            Assert.IsFalse(state.TryCashOutSpecialItem(0, out int amount, out _));
            Assert.AreEqual(0, amount);
            Assert.AreEqual(101, state.Board.GetItemAt(0));
            Assert.AreEqual(0, state.Coins);
        }

        [Test]
        public void OrderRefreshInterval_IsRandomizedBetweenFiveAndTenSeconds()
        {
            Assert.AreEqual(5, GameState.OrderRefreshMinSeconds);
            Assert.AreEqual(10, GameState.OrderRefreshMaxSeconds);
            var state = GameState.CreateNew(seed: 29u);
            var now = DateTimeOffset.UtcNow;
            state.SetNextOrderRefreshUtc(now.AddSeconds(-1));
            state.TickOrders(now);
            Assert.That((state.NextOrderRefreshUtc - now).TotalSeconds, Is.InRange(5d, 10d));
        }

        [Test]
        public void OrderLayout_OnlyScrollsWhenFourOrFiveOrdersAreActive()
        {
            Assert.AreEqual(3f, OrderLayoutRules.VisibleCardCount);
            Assert.IsFalse(OrderLayoutRules.IsScrollable(1));
            Assert.IsFalse(OrderLayoutRules.IsScrollable(3));
            Assert.IsTrue(OrderLayoutRules.IsScrollable(4));
            Assert.IsTrue(OrderLayoutRules.IsScrollable(5));
        }

        [TestCase(1, 20)]
        [TestCase(2, 30)]
        [TestCase(3, 40)]
        public void OrderReward_UsesRequirementCountMultiplier(int requirementCount, int expectedReward)
        {
            var required = Enumerable.Range(1, requirementCount)
                .Select(chain => chain * 100 + 1)
                .ToList();
            var order = new Order
            {
                RequiredItemIds = required,
                RewardAmount = 20
            };

            Assert.AreEqual(expectedReward, order.RewardCoins(new ItemCatalog()));
        }

        [TestCase(1, 5, 10)]
        [TestCase(2, 10, 15)]
        [TestCase(3, 15, 20)]
        [TestCase(4, 20, 25)]
        [TestCase(5, 25, 30)]
        [TestCase(6, 30, 40)]
        [TestCase(7, 35, 45)]
        [TestCase(8, 40, 50)]
        public void GeneratedOrderBaseReward_ScalesWithRequiredItemLevel(int itemLevel,
            int expectedMin, int expectedMax)
        {
            var catalog = new ItemCatalog();
            var model = new OrderModel(catalog);
            var order = model.RollOrder(0, new DeterministicRandom((uint)(100 + itemLevel)), null,
                new[] { 100 + itemLevel }, 1);

            Assert.That(order.RewardAmount, Is.InRange(expectedMin, expectedMax));
        }

        [Test]
        public void OrderReward_RoundsHalfUpToWholeCoin()
        {
            var order = new Order
            {
                RequiredItemIds = new System.Collections.Generic.List<int> { 101, 201 },
                RewardAmount = 21
            };

            Assert.AreEqual(32, order.RewardCoins(new ItemCatalog()));
        }

        [Test]
        public void PureMoneyOrder_UsesRequirementMultiplier()
        {
            var order = new Order
            {
                RequiredItemIds = new System.Collections.Generic.List<int> { 101, 201 },
                RewardAmount = 25,
                GrantsExperience = false
            };

            Assert.AreEqual(38, order.RewardCoins(new ItemCatalog()));
        }
    }
}
