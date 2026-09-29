using System;
using System.Linq;
using MergePrototype.Core;
using NUnit.Framework;

namespace MergePrototype.Tests.EditMode
{
    public class GeneratorAndEnergyTests
    {
        [Test]
        public void Generate_Success_FillsAllEmptyCellsWithoutEnergyCost()
        {
            var state = GameState.CreateNew(seed: 42u);
            ClearBoard(state);
            int energyBefore = state.Energy.Current;
            bool ok = state.TryGenerate(out string error);
            Assert.IsTrue(ok, error);
            Assert.AreEqual(energyBefore, state.Energy.Current);
            Assert.AreEqual(0, state.Board.GetEmptyIndices().Count);
        }

        [Test]
        public void Generate_ProducesOnlyNormalItemsUpToLevelFour()
        {
            var state = GameState.CreateNew(seed: 7u);
            ClearBoard(state);
            Assert.IsTrue(state.TryGenerate(out _));
            var snapshot = state.Board.Snapshot();
            foreach (int id in snapshot)
            {
                if (id == ItemCatalog.EmptyId) continue;
                var def = state.Catalog.Get(id);
                Assert.IsFalse(state.Catalog.IsSpecialItem(id));
                Assert.LessOrEqual(def.Level, 4, $"发放超出等级权重池: {id}");
            }
        }

        [Test]
        public void Generate_ZeroEnergy_StillFillsBoard()
        {
            var state = GameState.CreateNew(seed: 1u);
            ClearBoard(state);
            state.ResetEnergy(0, DateTimeOffset.UtcNow);
            bool ok = state.TryGenerate(out string error);
            Assert.IsTrue(ok, error);
            Assert.AreEqual(0, state.Energy.Current);
            Assert.AreEqual(0, state.Board.GetEmptyIndices().Count);
        }

        [Test]
        public void Generate_FillsOnlyEmptyCellsInBoardIndexOrder()
        {
            var state = GameState.CreateNew(seed: 2u);
            state.Board.SetItemAt(1, ItemCatalog.EmptyId);
            state.Board.SetItemAt(3, ItemCatalog.EmptyId);
            state.Board.SetItemAt(0, 108);
            state.Board.SetItemAt(2, 208);

            Assert.IsTrue(state.TryGenerate(out string error), error);

            Assert.AreEqual(108, state.Board.GetItemAt(0));
            Assert.AreEqual(208, state.Board.GetItemAt(2));
            Assert.AreNotEqual(ItemCatalog.EmptyId, state.Board.GetItemAt(1));
            Assert.AreNotEqual(ItemCatalog.EmptyId, state.Board.GetItemAt(3));
            Assert.AreEqual(0, state.Board.GetEmptyIndices().Count);
        }

        [Test]
        public void Generate_FullBoard_FailsWithoutSpendingEnergy()
        {
            var state = GameState.CreateNew(seed: 1u);
            for (int i = 0; i < BoardModel.CellCount; i++)
            {
                state.Board.SetItemAt(i, 101);
            }
            int energyBefore = state.Energy.Current;
            bool ok = state.TryGenerate(out string error);
            Assert.IsFalse(ok);
            StringAssert.Contains("棋盘已满", error);
            Assert.AreEqual(energyBefore, state.Energy.Current);
        }

        private static void ClearBoard(GameState state)
        {
            for (int i = 0; i < BoardModel.CellCount; i++)
                state.Board.SetItemAt(i, ItemCatalog.EmptyId);
        }

        [Test]
        public void Energy_InitialValues_MatchSpec()
        {
            var state = GameState.CreateNew(seed: 1u);
            Assert.AreEqual(30, state.Energy.Current);
            Assert.AreEqual(30, EnergyModel.MaxEnergy);
            Assert.AreEqual(1, EnergyModel.GenerateCost);
        }

        [Test]
        public void Energy_RegeneratesOnePointPerThirtySeconds()
        {
            var now = DateTimeOffset.UtcNow;
            var energy = new EnergyModel(10, now);
            Assert.AreEqual(0, energy.Regenerate(now.AddSeconds(29.9)));
            Assert.AreEqual(1, energy.Regenerate(now.AddSeconds(30.0)));
            Assert.AreEqual(11, energy.Current);

            energy = new EnergyModel(10, now);
            Assert.AreEqual(3, energy.Regenerate(now.AddSeconds(95))); // 3 个整周期
            Assert.AreEqual(13, energy.Current);
        }

        [Test]
        public void Energy_OfflineRegen_CappedAtMax()
        {
            var now = DateTimeOffset.UtcNow;
            var energy = new EnergyModel(28, now);
            int gained = energy.Regenerate(now.AddHours(2)); // 离线 2 小时
            Assert.AreEqual(2, gained);
            Assert.AreEqual(EnergyModel.MaxEnergy, energy.Current);
        }

        [Test]
        public void Energy_AtCap_ResetsBaseline_PreventingDoubleClaim()
        {
            var now = DateTimeOffset.UtcNow;
            var energy = new EnergyModel(EnergyModel.MaxEnergy, now);
            // 满能量状态下时间流逝不应积累离线能量。
            energy.Regenerate(now.AddHours(5));
            Assert.AreEqual(EnergyModel.MaxEnergy, energy.Current);

            // 消耗 1 点后再立即结算，不应立刻补满（基准已重置）。
            Assert.IsTrue(energy.TrySpend(1, now.AddHours(5)));
            Assert.AreEqual(0, energy.Regenerate(now.AddHours(5).AddSeconds(1)));
            Assert.AreEqual(EnergyModel.MaxEnergy - 1, energy.Current);
        }

        [Test]
        public void Energy_SpendFromFull_StartsRegenFromSpendMoment()
        {
            var now = DateTimeOffset.UtcNow;
            var energy = new EnergyModel(EnergyModel.MaxEnergy, now.AddMinutes(-10));
            Assert.IsTrue(energy.TrySpend(1, now));
            Assert.AreEqual(0, energy.Regenerate(now.AddSeconds(29)));
            Assert.AreEqual(1, energy.Regenerate(now.AddSeconds(30)));
        }
    }
}
