using MergePrototype.Core;
using NUnit.Framework;

namespace MergePrototype.Tests.EditMode
{
    public class InitialBoardLayoutTests
    {
        [Test]
        public void InitialLayout_MatchesNineBySevenSpec()
        {
            var catalog = new ItemCatalog();

            Assert.AreEqual(BoardModel.CellCount, InitialBoardLayout.ItemIds.Count);
            Assert.AreEqual(101, InitialBoardLayout.ItemIds[(5 - 1) * 7 + (4 - 1)]);
            foreach (int itemId in InitialBoardLayout.ItemIds)
                Assert.IsTrue(catalog.IsValidItem(itemId), $"非法初始物品: {itemId}");
        }

        [Test]
        public void InitialLayout_UnlocksCenterAndFourNeighborsOnly()
        {
            int center = InitialBoardLayout.CenterCellIndex;
            Assert.AreEqual(CellUnlockState.Unlocked, InitialBoardLayout.UnlockStates[center]);
            Assert.AreEqual(CellUnlockState.SemiUnlocked, InitialBoardLayout.UnlockStates[center - BoardModel.Columns]);
            Assert.AreEqual(CellUnlockState.SemiUnlocked, InitialBoardLayout.UnlockStates[center - 1]);
            Assert.AreEqual(CellUnlockState.SemiUnlocked, InitialBoardLayout.UnlockStates[center + 1]);
            Assert.AreEqual(CellUnlockState.SemiUnlocked, InitialBoardLayout.UnlockStates[center + BoardModel.Columns]);

            for (int i = 0; i < InitialBoardLayout.UnlockStates.Count; i++)
            {
                if (i == center || i == center - BoardModel.Columns || i == center - 1
                    || i == center + 1 || i == center + BoardModel.Columns)
                    continue;
                Assert.AreEqual(CellUnlockState.Locked, InitialBoardLayout.UnlockStates[i]);
            }
        }
    }
}
