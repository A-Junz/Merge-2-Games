using MergePrototype.Core;
using NUnit.Framework;

namespace MergePrototype.Tests.EditMode
{
    public class BoardModelTests
    {
        private ItemCatalog _catalog;
        private BoardModel _board;

        [SetUp]
        public void SetUp()
        {
            _catalog = new ItemCatalog();
            _board = new BoardModel(_catalog);
        }

        [Test]
        public void Board_Has63Cells_7Columns9Rows()
        {
            Assert.AreEqual(63, BoardModel.CellCount);
            Assert.AreEqual(7, BoardModel.Columns);
            Assert.AreEqual(9, BoardModel.Rows);
        }

        [Test]
        public void Drop_ToEmptyCell_MovesItem()
        {
            _board.SetItemAt(0, 101);
            var result = _board.HandleDrop(0, 10);
            Assert.AreEqual(DropResultType.Moved, result.Type);
            Assert.AreEqual(ItemCatalog.EmptyId, _board.GetItemAt(0));
            Assert.AreEqual(101, _board.GetItemAt(10));
        }

        [Test]
        public void Drop_OntoDifferentItem_Swaps()
        {
            _board.SetItemAt(0, 101);
            _board.SetItemAt(1, 201);
            var result = _board.HandleDrop(0, 1);
            Assert.AreEqual(DropResultType.Swapped, result.Type);
            Assert.AreEqual(201, _board.GetItemAt(0));
            Assert.AreEqual(101, _board.GetItemAt(1));
        }

        [Test]
        public void Drop_SameChainSameLevel_MergesToNextLevel()
        {
            _board.SetItemAt(0, 101);
            _board.SetItemAt(1, 101);
            var result = _board.HandleDrop(0, 1);
            Assert.AreEqual(DropResultType.Merged, result.Type);
            Assert.AreEqual(ItemCatalog.EmptyId, _board.GetItemAt(0)); // 源格清空
            Assert.AreEqual(102, _board.GetItemAt(1));                 // 目标格升级
            StringAssert.Contains("合成成功", result.Message);
        }

        [Test]
        public void Drop_DifferentChains_SwapsInsteadOfMerge()
        {
            _board.SetItemAt(0, 101); // 面包坊 L1
            _board.SetItemAt(1, 201); // 披萨店 L1
            var result = _board.HandleDrop(0, 1);
            Assert.AreEqual(DropResultType.Swapped, result.Type);
            Assert.AreEqual(201, _board.GetItemAt(0));
            Assert.AreEqual(101, _board.GetItemAt(1));
        }

        [Test]
        public void Drop_SameChainDifferentLevel_SwapsInsteadOfMerge()
        {
            _board.SetItemAt(0, 101);
            _board.SetItemAt(1, 102);
            var result = _board.HandleDrop(0, 1);
            Assert.AreEqual(DropResultType.Swapped, result.Type);
        }

        [Test]
        public void Drop_MaxLevelSameItem_RejectedWithMessage()
        {
            _board.SetItemAt(0, 108); // 面包链 L8 最高级
            _board.SetItemAt(1, 108);
            var result = _board.HandleDrop(0, 1);
            Assert.AreEqual(DropResultType.Rejected, result.Type);
            StringAssert.Contains("最高级", result.Message);
            Assert.AreEqual(108, _board.GetItemAt(0)); // 棋盘不变
            Assert.AreEqual(108, _board.GetItemAt(1));
        }

        [Test]
        public void Drop_InvalidIndices_RejectedWithoutChangingBoard()
        {
            _board.SetItemAt(0, 101);
            Assert.AreEqual(DropResultType.Rejected, _board.HandleDrop(-1, 0).Type);
            Assert.AreEqual(DropResultType.Rejected, _board.HandleDrop(0, 63).Type);
            Assert.AreEqual(DropResultType.Rejected, _board.HandleDrop(999, 999).Type);
            Assert.AreEqual(101, _board.GetItemAt(0));
        }

        [Test]
        public void Drop_OntoSelf_RejectedWithoutChange()
        {
            _board.SetItemAt(5, 201);
            var result = _board.HandleDrop(5, 5);
            Assert.AreEqual(DropResultType.Rejected, result.Type);
            Assert.AreEqual(201, _board.GetItemAt(5));
        }

        [Test]
        public void Drop_FromEmptyCell_Rejected()
        {
            _board.SetItemAt(1, 101);
            var result = _board.HandleDrop(0, 1);
            Assert.AreEqual(DropResultType.Rejected, result.Type);
            Assert.AreEqual(101, _board.GetItemAt(1));
        }

        [Test]
        public void EmptyCellQuery_CountAndRemove_Work()
        {
            Assert.AreEqual(63, _board.GetEmptyIndices().Count);
            Assert.IsTrue(_board.HasEmptyCell());

            _board.SetItemAt(0, 101);
            _board.SetItemAt(5, 101);
            _board.SetItemAt(9, 201);
            Assert.AreEqual(2, _board.CountItem(101));
            Assert.AreEqual(2, _board.RemoveItems(101, 5)); // 只有 2 个可移除
            Assert.AreEqual(0, _board.CountItem(101));
            Assert.AreEqual(1, _board.CountItem(201));
            Assert.AreEqual(62, _board.GetEmptyIndices().Count); // 63 格 - 剩余 1 个 201
        }

        [Test]
        public void SnapshotAndRestore_RoundTrips()
        {
            _board.SetItemAt(3, 103);
            _board.SetItemAt(62, 205);
            var snapshot = _board.Snapshot();

            var other = new BoardModel(_catalog);
            other.Restore(snapshot);
            Assert.AreEqual(103, other.GetItemAt(3));
            Assert.AreEqual(205, other.GetItemAt(62));
        }

        [Test]
        public void GetNextId_MaxLevel_ReturnsEmpty()
        {
            Assert.AreEqual(102, _catalog.GetNextId(101));
            Assert.AreEqual(ItemCatalog.EmptyId, _catalog.GetNextId(108));
            Assert.AreEqual(ItemCatalog.EmptyId, _catalog.GetNextId(208));
        }

        [Test]
        public void LockedSource_IsRejectedWithoutChangingBoard()
        {
            _board.SetItemAt(0, 101);
            _board.SetItemAt(1, 101);
            _board.SetUnlockState(0, CellUnlockState.Locked);
            _board.SetUnlockState(1, CellUnlockState.Unlocked);

            var result = _board.HandleDrop(0, 1);

            Assert.AreEqual(DropResultType.Rejected, result.Type);
            Assert.AreEqual(101, _board.GetItemAt(0));
            Assert.AreEqual(101, _board.GetItemAt(1));
        }

        [Test]
        public void SemiUnlockedSource_IsRejectedWithoutChangingBoard()
        {
            _board.SetItemAt(0, 101);
            _board.SetItemAt(1, 101);
            _board.SetUnlockState(0, CellUnlockState.SemiUnlocked);
            _board.SetUnlockState(1, CellUnlockState.Unlocked);

            var result = _board.HandleDrop(0, 1);

            Assert.AreEqual(DropResultType.Rejected, result.Type);
            Assert.AreEqual(101, _board.GetItemAt(0));
            Assert.AreEqual(101, _board.GetItemAt(1));
        }

        [Test]
        public void SemiUnlockedTarget_DifferentItemRejectsMoveAndSwap()
        {
            _board.SetItemAt(0, 101);
            _board.SetItemAt(1, 201);
            _board.SetUnlockState(0, CellUnlockState.Unlocked);
            _board.SetUnlockState(1, CellUnlockState.SemiUnlocked);

            var result = _board.HandleDrop(0, 1);

            Assert.AreEqual(DropResultType.Rejected, result.Type);
            Assert.AreEqual(101, _board.GetItemAt(0));
            Assert.AreEqual(201, _board.GetItemAt(1));
        }

        [Test]
        public void SemiUnlockedTarget_AllowsMergeFromUnlockedSourceAndUnlocksTarget()
        {
            _board.SetItemAt(0, 101);
            _board.SetItemAt(1, 101);
            _board.SetUnlockState(0, CellUnlockState.Unlocked);
            _board.SetUnlockState(1, CellUnlockState.SemiUnlocked);

            var result = _board.HandleDrop(0, 1);

            Assert.AreEqual(DropResultType.Merged, result.Type);
            Assert.AreEqual(102, _board.GetItemAt(1));
            Assert.AreEqual(CellUnlockState.Unlocked, _board.GetUnlockState(1));
        }

        [Test]
        public void UnlockingCell_PromotesItsLockedNeighborsToSemiUnlocked()
        {
            int center = 31;
            for (int i = 0; i < BoardModel.CellCount; i++)
                _board.SetUnlockState(i, CellUnlockState.Locked);
            _board.SetItemAt(center, 101);
            _board.SetItemAt(center + 1, 101);
            _board.SetUnlockState(center, CellUnlockState.Unlocked);
            _board.SetUnlockState(center + 1, CellUnlockState.SemiUnlocked);

            Assert.AreEqual(DropResultType.Merged, _board.HandleDrop(center, center + 1).Type);

            Assert.AreEqual(CellUnlockState.SemiUnlocked, _board.GetUnlockState(center + 2));
            Assert.AreEqual(CellUnlockState.SemiUnlocked, _board.GetUnlockState(center + 1 - BoardModel.Columns));
            Assert.AreEqual(CellUnlockState.SemiUnlocked, _board.GetUnlockState(center + 1 + BoardModel.Columns));
        }
    }
}
