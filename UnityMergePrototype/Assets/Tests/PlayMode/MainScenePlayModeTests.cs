using System.Collections;
using System.IO;
using MergePrototype.Core;
using MergePrototype.Persistence;
using MergePrototype.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MergePrototype.Tests.PlayMode
{
    /// <summary>
    /// 验证 Main 场景能启动并完成「点击发牌 → 填满空位 → 存档落盘」的闭环。
    /// </summary>
    public class MainScenePlayModeTests
    {
        /// <summary>测试存档放在工程目录内，避免污染真实 persistentDataPath。</summary>
        private static string SavePath
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(projectRoot, "Temp", "merge-playmode-test-save.json");
            }
        }

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

        [UnityTest]
        public IEnumerator MainScene_Starts_WithoutExceptions()
        {
            LogAssert.ignoreFailingMessages = false;
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null; // 等待 Awake/Start

            var presenter = Object.FindObjectOfType<GamePresenter>();
            Assert.IsNotNull(presenter, "场景中应存在 GamePresenter");
            Assert.IsNotNull(presenter.State, "GameState 应已初始化");
        }

        [UnityTest]
        public IEnumerator MainScene_Builds_63Cells_5OrderCards_1Generator()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;

            var presenter = Object.FindObjectOfType<GamePresenter>();
            Assert.IsNotNull(presenter);
            Assert.AreEqual(BoardModel.CellCount, presenter.Cells.Count, "应有 63 个棋盘格");
            Assert.AreEqual(OrderModel.SlotCount, presenter.OrderCards.Count, "应有 5 张订单卡");
            Assert.IsNotNull(presenter.GeneratorButton, "应有 1 个生成器");
            Assert.AreEqual(1, Object.FindObjectsOfType<GamePresenter>().Length);
        }

        [UnityTest]
        public IEnumerator MainScene_FirstOrderCard_UsesConfiguredLayoutSize()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;

            var presenter = Object.FindObjectOfType<GamePresenter>();
            Assert.IsNotNull(presenter);
            var cardRect = presenter.OrderCards[0].GetComponent<RectTransform>();

            Assert.AreEqual(UiLayoutSpec.OrderCardWidth, cardRect.rect.width, 0.1f,
                "首个订单卡片不能保留预制体默认宽度");
            Assert.AreEqual(UiLayoutSpec.OrderCardHeight, cardRect.rect.height, 0.1f,
                "首个订单卡片不能保留预制体默认高度");
        }

        [UnityTest]
        public IEnumerator ClickGenerator_FillsBoardWithoutEnergyLimit_AndWritesSave()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;

            var presenter = Object.FindObjectOfType<GamePresenter>();
            Assert.IsNotNull(presenter);
            int energyBefore = presenter.State.Energy.Current;
            int emptyBefore = presenter.State.Board.GetEmptyIndices().Count;

            presenter.GeneratorButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(energyBefore, presenter.State.Energy.Current, "发牌不应受能量限制");
            Assert.AreEqual(0, presenter.State.Board.GetEmptyIndices().Count, "一次点击应按行优先填满所有空位");
            Assert.Greater(emptyBefore, 0);
            Assert.IsTrue(File.Exists(SavePath), "存档文件应已写入磁盘");
        }

        [UnityTest]
        public IEnumerator PlayModeExit_Reenter_RestoresState()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;

            var first = Object.FindObjectOfType<GamePresenter>();
            first.GeneratorButton.onClick.Invoke();
            yield return null;
            int coinsBefore = first.State.Coins;
            int itemCount = BoardModel.CellCount - first.State.Board.GetEmptyIndices().Count;
            Assert.GreaterOrEqual(itemCount, 1);

            // 模拟退出并重进：重新加载场景，状态应来自存档。
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;

            var second = Object.FindObjectOfType<GamePresenter>();
            Assert.AreEqual(coinsBefore, second.State.Coins, "金币应从存档恢复");
            Assert.AreEqual(itemCount,
                BoardModel.CellCount - second.State.Board.GetEmptyIndices().Count,
                "棋盘物品数应从存档恢复");
        }
    }
}
