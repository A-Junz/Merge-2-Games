using System.Collections;
using System.IO;
using MergePrototype.Core;
using MergePrototype.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MergePrototype.Tests.PlayMode
{
    /// <summary>
    /// 拖拽交互端到端验证：通过 EventSystem 真实派发
    /// BeginDrag/Drag/EndDrag 指针事件，覆盖移动、交换、合成与拖出棋盘还原。
    /// </summary>
    public class DragInteractionTests
    {
        private static string SavePath
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(projectRoot, "Temp", "merge-drag-test-save.json");
            }
        }

        private GamePresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            GameBootstrap.SavePathOverride = SavePath;
            try { if (File.Exists(SavePath)) File.Delete(SavePath); } catch { /* 忽略 */ }
        }

        [TearDown]
        public void TearDown()
        {
            GameBootstrap.SavePathOverride = null;
            try { if (File.Exists(SavePath)) File.Delete(SavePath); } catch { /* 忽略 */ }
        }

        private IEnumerator LoadSceneWithCleanBoard()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;

            _presenter = Object.FindObjectOfType<GamePresenter>();
            Assert.IsNotNull(_presenter);
            _presenter.State.Board.Clear();
            for (int i = 0; i < BoardModel.CellCount; i++)
                _presenter.State.Board.SetUnlockState(i, CellUnlockState.Unlocked);
            _presenter.RefreshAll();
            Canvas.ForceUpdateCanvases();
            yield return null; // 等 GridLayoutGroup 完成布局
        }

        /// <summary>模拟一次完整拖拽：从 source 格拖到屏幕坐标 screenPos。</summary>
        private void SimulateDrag(int sourceIndex, Vector2 screenPos)
        {
            var cell = _presenter.Cells[sourceIndex];
            var data = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = cell.transform.position // Overlay 画布下世界坐标即屏幕坐标
            };
            ExecuteEvents.Execute(cell.gameObject, data, ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(cell.gameObject, data, ExecuteEvents.dragHandler);
            data.position = screenPos;
            ExecuteEvents.Execute(cell.gameObject, data, ExecuteEvents.endDragHandler);
        }

        [UnityTest]
        public IEnumerator Drag_OntoEmptyCell_MovesItem()
        {
            yield return LoadSceneWithCleanBoard();
            _presenter.State.Board.SetItemAt(0, 101);
            _presenter.RefreshAll();

            SimulateDrag(0, _presenter.Cells[10].transform.position);

            Assert.AreEqual(ItemCatalog.EmptyId, _presenter.State.Board.GetItemAt(0));
            Assert.AreEqual(101, _presenter.State.Board.GetItemAt(10));
        }

        [UnityTest]
        public IEnumerator Drag_OntoSameItem_Merges()
        {
            yield return LoadSceneWithCleanBoard();
            _presenter.State.Board.SetItemAt(0, 101);
            _presenter.State.Board.SetItemAt(1, 101);
            _presenter.RefreshAll();

            SimulateDrag(0, _presenter.Cells[1].transform.position);

            Assert.AreEqual(ItemCatalog.EmptyId, _presenter.State.Board.GetItemAt(0));
            Assert.AreEqual(102, _presenter.State.Board.GetItemAt(1), "同链同级应合成为下一级");
        }

        [UnityTest]
        public IEnumerator Drag_OntoDifferentItem_Swaps()
        {
            yield return LoadSceneWithCleanBoard();
            _presenter.State.Board.SetItemAt(0, 101);
            _presenter.State.Board.SetItemAt(1, 201);
            _presenter.RefreshAll();

            SimulateDrag(0, _presenter.Cells[1].transform.position);

            Assert.AreEqual(201, _presenter.State.Board.GetItemAt(0));
            Assert.AreEqual(101, _presenter.State.Board.GetItemAt(1));
        }

        [UnityTest]
        public IEnumerator Drag_OutsideBoard_RestoresWithoutChange()
        {
            yield return LoadSceneWithCleanBoard();
            _presenter.State.Board.SetItemAt(30, 202);
            _presenter.RefreshAll();

            // 拖到屏幕外的一个不可能命中任何格子的位置。
            SimulateDrag(30, new Vector2(-500f, -500f));

            Assert.AreEqual(202, _presenter.State.Board.GetItemAt(30), "拖到棋盘外应恢复原状");
            Assert.AreEqual(1, BoardModel.CellCount - _presenter.State.Board.GetEmptyIndices().Count);
        }

        [UnityTest]
        public IEnumerator Drag_FromEmptyCell_DoesNothing()
        {
            yield return LoadSceneWithCleanBoard();
            _presenter.State.Board.SetItemAt(5, 101);
            _presenter.RefreshAll();

            SimulateDrag(0, _presenter.Cells[5].transform.position); // 0 是空格

            Assert.AreEqual(101, _presenter.State.Board.GetItemAt(5), "空拖拽不应影响棋盘");
            Assert.AreEqual(ItemCatalog.EmptyId, _presenter.State.Board.GetItemAt(0));
        }

        [UnityTest]
        public IEnumerator Drag_FromSemiUnlockedCell_IsIgnored()
        {
            yield return LoadSceneWithCleanBoard();
            _presenter.State.Board.SetItemAt(0, 101);
            _presenter.State.Board.SetUnlockState(0, CellUnlockState.SemiUnlocked);
            _presenter.RefreshAll();

            SimulateDrag(0, _presenter.Cells[1].transform.position);

            Assert.AreEqual(101, _presenter.State.Board.GetItemAt(0));
            Assert.AreEqual(ItemCatalog.EmptyId, _presenter.State.Board.GetItemAt(1));
        }

        [UnityTest]
        public IEnumerator Drag_MaxLevelSameItem_RejectedAndUnchanged()
        {
            yield return LoadSceneWithCleanBoard();
            _presenter.State.Board.SetItemAt(0, 108);
            _presenter.State.Board.SetItemAt(1, 108);
            _presenter.RefreshAll();

            SimulateDrag(0, _presenter.Cells[1].transform.position);

            Assert.AreEqual(108, _presenter.State.Board.GetItemAt(0));
            Assert.AreEqual(108, _presenter.State.Board.GetItemAt(1), "最高级合成应被拒绝");
        }
    }
}
