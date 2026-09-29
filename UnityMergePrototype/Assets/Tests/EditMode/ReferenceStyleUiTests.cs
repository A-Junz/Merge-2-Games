using System.IO;
using MergePrototype.Core;
using MergePrototype.Persistence;
using MergePrototype.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MergePrototype.Tests.EditMode
{
    public class ReferenceStyleUiTests
    {
        [Test]
        public void ReferenceStyleUi_UsesBackdropAndAvatarLayers()
        {
            var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            Assert.IsNotNull(prefab);

            var backdrop = prefab.transform.Find("BackdropArtwork");
            Assert.IsNotNull(backdrop, "The reference-style background must be a dedicated artwork layer");
            Assert.AreEqual("ui_gameplay_backdrop", backdrop.GetComponent<Image>().sprite.name);

            var orderPrefab = Resources.Load<OrderCardView>("UI/Prefabs/OrderCard");
            Assert.IsNotNull(orderPrefab);
            Assert.IsNotNull(orderPrefab.transform.Find("AvatarImage"),
                "Order cards must render a real customer sprite instead of a letter placeholder");
            Assert.IsFalse(orderPrefab.transform.Find("AvatarImage/AvatarText").gameObject.activeSelf,
                "The legacy letter avatar must be hidden in the reference-style card");
        }

        [Test]
        public void Presenter_BindsThreeReferenceStyleCustomerAvatars()
        {
            var root = Object.Instantiate(Resources.Load<GameUiView>("UI/Prefabs/GameUi"));
            string savePath = Path.Combine(Path.GetTempPath(), "merge-reference-style-ui.json");
            try
            {
                var state = GameState.CreateNew(seed: 11u);
                state.Orders.RollAll(state.Rng);
                var presenter = root.gameObject.AddComponent<GamePresenter>();
                presenter.Initialize(state, new SaveService(savePath), null);

                Assert.GreaterOrEqual(presenter.OrderCards.Count, 3);
                for (int i = 0; i < 3; i++)
                {
                    Assert.IsNotNull(presenter.OrderCards[i].transform.Find("AvatarImage").GetComponent<Image>().sprite,
                        $"Reference order card {i} should receive a customer avatar sprite");
                }
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void Presenter_KeepsCustomerAvatarWithOrderWhenDisplaySortingChanges()
        {
            var root = Object.Instantiate(Resources.Load<GameUiView>("UI/Prefabs/GameUi"));
            string savePath = Path.Combine(Path.GetTempPath(), "merge-avatar-order-binding.json");
            try
            {
                var state = GameState.CreateNew(seed: 11u);
                state.Board.Clear();
                for (int i = 0; i < BoardModel.CellCount; i++)
                    state.Board.SetUnlockState(i, CellUnlockState.Unlocked);
                state.Board.SetItemAt(0, 102);
                state.Orders.SetOrder(0, new Order
                {
                    ItemId = 101,
                    RequiredItemIds = new System.Collections.Generic.List<int> { 101 },
                    CharacterId = 1
                });
                state.Orders.SetOrder(1, new Order
                {
                    ItemId = 102,
                    RequiredItemIds = new System.Collections.Generic.List<int> { 102 },
                    CharacterId = 2
                });

                var presenter = root.gameObject.AddComponent<GamePresenter>();
                presenter.Initialize(state, new SaveService(savePath), null);

                Assert.AreEqual("avatar_girl", presenter.OrderCards[0].AvatarImage.sprite.name,
                    "已满足订单排序到前面后，头像仍应来自该订单的 CharacterId");
                Assert.AreEqual("avatar_boy", presenter.OrderCards[1].AvatarImage.sprite.name,
                    "未满足订单移动到后面后，头像仍应来自该订单的 CharacterId");
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void Presenter_BindsTheFourAddedCustomerAvatarsToTheirCharacterIds()
        {
            var root = Object.Instantiate(Resources.Load<GameUiView>("UI/Prefabs/GameUi"));
            string savePath = Path.Combine(Path.GetTempPath(), "merge-added-avatar-order-binding.json");
            try
            {
                var state = GameState.CreateNew(seed: 23u);
                state.Board.Clear();
                state.Orders.SetActiveCount(4);
                string[] expectedNames =
                {
                    "avatar_redhead_chef",
                    "avatar_gardener",
                    "avatar_mentor",
                    "avatar_explorer"
                };
                for (int i = 0; i < expectedNames.Length; i++)
                {
                    state.Orders.SetOrder(i, new Order
                    {
                        ItemId = 101,
                        RequiredItemIds = new System.Collections.Generic.List<int> { 101 },
                        CharacterId = i + 4
                    });
                }

                var presenter = root.gameObject.AddComponent<GamePresenter>();
                presenter.Initialize(state, new SaveService(savePath), null);

                for (int i = 0; i < expectedNames.Length; i++)
                    Assert.AreEqual(expectedNames[i], presenter.OrderCards[i].AvatarImage.sprite.name,
                        $"订单 {i + 1} 应显示新增人物头像");
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                Object.DestroyImmediate(root.gameObject);
            }
        }
    }
}
