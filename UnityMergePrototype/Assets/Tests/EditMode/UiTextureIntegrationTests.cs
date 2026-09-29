using System.IO;
using MergePrototype.Core;
using MergePrototype.Persistence;
using MergePrototype.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MergePrototype.Tests.EditMode
{
    public class UiTextureIntegrationTests
    {
        [Test]
        public void BuildsPrototypeUiWithSlicedSprites()
        {
            var host = new GameObject("UiTextureIntegrationTest");
            string savePath = Path.Combine(Path.GetTempPath(), "merge-ui-sprite-test.json");
            try
            {
                var presenter = host.AddComponent<GamePresenter>();
                presenter.Initialize(GameState.CreateNew(), new SaveService(savePath), null);

                var gameUi = host.transform.Find("GameUi");
                Assert.IsNotNull(gameUi, "Presenter should instantiate the editable GameUi prefab");
                AssertSprite(gameUi.gameObject, "LevelPanel", "ui_level_panel_base");
                AssertSprite(gameUi.gameObject, "OrdersSection/OrdersHeader", "ui_orders_header");
                AssertSprite(gameUi.gameObject, "BoardFrame", "ui_board_frame_empty");
                AssertSprite(gameUi.gameObject, "GeneratorButton", "ui_serve_items_button");
                AssertSprite(gameUi.gameObject, "EncyclopediaButton", "ui_collection_closed_book");

                var boardCells = gameUi.GetComponentsInChildren<BoardCellView>(true);
                Assert.AreEqual(BoardModel.CellCount, boardCells.Length);
                // The current new-game layout intentionally fills every cell;
                // verify the editable prefab's empty-state sprite separately.
                var boardCellPrefab = Resources.Load<BoardCellView>("UI/Prefabs/BoardCell");
                Assert.AreEqual("ui_board_cell_empty", boardCellPrefab.Background.sprite.name);

                var orderCards = gameUi.GetComponentsInChildren<OrderCardView>(true);
                Assert.AreEqual(OrderModel.SlotCount, orderCards.Length);
                Assert.AreEqual("ui_order_card_frame", orderCards[0].transform.Find("Background")
                    .GetComponent<Image>().sprite.name);
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                Object.DestroyImmediate(host);
            }
        }

        private static void AssertSprite(GameObject root, string childName, string expectedName)
        {
            var target = string.IsNullOrEmpty(childName) ? root : root.transform.Find(childName).gameObject;
            Assert.IsNotNull(target, $"Missing UI object: {childName ?? root.name}");
            var image = target.GetComponent<Image>();
            Assert.IsNotNull(image, $"Missing Image on UI object: {target.name}");
            Assert.IsNotNull(image.sprite, $"Missing sprite on UI object: {target.name}");
            Assert.AreEqual(expectedName, image.sprite.name, $"Unexpected sprite on UI object: {target.name}");
        }
    }
}
