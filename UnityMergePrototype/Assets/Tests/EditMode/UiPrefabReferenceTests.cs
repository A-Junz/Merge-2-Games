using MergePrototype.Presentation;
using MergePrototype.Core;
using MergePrototype.Persistence;
using MergePrototype.EditorTools;
using NUnit.Framework;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MergePrototype.Tests.EditMode
{
    public class UiPrefabReferenceTests
    {
        [Test]
        public void LoadsEditableUiPrefabsWithRequiredReferences()
        {
            var root = Resources.Load<GameUiView>("UI/Prefabs/GameUi");

            Assert.IsNotNull(root);
            Assert.IsNotNull(root.OrderCardPrefab);
            Assert.IsNotNull(root.BoardCellPrefab);
            Assert.IsNotNull(root.LevelLabel);
            Assert.IsNotNull(root.LevelFill);
            Assert.IsNotNull(root.OrderContent);
            Assert.IsNotNull(root.BoardGrid);
            Assert.IsNotNull(root.EncyclopediaButton);
            Assert.IsNotNull(root.AutoCompleteButton);
            Assert.IsNotNull(root.DeleteModal);
            Assert.Greater(root.AutoCompleteButton.GetComponent<RectTransform>().anchoredPosition.y,
                root.RestartButton.GetComponent<RectTransform>().anchoredPosition.y);
            Assert.IsNotNull(root.EncyclopediaModal);
            Assert.IsNotNull(root.LevelUpRewardModal);
            Assert.AreEqual(0f, root.OrderContent.anchorMin.x);
            Assert.AreEqual(0f, root.OrderContent.anchorMax.x);
            Assert.AreEqual(0f, root.OrderContent.pivot.x);
        }

        [Test]
        public void GameUiPrefabUsesWidthDrivenScalingAndSafeAreaLayout()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            Assert.IsNotNull(prefab);
            var scaler = prefab.GetComponent<CanvasScaler>();
            Assert.IsNotNull(scaler);
            Assert.AreEqual(UiLayoutSpec.CanvasMatchWidthOrHeight,
                scaler.matchWidthOrHeight, 0.001f);
            Assert.IsNotNull(prefab.GetComponent<SafeAreaLayout>());
            Assert.AreEqual(0f, prefab.StatusText.rectTransform.anchorMin.y);
            Assert.AreEqual(0f, prefab.StatusText.rectTransform.anchorMax.y);
        }

        [Test]
        public void LevelUpRewardModalExposesClaimButtonsAndDynamicRewardLabels()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            Assert.IsNotNull(prefab);
            var modal = prefab.LevelUpRewardModal.transform;
            Assert.IsNotNull(modal.Find("Card/TitleRibbon"));
            Assert.IsNotNull(modal.Find("Card/RewardArea/CashIcon"));
            Assert.IsNotNull(modal.Find("Card/ClaimAllButton"));
            Assert.IsNotNull(modal.Find("Card/ClaimAllButton/VideoIcon"));
            Assert.AreEqual("Claim All", modal.Find("Card/ClaimAllButton/Label")
                .GetComponent<Text>().text);
            Assert.IsNotNull(modal.Find("Card/OnlyClaimButton"));
            Assert.AreEqual("Only claim +0", modal.Find("Card/OnlyClaimButton/Label")
                .GetComponent<Text>().text);
        }

        [Test]
        public void LevelUpRewardModalUsesImportedSpritesForVisualLayers()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            Assert.IsNotNull(prefab);
            var modal = prefab.LevelUpRewardModal.transform;
            Assert.IsNotNull(modal.Find("Card").GetComponent<Image>().sprite);
            Assert.IsNotNull(modal.Find("Card/TitleRibbon").GetComponent<Image>().sprite);
            Assert.IsNotNull(modal.Find("Card/ChefHat").GetComponent<Image>().sprite);
            Assert.IsNotNull(modal.Find("Card/RewardArea").GetComponent<Image>().sprite);
            Assert.IsNotNull(modal.Find("Card/RewardArea/CashIcon").GetComponent<Image>().sprite);
            Assert.IsNotNull(modal.Find("Card/ClaimAllButton").GetComponent<Image>().sprite);
            Assert.IsNotNull(modal.Find("Card/ClaimAllButton/VideoIcon").GetComponent<Image>().sprite);
            Assert.IsNotNull(modal.Find("Card/OnlyClaimButton").GetComponent<Image>().sprite);
            Assert.IsNotNull(modal.Find("Card/OnlyClaimButton/SkipIcon").GetComponent<Image>().sprite);
        }

        [Test]
        public void EncyclopediaPrefabExposesEditableChainSections()
        {
            var host = new GameObject("EncyclopediaPrefabContract");
            try
            {
                var method = typeof(UiPrefabBuilder).GetMethod("BuildModal",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var modal = (GameObject)method.Invoke(null,
                    new object[] { host.transform, "EncyclopediaModal", "FOOD COLLECTION" });
                var root = modal.transform;
                Assert.IsNotNull(root.Find("Card/Header/Book"));
                Assert.IsNotNull(root.Find("Card/Header/Title"));
                Assert.IsNotNull(root.Find("Card/Header/Close"));
                Assert.IsNotNull(root.Find("Card/Summary/ProgressTrack/Fill"));
                Assert.IsNotNull(root.Find("Card/Summary/ProgressText"));
                Assert.IsNotNull(root.Find("Card/Summary/RewardText"));
                Assert.IsNotNull(root.Find("Card/Summary").GetComponent<Button>());
                Assert.IsNotNull(root.Find("Card/ChainScroll/Viewport/Content/Chain_bread/Grid/Catalog_101"));
                Assert.IsNotNull(root.Find("Card/ChainScroll/Viewport/Content/Chain_drink/Grid/Catalog_808"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EncyclopediaRefreshShowsQuestionMarkForUnseenElements()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            Assert.IsNotNull(prefab);
            var root = Object.Instantiate(prefab);
            var savePath = Path.Combine(Path.GetTempPath(), "merge-encyclopedia-visibility.json");
            try
            {
                var state = GameState.CreateNew(seed: 31u);
                state.RestoreSeenItems(new[] { 101 });
                var presenter = root.gameObject.AddComponent<GamePresenter>();
                presenter.Initialize(state, new SaveService(savePath), null);

                var newLayout = root.EncyclopediaModal.transform.Find("Card/ChainScroll/Viewport/Content");
                string prefix = newLayout == null
                    ? "Card/IconGrid"
                    : "Card/ChainScroll/Viewport/Content/Chain_bread/Grid";
                var seen = root.EncyclopediaModal.transform.Find($"{prefix}/Catalog_101");
                var unseen = root.EncyclopediaModal.transform.Find($"{prefix}/Catalog_108");
                Assert.IsTrue(seen.Find("Icon").GetComponent<Image>().gameObject.activeSelf);
                Assert.AreNotEqual("?", seen.Find("Name").GetComponent<Text>().text);
                Assert.IsFalse(unseen.Find("Icon").GetComponent<Image>().gameObject.activeSelf);
                Assert.AreEqual("?", unseen.Find("Name").GetComponent<Text>().text);
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void EncyclopediaPrefabUsesGeneratedFoodThemeSprites()
        {
            var host = new GameObject("EncyclopediaSpriteContract");
            try
            {
                var method = typeof(UiPrefabBuilder).GetMethod("BuildModal",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var modal = (GameObject)method.Invoke(null,
                    new object[] { host.transform, "EncyclopediaModal", "FOOD COLLECTION" });
                var root = modal.transform;

                Assert.AreEqual("encyclopedia_modal_body", root.Find("Card")
                    .GetComponent<Image>().sprite.name);
                Assert.AreEqual("encyclopedia_header_wood_green", root.Find("Card/Header")
                    .GetComponent<Image>().sprite.name);
                Assert.AreEqual("encyclopedia_close_button", root.Find("Card/Header/Close")
                    .GetComponent<Image>().sprite.name);
                Assert.AreEqual("encyclopedia_closed_recipe_book", root.Find("Card/Header/Book")
                    .GetComponent<Image>().sprite.name);
                Assert.AreEqual("encyclopedia_chain_card", root.Find("Card/ChainScroll/Viewport/Content/Chain_bread")
                    .GetComponent<Image>().sprite.name);
                Assert.AreEqual("encyclopedia_cash_reward", root.Find("Card/Summary/RewardIcon")
                    .GetComponent<Image>().sprite.name);

                var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
                Assert.IsNotNull(prefab);
                Assert.IsNotNull(prefab.EncyclopediaModal.transform.Find("Card")
                    .GetComponent<Image>().sprite);
                Assert.IsNotNull(prefab.EncyclopediaModal.transform.Find("Card/Header")
                    .GetComponent<Image>().sprite);
                Assert.IsNotNull(prefab.EncyclopediaModal.transform.Find("Card/ChainScroll/Viewport/Content/Chain_bread")
                    .GetComponent<Image>().sprite);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EncyclopediaLayoutAllocatesVisibleChainContent()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            Assert.IsNotNull(prefab);
            var root = Object.Instantiate(prefab);
            var savePath = Path.Combine(Path.GetTempPath(), "merge-encyclopedia-layout.json");
            try
            {
                var presenter = root.gameObject.AddComponent<GamePresenter>();
                presenter.Initialize(GameState.CreateNew(), new SaveService(savePath), null);
                presenter.NotifyToggleEncyclopedia();

                var scroll = root.EncyclopediaModal.transform.Find("Card/ChainScroll");
                var viewport = scroll.Find("Viewport") as RectTransform;
                var content = scroll.Find("Viewport/Content") as RectTransform;
                var firstChain = scroll.Find("Viewport/Content/Chain_bread") as RectTransform;

                Assert.Greater(viewport.rect.height, 0f);
                Assert.IsNotNull(viewport.GetComponent<RectMask2D>(),
                    "The viewport needs a RectMask2D so a transparent Image cannot zero the stencil mask");
                Assert.Greater(firstChain.rect.height, 0f);
                Assert.Greater(content.rect.height, viewport.rect.height,
                    "The chain content must be taller than the viewport so the list can scroll");
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void EncyclopediaSummaryUsesCompactProgressAndRewardLayout()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            Assert.IsNotNull(prefab);
            var root = Object.Instantiate(prefab);
            var savePath = Path.Combine(Path.GetTempPath(), "merge-encyclopedia-summary.json");
            try
            {
                var presenter = root.gameObject.AddComponent<GamePresenter>();
                presenter.Initialize(GameState.CreateNew(), new SaveService(savePath), null);
                var summary = root.EncyclopediaModal.transform.Find("Card/Summary");
                Assert.IsNotNull(summary.Find("SummaryBook"));
                Assert.AreEqual("0/80", summary.Find("ProgressText").GetComponent<Text>().text);
                Assert.AreEqual("+100", summary.Find("RewardText").GetComponent<Text>().text);
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void EncyclopediaSummaryAllCompletionRewardIsClaimableAtFullProgress()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            Assert.IsNotNull(prefab);
            var root = Object.Instantiate(prefab);
            var savePath = Path.Combine(Path.GetTempPath(), "merge-encyclopedia-all-reward.json");
            try
            {
                var state = GameState.CreateNew(seed: 43u);
                var allItems = new System.Collections.Generic.List<int>();
                for (int chain = 1; chain <= ItemCatalog.ChainCount; chain++)
                {
                    for (int level = 1; level <= ItemCatalog.MaxLevel; level++)
                        allItems.Add(chain * 100 + level);
                }
                state.RestoreSeenItems(allItems);

                var presenter = root.gameObject.AddComponent<GamePresenter>();
                presenter.Initialize(state, new SaveService(savePath), null);

                var button = root.EncyclopediaModal.transform.Find("Card/Summary")
                    .GetComponent<Button>();
                Assert.IsTrue(button.interactable);
                int coinsBefore = state.Coins;
                button.onClick.Invoke();
                Assert.AreEqual(coinsBefore + EncyclopediaRewardRules.AllChainsCompletionReward, state.Coins);
                Assert.IsFalse(button.interactable);
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void EncyclopediaChainRewardExposesClaimableAndClaimedLayers()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            Assert.IsNotNull(prefab);
            foreach (string chainId in new[] { "pizza", "drink" })
            {
                var reward = prefab.EncyclopediaModal.transform.Find(
                    $"Card/ChainScroll/Viewport/Content/Chain_{chainId}/RewardButton");
                Assert.IsNotNull(reward);
                Assert.IsNotNull(reward.Find("RewardIcon"));
                Assert.IsNotNull(reward.Find("RewardText"));
                Assert.IsNotNull(reward.Find("ClaimedIcon"));
                Assert.IsNotNull(reward.GetComponent<Button>());
            }
        }

        [Test]
        public void EncyclopediaRefreshShowsMoneyForClaimableAndCheckForClaimed()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            Assert.IsNotNull(prefab);
            var root = Object.Instantiate(prefab);
            var savePath = Path.Combine(Path.GetTempPath(), "merge-encyclopedia-reward-states.json");
            try
            {
                var seen = new System.Collections.Generic.List<int>();
                for (int level = 1; level <= ItemCatalog.MaxLevel; level++)
                {
                    seen.Add(200 + level);
                    seen.Add(800 + level);
                }
                var state = GameState.CreateNew(seed: 41u);
                state.RestoreSeenItems(seen);
                Assert.IsTrue(state.TryClaimEncyclopediaChainReward("drink", out _, out string claimError), claimError);

                var presenter = root.gameObject.AddComponent<GamePresenter>();
                presenter.Initialize(state, new SaveService(savePath), null);

                var pizzaReward = root.EncyclopediaModal.transform.Find(
                    "Card/ChainScroll/Viewport/Content/Chain_pizza/RewardButton");
                Assert.IsTrue(pizzaReward.GetComponent<Button>().interactable);
                Assert.IsTrue(pizzaReward.Find("RewardIcon").gameObject.activeSelf);
                Assert.AreEqual("+10", pizzaReward.Find("RewardText").GetComponent<Text>().text);
                Assert.IsFalse(pizzaReward.Find("ClaimedIcon").gameObject.activeSelf);

                var drinkReward = root.EncyclopediaModal.transform.Find(
                    "Card/ChainScroll/Viewport/Content/Chain_drink/RewardButton");
                Assert.IsFalse(drinkReward.GetComponent<Button>().interactable);
                Assert.IsFalse(drinkReward.Find("RewardIcon").gameObject.activeSelf);
                Assert.IsFalse(drinkReward.Find("RewardText").gameObject.activeSelf);
                Assert.IsTrue(drinkReward.Find("ClaimedIcon").gameObject.activeSelf);
                Assert.AreEqual("✓", drinkReward.Find("ClaimedIcon").GetComponent<Text>().text);
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void Presenter_BindsAutoCompleteButtonToCoreAutomation()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            var root = Object.Instantiate(prefab);
            var savePath = Path.Combine(Path.GetTempPath(), "merge-auto-complete-button.json");
            try
            {
                var state = GameState.CreateNew(seed: 51u);
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

                var presenter = root.gameObject.AddComponent<GamePresenter>();
                presenter.Initialize(state, new SaveService(savePath), null);
                presenter.AutoCompleteButton.onClick.Invoke();

                Assert.AreEqual(5, state.Coins);
                StringAssert.StartsWith("AUTO:", root.StatusText.text);
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void ReusablePrefabsExposeSerializedViewReferences()
        {
            var order = Resources.Load<OrderCardView>("UI/Prefabs/OrderCard");
            var cell = Resources.Load<BoardCellView>("UI/Prefabs/BoardCell");

            Assert.IsNotNull(order);
            Assert.IsNotNull(cell);
            Assert.IsNotNull(order.SubmitButton);
            Assert.AreEqual(3, order.ItemIcons.Count);
            Assert.IsNotNull(cell.Background);
            Assert.IsNotNull(cell.IconImage);
            Assert.IsNotNull(cell.transform.Find("LuckyMarker"));
            Assert.IsNotNull(cell.SemiLockOverlay);
            Assert.IsNotNull(cell.FullLockOverlay);
            Assert.IsTrue(cell.Background.raycastTarget,
                "BoardCell background must receive UI pointer events for drag/long-press input");
        }

        [Test]
        public void GameUiMoneyDisplayUsesCurrencyIconAndAmountText()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.transform.Find("TopStatus/MoneyPill/MoneyIcon/Icon"));
            Assert.AreEqual("0", prefab.MoneyText.text);
        }

        [Test]
        public void OrderCardReferenceStyleHasHeroRewardAndClaimLayers()
        {
            var order = Resources.Load<OrderCardView>("UI/Prefabs/OrderCard");
            Assert.IsNotNull(order);
            Assert.IsNotNull(order.transform.Find("HeroPanel"));
            Assert.IsNotNull(order.transform.Find("RewardBar"));
            Assert.IsNotNull(order.transform.Find("RewardBar/RewardXpIcon"));
            Assert.IsNotNull(order.transform.Find("RewardBar/RewardCoinIcon"));
            Assert.IsNotNull(order.transform.Find("RewardBar/RewardXpText"));
            Assert.IsNotNull(order.transform.Find("RewardBar/RewardCoinText"));
            Assert.IsNotNull(order.transform.Find("RewardedVideoButton"));
            Assert.IsNotNull(order.transform.Find("SubmitButton"));
            Assert.Greater(order.transform.Find("RewardedVideoButton/Label")
                .GetComponent<Text>().fontSize, 0);
        }

        [Test]
        public void OrderCardClaimLabelsAndRewardIconsMatchReferenceStyle()
        {
            var prefab = Resources.Load<OrderCardView>("UI/Prefabs/OrderCard");
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.transform.Find("RewardBar/RewardXpIcon/Icon"));
            Assert.IsNotNull(prefab.transform.Find("RewardBar/RewardCoinIcon/Icon"));

            var host = Object.Instantiate(prefab);
            try
            {
                host.Bind(null, 0);
                host.Render(new Sprite[1], new[] { true }, "进度 1/1", "XP +1    Coins +32",
                    true, null, 32, 3);

                string allLabel = host.transform.Find("RewardedVideoButton/Label")
                    .GetComponent<Text>().text;
                string directLabel = host.transform.Find("SubmitButton/Label")
                    .GetComponent<Text>().text;
                StringAssert.Contains("CLAIM ALL", allLabel);
                StringAssert.DoesNotContain("+32", allLabel);
                StringAssert.Contains("ONLY CLAIM", directLabel);
                StringAssert.Contains("+3", directLabel);
            }
            finally
            {
                Object.DestroyImmediate(host.gameObject);
            }
        }

        [Test]
        public void NonRewardGatedOrderHidesVideoButtonAndShowsFullClaim()
        {
            var prefab = Resources.Load<OrderCardView>("UI/Prefabs/OrderCard");
            Assert.IsNotNull(prefab);

            var host = Object.Instantiate(prefab);
            try
            {
                host.Bind(null, 0);
                host.Render(new Sprite[1], new[] { true }, "进度 1/1", "XP +1    Coins +32",
                    true, null, 32, 3, false);

                Assert.IsFalse(host.RewardedVideoButton.gameObject.activeSelf);
                string directLabel = host.transform.Find("SubmitButton/Label")
                    .GetComponent<Text>().text;
                StringAssert.Contains("CLAIM ALL", directLabel);
                StringAssert.Contains("+32", directLabel);
            }
            finally
            {
                Object.DestroyImmediate(host.gameObject);
            }
        }

        [Test]
        public void PresenterInstantiatesOnePrefabPerModelSlot()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            Assert.IsNotNull(prefab);
            var root = Object.Instantiate(prefab);
            var savePath = Path.Combine(Path.GetTempPath(), "merge-ui-prefab-contract.json");
            try
            {
                var presenter = root.gameObject.AddComponent<GamePresenter>();
                presenter.Initialize(GameState.CreateNew(), new SaveService(savePath), null);

                Assert.AreEqual(OrderModel.SlotCount,
                    root.OrderContent.GetComponentsInChildren<OrderCardView>(true).Length);
                Assert.AreEqual(BoardModel.CellCount,
                    root.BoardGrid.GetComponentsInChildren<BoardCellView>(true).Length);
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void OrderSatisfiedMarker_OnlyShowsOnFullyUnlockedMatchingCells()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            Assert.IsNotNull(prefab);
            var root = Object.Instantiate(prefab);
            var savePath = Path.Combine(Path.GetTempPath(), "merge-order-marker-contract.json");
            try
            {
                var state = GameState.CreateNew(seed: 23u);
                state.Board.SetItemAt(InitialBoardLayout.CenterCellIndex, 102);
                var presenter = root.gameObject.AddComponent<GamePresenter>();
                presenter.Initialize(state, new SaveService(savePath), null);

                Assert.IsFalse(presenter.Cells[24].transform.Find("LuckyMarker").gameObject.activeSelf,
                    "半解锁格中的订单需求物不应显示对钩");
                Assert.IsFalse(presenter.Cells[38].transform.Find("LuckyMarker").gameObject.activeSelf,
                    "半解锁格中的订单需求物不应显示对钩");
                Assert.IsTrue(presenter.Cells[InitialBoardLayout.CenterCellIndex]
                    .transform.Find("LuckyMarker").gameObject.activeSelf,
                    "完全解锁格中的订单需求物应显示对钩");
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void BoardCellPrefabUsesIconFirstPresentation()
        {
            var prefab = Resources.Load<BoardCellView>("UI/Prefabs/BoardCell");
            Assert.IsNotNull(prefab);
            var cell = Object.Instantiate(prefab);
            try
            {
                cell.Bind(null, 0);
                cell.Render(101, "🍓", null, "Strawberry", Color.white);
                cell.SetUnlockState(CellUnlockState.SemiUnlocked);
                Assert.IsTrue(cell.SemiLockOverlay.activeSelf);
                Assert.IsFalse(cell.FullLockOverlay.activeSelf);
                cell.SetUnlockState(CellUnlockState.Locked);
                Assert.IsFalse(cell.SemiLockOverlay.activeSelf);
                Assert.IsTrue(cell.FullLockOverlay.activeSelf);

                Assert.IsFalse(cell.transform.Find("NameLabel").gameObject.activeSelf);
                cell.SetOrderSatisfiedMarker(true);
                Assert.IsTrue(cell.transform.Find("LuckyMarker").gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(cell.gameObject);
            }
        }

        [Test]
        public void BoardCellPrefabShowsSpecialCashAmount()
        {
            var prefab = Resources.Load<BoardCellView>("UI/Prefabs/BoardCell");
            Assert.IsNotNull(prefab);
            var cell = Object.Instantiate(prefab);
            try
            {
                cell.Bind(null, 0);
                var currencyChip = Resources.Load<Sprite>("UI/ui_reward_cash_chip");
                cell.Render(1101, string.Empty, currencyChip, "5", Color.white, true);

                var label = cell.transform.Find("NameLabel").GetComponent<UnityEngine.UI.Text>();
                Assert.IsTrue(label.gameObject.activeSelf);
                Assert.AreEqual("5", label.text);
                Assert.AreEqual(UnityEngine.UI.Image.Type.Filled, cell.IconImage.type);
                Assert.Less(cell.IconImage.fillAmount, 1f);
            }
            finally
            {
                Object.DestroyImmediate(cell.gameObject);
            }
        }

        [Test]
        public void BoardCellPrefabShowsCrownForMaxSpecialLevelOnly()
        {
            var prefab = Resources.Load<BoardCellView>("UI/Prefabs/BoardCell");
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.transform.Find("CrownMarker"));

            var cell = Object.Instantiate(prefab);
            try
            {
                cell.Bind(null, 0);
                var currencyChip = Resources.Load<Sprite>("UI/ui_reward_cash_chip");
                cell.Render(ItemCatalog.SpecialLevelFiveId, string.Empty, currencyChip,
                    "50", Color.white, true);
                Assert.IsTrue(cell.transform.Find("CrownMarker").gameObject.activeSelf);

                cell.Render(ItemCatalog.SpecialLevelOneId + 3, string.Empty, currencyChip,
                    "30", Color.white, true);
                Assert.IsFalse(cell.transform.Find("CrownMarker").gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(cell.gameObject);
            }
        }

        [Test]
        public void BoardCell_TwoSingleTapPointerClicks_CashOutSpecialItem()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            Assert.IsNotNull(prefab);
            var root = Object.Instantiate(prefab);
            var savePath = Path.Combine(Path.GetTempPath(), "merge-special-double-tap.json");
            try
            {
                var state = GameState.CreateNew(seed: 41u);
                int cellIndex = InitialBoardLayout.CenterCellIndex;
                state.Board.SetItemAt(cellIndex, 1103);
                var presenter = root.gameObject.AddComponent<GamePresenter>();
                presenter.Initialize(state, new SaveService(savePath), null);

                var cell = presenter.Cells[cellIndex];
                cell.OnPointerClick(new PointerEventData(null) { clickCount = 1 });
                cell.OnPointerClick(new PointerEventData(null) { clickCount = 1 });

                Assert.AreEqual(12, state.Coins);
                Assert.AreEqual(ItemCatalog.EmptyId, state.Board.GetItemAt(cellIndex));
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void GameUiPrefabHidesLegacyRestartControl()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            Assert.IsNotNull(prefab);

            var legacyRestart = prefab.transform.Find("LegacyRestartButton");
            Assert.IsNotNull(legacyRestart);
            Assert.IsFalse(legacyRestart.gameObject.activeSelf,
                "The restart control is intentionally removed from the level progress UI");
        }
    }
}
