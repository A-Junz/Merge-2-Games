using System;
using System.Collections.Generic;
using System.IO;
using MergePrototype.Core;
using MergePrototype.Persistence;
using NUnit.Framework;

namespace MergePrototype.Tests.EditMode
{
    public class OrderModelTests
    {
        private static void UnlockAll(BoardModel board)
        {
            for (int i = 0; i < BoardModel.CellCount; i++)
                board.SetUnlockState(i, CellUnlockState.Unlocked);
        }

        [Test]
        public void EncyclopediaRewards_UseElementChainAndAllChainBonuses()
        {
            Assert.AreEqual(17, EncyclopediaRewardRules.CalculateTotalReward(7, 1, false));
            Assert.AreEqual(280, EncyclopediaRewardRules.CalculateTotalReward(80, 10, true));
        }

        [Test]
        public void EncyclopediaProgress_CountsUnlockedElementsAndCompletedChains()
        {
            var state = GameState.CreateNew(seed: 1u);
            var allItems = new List<int>();
            for (int chain = 1; chain <= ItemCatalog.ChainCount; chain++)
            {
                for (int level = 1; level <= ItemCatalog.MaxLevel; level++)
                    allItems.Add(chain * 100 + level);
            }

            state.RestoreSeenItems(allItems);

            Assert.AreEqual(80, state.EncyclopediaUnlockedElementCount);
            Assert.AreEqual(10, state.EncyclopediaCompletedChainCount);
            Assert.IsTrue(state.EncyclopediaAllChainsCompleted);
            Assert.AreEqual(280, state.EncyclopediaRewardTotal);
        }

        [Test]
        public void EncyclopediaChainReward_CanBeClaimedOnce()
        {
            var state = GameState.CreateNew(seed: 2u);
            var pizzaItems = new List<int>();
            for (int level = 1; level <= ItemCatalog.MaxLevel; level++)
                pizzaItems.Add(200 + level);
            state.RestoreSeenItems(pizzaItems);

            Assert.IsTrue(state.IsEncyclopediaChainCompleted("pizza"));
            Assert.IsTrue(state.CanClaimEncyclopediaChainReward("pizza"));
            int coinsBefore = state.Coins;

            Assert.IsTrue(state.TryClaimEncyclopediaChainReward("pizza", out int reward, out string error), error);
            Assert.AreEqual(EncyclopediaRewardRules.ChainCompletionReward, reward);
            Assert.AreEqual(coinsBefore + reward, state.Coins);
            Assert.IsTrue(state.IsEncyclopediaChainRewardClaimed("pizza"));
            Assert.IsFalse(state.CanClaimEncyclopediaChainReward("pizza"));
            Assert.IsFalse(state.TryClaimEncyclopediaChainReward("pizza", out _, out _));
        }

        [Test]
        public void EncyclopediaAllCompletionReward_CanBeClaimedOnce()
        {
            var state = GameState.CreateNew(seed: 3u);
            var allItems = new List<int>();
            for (int chain = 1; chain <= ItemCatalog.ChainCount; chain++)
            {
                for (int level = 1; level <= ItemCatalog.MaxLevel; level++)
                    allItems.Add(chain * 100 + level);
            }
            state.RestoreSeenItems(allItems);

            Assert.IsTrue(state.EncyclopediaAllChainsCompleted);
            Assert.IsTrue(state.CanClaimEncyclopediaAllReward());
            int coinsBefore = state.Coins;

            Assert.IsTrue(state.TryClaimEncyclopediaAllReward(out int reward, out string error), error);
            Assert.AreEqual(EncyclopediaRewardRules.AllChainsCompletionReward, reward);
            Assert.AreEqual(coinsBefore + reward, state.Coins);
            Assert.IsTrue(state.IsEncyclopediaAllRewardClaimed);
            Assert.IsFalse(state.CanClaimEncyclopediaAllReward());
            Assert.IsFalse(state.TryClaimEncyclopediaAllReward(out _, out _));
        }

        [Test]
        public void EncyclopediaItemNames_AreEnglish()
        {
            var catalog = new ItemCatalog();
            foreach (var def in catalog.AllItems)
            {
                if (def.ChainId == "special") continue;
                foreach (char character in def.DisplayName)
                    Assert.Less((int)character, 128, $"Item {def.Id} is not using English text");
            }
        }

        [Test]
        public void OrderPriority_FullyFulfilledThenProgressThenOriginalSlot()
        {
            Assert.Less(OrderLayoutRules.ComparePriority(true, 0, 2, false, 2, 0), 0);
            Assert.Less(OrderLayoutRules.ComparePriority(false, 2, 1, false, 1, 0), 0);
            Assert.Less(OrderLayoutRules.ComparePriority(false, 1, 1, false, 1, 2), 0);
        }

        [Test]
        public void Orders_FiveSlots_WithInactiveSlotsAfterInitialOne()
        {
            var state = GameState.CreateNew(seed: 123u);
            Assert.AreEqual(1, state.Orders.ActiveCount);
            for (int i = 0; i < OrderModel.SlotCount; i++)
            {
                var order = state.Orders.GetOrder(i);
                if (i >= state.Orders.ActiveCount)
                {
                    Assert.IsNull(order);
                    continue;
                }
                Assert.IsNotNull(order);
                Assert.IsTrue(state.Catalog.IsValidItem(order.ItemId));
                var def = state.Catalog.Get(order.ItemId);
                Assert.IsTrue(def.Level >= 1 && def.Level <= 4, $"订单物品等级越界: L{def.Level}");
                Assert.IsTrue(order.Count == 1 || order.Count == 2);
            }
        }

        [Test]
        public void Submit_AdvancesLevelProgress_LevelsUpAfterRequiredExperience()
        {
            var state = GameState.CreateNew(seed: 55u);
            state.Board.Clear();
            UnlockAll(state.Board);
            Assert.AreEqual(1, state.Level);
            Assert.AreEqual(0, state.LevelProgress);
            Assert.AreEqual(3, GameState.ExperienceRequiredForNextLevel(1));

            for (int i = 0; i < GameState.ExperienceRequiredForNextLevel(1); i++)
            {
                // 每单都强制可满足：固定订单 + 摆上所需物品。
                state.Orders.SetOrder(0, new Order { ItemId = 101, Count = 1 });
                state.Board.SetItemAt(i, 101);
                Assert.IsTrue(state.TrySubmitOrder(0, out _, out _), $"第 {i + 1} 单应可提交");
            }

            Assert.AreEqual(2, state.Level, "完成 3 个经验订单应升到 2 级");
            Assert.AreEqual(0, state.LevelProgress, "升级后进度应清零");
        }

        [Test]
        public void Submit_LevelUp_QueuesUpgradeRewardUntilClaimed()
        {
            var state = GameState.CreateNew(seed: 55u);
            state.Board.Clear();
            UnlockAll(state.Board);
            state.SetLevel(1, GameState.ExperienceRequiredForNextLevel(1) - 1);
            state.Orders.SetActiveCount(1);
            state.Orders.SetOrder(0, new Order { ItemId = 101, Count = 1, RewardAmount = 5 });
            state.Board.SetItemAt(0, 101);

            Assert.IsTrue(state.TrySubmitOrder(0, out int orderReward, out string error), error);

            Assert.AreEqual(5, orderReward, "提交订单返回值只应包含本订单已发放的金额");
            Assert.AreEqual(5, state.Coins, "升级奖励在弹窗领取前不应进入金币");
            Assert.AreEqual(2, state.Level);
            Assert.Greater(state.PendingLevelUpReward, 0);
            Assert.AreEqual(OrderRewardRules.CalculateDirectReward(state.PendingLevelUpReward),
                state.PendingLevelUpDirectReward);
        }

        [Test]
        public void ClaimPendingLevelUpReward_DirectPaysOneTenthAndClearsPendingReward()
        {
            var state = GameState.CreateNew(seed: 55u);
            state.Board.Clear();
            UnlockAll(state.Board);
            state.SetLevel(1, GameState.ExperienceRequiredForNextLevel(1) - 1);
            state.Orders.SetActiveCount(1);
            state.Orders.SetOrder(0, new Order { ItemId = 101, Count = 1, RewardAmount = 5 });
            state.Board.SetItemAt(0, 101);
            Assert.IsTrue(state.TrySubmitOrder(0, out _, out string submitError), submitError);

            int pending = state.PendingLevelUpReward;
            int coinsBeforeClaim = state.Coins;
            Assert.IsTrue(state.TryClaimPendingLevelUpReward(OrderRewardClaimMode.Direct,
                out int claimed, out string error), error);

            Assert.AreEqual(OrderRewardRules.CalculateDirectReward(pending), claimed);
            Assert.AreEqual(coinsBeforeClaim + claimed, state.Coins);
            Assert.AreEqual(0, state.PendingLevelUpReward);
            Assert.AreEqual(0, state.PendingLevelUpDirectReward);
        }

        [Test]
        public void ClaimPendingLevelUpReward_RewardedVideoPaysFullAmountAndClearsPendingReward()
        {
            var state = GameState.CreateNew(seed: 55u);
            state.Board.Clear();
            UnlockAll(state.Board);
            state.SetLevel(1, GameState.ExperienceRequiredForNextLevel(1) - 1);
            state.Orders.SetActiveCount(1);
            state.Orders.SetOrder(0, new Order { ItemId = 101, Count = 1, RewardAmount = 5 });
            state.Board.SetItemAt(0, 101);
            Assert.IsTrue(state.TrySubmitOrder(0, out _, out string submitError), submitError);

            int pending = state.PendingLevelUpReward;
            int coinsBeforeClaim = state.Coins;
            Assert.IsTrue(state.TryClaimPendingLevelUpReward(OrderRewardClaimMode.RewardedVideo,
                out int claimed, out string error), error);

            Assert.AreEqual(pending, claimed);
            Assert.AreEqual(coinsBeforeClaim + pending, state.Coins);
            Assert.AreEqual(0, state.PendingLevelUpReward);
        }

        [Test]
        public void Submit_NotFulfilled_DoesNotAdvanceLevel()
        {
            var state = GameState.CreateNew(seed: 55u);
            state.Board.Clear();
            UnlockAll(state.Board);
            state.Orders.SetActiveCount(1);
            state.Orders.SetOrder(0, new Order { ItemId = 101, Count = 2 });
            Assert.IsFalse(state.TrySubmitOrder(0, out _, out _));
            Assert.AreEqual(1, state.Level);
            Assert.AreEqual(0, state.LevelProgress);
        }

        [Test]
        public void SetLevel_ClampsValues()
        {
            var state = GameState.CreateNew(seed: 1u);
            state.SetLevel(0, 99); // 非法输入
            Assert.AreEqual(1, state.Level);
            Assert.AreEqual(GameState.ExperienceRequiredForNextLevel(1) - 1, state.LevelProgress);
            state.SetLevel(5, 3);
            Assert.AreEqual(5, state.Level);
            Assert.AreEqual(3, state.LevelProgress);
        }

        [Test]
        public void Reward_Formula_UsesRequirementCountMultiplier()
        {
            var catalog = new ItemCatalog();
            var order = new Order { ItemId = 103, Count = 2 }; // L3，数量 2
            Assert.AreEqual(23, order.RewardCoins(catalog)); // L3 基础金额 15 × 1.5，四舍五入
            order = new Order { ItemId = 101, Count = 1 };
            Assert.AreEqual(5, order.RewardCoins(catalog)); // L1 基础金额 5
            order = new Order { ItemId = 204, Count = 2 }; // L4
            Assert.AreEqual(30, order.RewardCoins(catalog)); // L4 基础金额 20 × 1.5
        }

        [Test]
        public void Submit_Fulfilled_RemovesExactCount_PaysReward_ReplacesOrder()
        {
            var state = GameState.CreateNew(seed: 55u);
            state.Board.Clear();
            UnlockAll(state.Board);
            state.Orders.SetActiveCount(1); // 只剩 1 单时，完成后立即补出新订单。
            state.Orders.SetOrder(0, new Order { ItemId = 101, Count = 2 });
            state.Board.SetItemAt(0, 101);
            state.Board.SetItemAt(1, 101);
            state.Board.SetItemAt(2, 101); // 多出的 1 个不应被移除
            int coinsBefore = state.Coins;

            bool ok = state.TrySubmitOrder(0, out int reward, out string error);
            Assert.IsTrue(ok, error);
            Assert.AreEqual(8, reward); // 低等级多需求订单不触发激励限制，直接领取完整奖励
            Assert.AreEqual(coinsBefore + 8, state.Coins);
            Assert.AreEqual(1, state.Board.CountItem(101), "应精确移除 2 个");

            var newOrder = state.Orders.GetOrder(0);
            Assert.IsFalse(newOrder.ItemId == 101 && newOrder.Count == 2, "新订单不应与原订单完全相同");
        }

        [Test]
        public void Submit_DirectCompletionPaysOneTenthButKeepsExperience()
        {
            var state = GameState.CreateNew(seed: 55u);
            state.Board.Clear();
            UnlockAll(state.Board);
            state.Orders.SetActiveCount(1);
            state.Orders.SetOrder(0, new Order
            {
                ItemId = 101,
                Count = 1,
                RewardAmount = 32,
                RequiresRewardedVideo = true
            });
            state.Board.SetItemAt(0, 101);

            bool ok = state.TrySubmitOrder(0, out int reward, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(3, reward, "直接完成应获得完整金额的十分之一（32 / 10 向下取整）");
            Assert.AreEqual(1, state.LevelProgress, "直接完成不应减少经验");
            Assert.AreEqual(3, state.Coins);
        }

        [Test]
        public void Submit_NotFulfilled_ChangesNothing()
        {
            var state = GameState.CreateNew(seed: 55u);
            state.Board.Clear();
            state.Orders.SetOrder(0, new Order { ItemId = 101, Count = 2 });
            state.Board.SetItemAt(0, 101); // 只有 1 个
            int coinsBefore = state.Coins;

            bool ok = state.TrySubmitOrder(0, out int reward, out string error);
            Assert.IsFalse(ok);
            Assert.AreEqual(0, reward);
            StringAssert.Contains("不足", error);
            Assert.AreEqual(1, state.Board.CountItem(101));
            Assert.AreEqual(coinsBefore, state.Coins);
            Assert.AreEqual(101, state.Orders.GetOrder(0).ItemId); // 订单未替换
        }

        [Test]
        public void Submit_InvalidSlot_Fails()
        {
            var state = GameState.CreateNew(seed: 55u);
            Assert.IsFalse(state.TrySubmitOrder(-1, out _, out _));
            Assert.IsFalse(state.TrySubmitOrder(3, out _, out _));
        }

        [Test]
        public void Submit_IgnoresItemsInSemiAndLockedCells()
        {
            var state = GameState.CreateNew(seed: 55u);
            state.Board.Clear();
            state.Orders.SetActiveCount(1);
            state.Orders.SetOrder(0, new Order { ItemId = 101, Count = 1 });
            state.Board.SetUnlockState(0, CellUnlockState.Locked);
            state.Board.SetUnlockState(1, CellUnlockState.SemiUnlocked);
            state.Board.SetUnlockState(2, CellUnlockState.Unlocked);
            state.Board.SetItemAt(0, 101);
            state.Board.SetItemAt(1, 101);

            Assert.AreEqual(0, state.Orders.GetProgress(0, state.Board));
            Assert.IsFalse(state.TrySubmitOrder(0, out _, out _));
            Assert.AreEqual(101, state.Board.GetItemAt(0));
            Assert.AreEqual(101, state.Board.GetItemAt(1));

            state.Board.SetItemAt(2, 101);
            Assert.IsTrue(state.TrySubmitOrder(0, out _, out _));
            Assert.AreEqual(101, state.Board.GetItemAt(0));
            Assert.AreEqual(101, state.Board.GetItemAt(1));
            Assert.AreEqual(ItemCatalog.EmptyId, state.Board.GetItemAt(2));
        }

        [Test]
        public void Rng_SameSeed_ProducesSameOrderSequence()
        {
            var a = GameState.CreateNew(seed: 999u);
            var b = GameState.CreateNew(seed: 999u);
            for (int i = 0; i < a.Orders.ActiveCount; i++)
            {
                Assert.AreEqual(a.Orders.GetOrder(i).ItemId, b.Orders.GetOrder(i).ItemId);
                Assert.AreEqual(a.Orders.GetOrder(i).Count, b.Orders.GetOrder(i).Count);
            }
        }
    }

    public class SaveServiceTests
    {
        private string _tempFile;
        private SaveService _saveService;

        [SetUp]
        public void SetUp()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), $"merge-test-{Guid.NewGuid():N}.json");
            _saveService = new SaveService(_tempFile);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (File.Exists(_tempFile)) File.Delete(_tempFile); } catch { /* 忽略 */ }
        }

        [Test]
        public void Load_MissingFile_CreatesNewGameWithInitialState()
        {
            var state = _saveService.LoadOrCreateNew();
            Assert.IsNull(_saveService.LastFallbackReason);
            Assert.AreEqual(30, state.Energy.Current);
            Assert.AreEqual(0, state.Coins);
            Assert.AreEqual(0, state.Board.GetEmptyIndices().Count);
            Assert.AreEqual(CellUnlockState.Unlocked, state.Board.GetUnlockState(31));
            Assert.AreEqual(CellUnlockState.SemiUnlocked, state.Board.GetUnlockState(24));
            Assert.IsNotNull(state.Orders.GetOrder(0));
        }

        [Test]
        public void SaveLoad_RoundTrip_PreservesAllKeyState()
        {
            var state = GameState.CreateNew(seed: 77u);
            state.Board.SetItemAt(0, 101);
            state.Board.SetItemAt(20, 203);
            state.Board.SetUnlockState(0, CellUnlockState.Unlocked);
            state.Board.SetUnlockState(20, CellUnlockState.SemiUnlocked);
            state.SetCoins(350);
            var pizzaItems = new List<int>();
            for (int level = 1; level <= ItemCatalog.MaxLevel; level++)
                pizzaItems.Add(200 + level);
            state.RestoreSeenItems(pizzaItems);
            state.RestoreClaimedEncyclopediaChainIds(new[] { "pizza" });
            state.ResetEnergy(12, new DateTimeOffset(2026, 9, 18, 8, 0, 0, TimeSpan.Zero));
            state.Orders.SetOrder(1, new Order
            {
                ItemId = 104,
                Count = 2,
                RequiresRewardedVideo = true
            });
            state.Orders.SetDefaultOrderProgress(2);
            state.SetLevel(4, 5);
            state.RestorePendingLevelUpReward(37);
            uint rngState = state.Rng.State;

            Assert.IsTrue(_saveService.Save(state, out string error), error);

            var loaded = _saveService.LoadOrCreateNew();
            Assert.IsNull(_saveService.LastFallbackReason);
            Assert.AreEqual(101, loaded.Board.GetItemAt(0));
            Assert.AreEqual(203, loaded.Board.GetItemAt(20));
            Assert.AreEqual(350, loaded.Coins);
            Assert.IsTrue(loaded.IsEncyclopediaChainRewardClaimed("pizza"));
            Assert.IsFalse(loaded.CanClaimEncyclopediaChainReward("pizza"));
            Assert.AreEqual(37, loaded.PendingLevelUpReward);
            Assert.AreEqual(3, loaded.PendingLevelUpDirectReward);
            Assert.AreEqual(104, loaded.Orders.GetOrder(1).ItemId);
            Assert.AreEqual(2, loaded.Orders.GetOrder(1).Count);
            Assert.IsTrue(loaded.Orders.GetOrder(1).RequiresRewardedVideo);
            Assert.AreEqual(CellUnlockState.Unlocked, loaded.Board.GetUnlockState(0));
            Assert.AreEqual(CellUnlockState.SemiUnlocked, loaded.Board.GetUnlockState(20));
            Assert.AreEqual(2, loaded.Orders.DefaultOrderProgress);
            Assert.AreEqual(rngState, loaded.Rng.State);
            Assert.AreEqual(4, loaded.Level);
            Assert.AreEqual(4, loaded.LevelProgress);

            // 能量：存档时刻 12 点，基准 08:00；加载时按当前时间离线恢复，应不少于 12。
            Assert.GreaterOrEqual(loaded.Energy.Current, 12);
            Assert.LessOrEqual(loaded.Energy.Current, EnergyModel.MaxEnergy);
        }

        [Test]
        public void SaveLoad_PreservesEncyclopediaAllRewardClaim()
        {
            var state = GameState.CreateNew(seed: 88u);
            var allItems = new List<int>();
            for (int chain = 1; chain <= ItemCatalog.ChainCount; chain++)
            {
                for (int level = 1; level <= ItemCatalog.MaxLevel; level++)
                    allItems.Add(chain * 100 + level);
            }
            state.RestoreSeenItems(allItems);
            Assert.IsTrue(state.TryClaimEncyclopediaAllReward(out _, out string claimError), claimError);
            Assert.IsTrue(_saveService.Save(state, out string saveError), saveError);

            var loaded = _saveService.LoadOrCreateNew();
            Assert.IsTrue(loaded.IsEncyclopediaAllRewardClaimed);
            Assert.IsFalse(loaded.CanClaimEncyclopediaAllReward());
        }

        [Test]
        public void Load_OfflineEnergy_AccruesWithElapsedTime_AndCapped()
        {
            var state = GameState.CreateNew(seed: 1u);
            state.ResetEnergy(29, DateTimeOffset.UtcNow.AddHours(-3));
            Assert.IsTrue(_saveService.Save(state, out _));

            var loaded = _saveService.LoadOrCreateNew();
            Assert.AreEqual(EnergyModel.MaxEnergy, loaded.Energy.Current);
        }

        [Test]
        public void Load_CorruptedJson_FallsBackToNewGame()
        {
            File.WriteAllText(_tempFile, "{ this is not valid json !!!");
            var state = _saveService.LoadOrCreateNew();
            Assert.IsNotNull(_saveService.LastFallbackReason);
            Assert.AreEqual(30, state.Energy.Current); // 可用的新游戏状态
        }

        [Test]
        public void Load_UnsupportedVersion_FallsBack()
        {
            File.WriteAllText(_tempFile,
                "{\"version\":999,\"board\":[],\"energy\":30,\"coins\":0,\"orders\":[],\"lastEnergyUtcTicks\":\"0\",\"rngState\":1}");
            _saveService.LoadOrCreateNew();
            Assert.IsNotNull(_saveService.LastFallbackReason);
            StringAssert.Contains("版本", _saveService.LastFallbackReason);
        }

        [Test]
        public void Load_WrongCellCount_FallsBack()
        {
            var ticks = DateTimeOffset.UtcNow.UtcTicks;
            File.WriteAllText(_tempFile,
                "{\"version\":1,\"board\":[0,0,0],\"energy\":30,\"coins\":0," +
                "\"orders\":[{\"itemId\":101,\"count\":1},{\"itemId\":101,\"count\":1},{\"itemId\":101,\"count\":1}]," +
                $"\"lastEnergyUtcTicks\":\"{ticks}\",\"rngState\":1}}");
            _saveService.LoadOrCreateNew();
            Assert.IsNotNull(_saveService.LastFallbackReason);
            StringAssert.Contains("格数", _saveService.LastFallbackReason);
        }

        [Test]
        public void Load_InvalidBoardItemId_FallsBack()
        {
            var ticks = DateTimeOffset.UtcNow.UtcTicks;
            var board = new int[BoardModel.CellCount];
            board[0] = 999999;
            var boardJson = string.Join(",", board);
            File.WriteAllText(_tempFile,
                "{\"version\":1,\"board\":[" + boardJson + "],\"energy\":30,\"coins\":0," +
                "\"orders\":[{\"itemId\":101,\"count\":1},{\"itemId\":101,\"count\":1},{\"itemId\":101,\"count\":1}]," +
                $"\"lastEnergyUtcTicks\":\"{ticks}\",\"rngState\":1}}");

            var state = _saveService.LoadOrCreateNew();

            Assert.IsNotNull(_saveService.LastFallbackReason);
            StringAssert.Contains("棋盘物品", _saveService.LastFallbackReason);
            Assert.AreEqual(101, state.Board.GetItemAt(31));
        }

        [Test]
        public void Load_InvalidOrderCount_FallsBack()
        {
            var ticks = DateTimeOffset.UtcNow.UtcTicks;
            var boardJson = string.Join(",", new int[BoardModel.CellCount]);
            File.WriteAllText(_tempFile,
                "{\"version\":1,\"board\":[" + boardJson + "],\"energy\":30,\"coins\":0," +
                "\"orders\":[{\"itemId\":101,\"count\":0},{\"itemId\":102,\"count\":1},{\"itemId\":103,\"count\":1}]," +
                $"\"lastEnergyUtcTicks\":\"{ticks}\",\"rngState\":1}}");

            var state = _saveService.LoadOrCreateNew();

            Assert.IsNotNull(_saveService.LastFallbackReason);
            StringAssert.Contains("订单数量", _saveService.LastFallbackReason);
            Assert.AreEqual(EnergyModel.InitialEnergy, state.Energy.Current);
        }

        [Test]
        public void Load_OldSaveWithoutLevelFields_DefaultsToLevel1()
        {
            // 模拟等级系统上线前的旧存档：无 level/levelProgress 字段。
            var ticks = DateTimeOffset.UtcNow.UtcTicks;
            var boardJson = string.Join(",", new int[BoardModel.CellCount]);
            File.WriteAllText(_tempFile,
                "{\"version\":1,\"board\":[" + boardJson + "],\"energy\":30,\"coins\":10," +
                "\"orders\":[{\"itemId\":101,\"count\":1},{\"itemId\":102,\"count\":1},{\"itemId\":103,\"count\":1}]," +
                $"\"lastEnergyUtcTicks\":\"{ticks}\",\"rngState\":1}}");
            var state = _saveService.LoadOrCreateNew();
            Assert.IsNull(_saveService.LastFallbackReason);
            Assert.AreEqual(1, state.Level);
            Assert.AreEqual(0, state.LevelProgress);
        }

        [Test]
        public void Save_CreatesFileAtPath()
        {
            var state = GameState.CreateNew(seed: 5u);
            Assert.IsTrue(_saveService.Save(state, out _));
            Assert.IsTrue(File.Exists(_tempFile));
        }
    }
}
