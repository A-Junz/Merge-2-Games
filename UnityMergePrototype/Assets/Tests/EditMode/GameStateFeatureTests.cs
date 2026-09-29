using MergePrototype.Core;
using NUnit.Framework;

namespace MergePrototype.Tests.EditMode
{
    public class GameStateFeatureTests
    {
        [Test]
        public void DeleteItem_RemovesOnlySelectedOccupiedCell()
        {
            var state = GameState.CreateNew(123u);
            state.Board.SetItemAt(0, 101);
            state.Board.SetItemAt(1, 102);
            bool changed = false;
            state.StateChanged += () => changed = true;

            Assert.IsTrue(state.TryDeleteItem(0, out string error), error);
            Assert.AreEqual(ItemCatalog.EmptyId, state.Board.GetItemAt(0));
            Assert.AreEqual(102, state.Board.GetItemAt(1));
            Assert.IsTrue(changed);
        }

        [Test]
        public void Generate_RecordsLuckyCellsOnlyForRareLevelThreeAndFour()
        {
            var state = GameState.CreateNew(1u);
            state.Board.Clear();
            Assert.IsTrue(state.TryGenerate(out string error), error);
            Assert.Greater(state.LastLuckyGeneratedCellIndices.Count, 0);
            foreach (int cellIndex in state.LastLuckyGeneratedCellIndices)
            {
                int itemId = state.Board.GetItemAt(cellIndex);
                int level = state.Catalog.Get(itemId).Level;
                Assert.IsTrue(level == 3 || level == 4, $"幸运格等级错误: L{level}");
            }
        }

        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, true)]
        [TestCase(4, true)]
        [TestCase(5, false)]
        public void LuckyMarkerPolicy_OnlyMarksRareLevelThreeAndFour(int level, bool expected)
        {
            Assert.AreEqual(expected, LuckyMarkerPolicy.IsLuckyLevel(level));
        }

        [Test]
        public void NewGame_UsesInitialBoardLayoutAndUnlockStates()
        {
            var state = GameState.CreateNew(seed: 11u);

            Assert.AreEqual(101, state.Board.GetItemAt(31));
            Assert.AreEqual(CellUnlockState.Unlocked, state.Board.GetUnlockState(31));
            Assert.AreEqual(CellUnlockState.SemiUnlocked, state.Board.GetUnlockState(24));
            Assert.AreEqual(CellUnlockState.SemiUnlocked, state.Board.GetUnlockState(30));
            Assert.AreEqual(CellUnlockState.SemiUnlocked, state.Board.GetUnlockState(32));
            Assert.AreEqual(CellUnlockState.SemiUnlocked, state.Board.GetUnlockState(38));
            Assert.AreEqual(CellUnlockState.Locked, state.Board.GetUnlockState(0));
        }
    }
}
