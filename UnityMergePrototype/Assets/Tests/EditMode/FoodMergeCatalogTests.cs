using MergePrototype.Core;
using NUnit.Framework;

namespace MergePrototype.Tests.EditMode
{
    public class FoodMergeCatalogTests
    {
        [Test]
        public void FoodCatalog_HasTenKitchenChains_WithEightLevelsEach()
        {
            var catalog = new ItemCatalog();
            Assert.AreEqual(8, ItemCatalog.MaxLevel);
            Assert.AreEqual(10, catalog.LevelOneIds.Count);

            for (int chain = 1; chain <= ItemCatalog.ChainCount; chain++)
            {
                for (int level = 1; level <= 8; level++)
                {
                    int id = chain * 100 + level;
                    Assert.IsTrue(catalog.TryGet(id, out var item), $"缺少物品 ID {id}");
                    Assert.AreEqual(level, item.Level);
                    int expectedNext = level < 8 ? id + 1 : ItemCatalog.EmptyId;
                    Assert.AreEqual(expectedNext, catalog.GetNextId(id));
                }
            }
        }

        [Test]
        public void FoodCatalog_UsesConcreteNamesAndIconsForEveryLevel()
        {
            var catalog = new ItemCatalog();
            for (int chain = 1; chain <= ItemCatalog.ChainCount; chain++)
            {
                for (int level = 1; level <= 8; level++)
                {
                    var item = catalog.Get(chain * 100 + level);
                    Assert.IsNotEmpty(item.DisplayName, $"ID {item.Id} 缺少名称");
                    Assert.IsNotEmpty(item.Emoji, $"ID {item.Id} 缺少图标");
                }
            }
        }

        [Test]
        public void FoodCatalog_UsesLatestFruitCookwareNoodlesChainsAndSpriteSlots()
        {
            var catalog = new ItemCatalog();

            Assert.AreEqual("Blueberry", catalog.Get(301).DisplayName);
            Assert.AreEqual("fruit", catalog.Get(301).IconSheet);
            Assert.AreEqual(0, catalog.Get(301).IconIndex);

            Assert.AreEqual("Wheat Sprig", catalog.Get(501).DisplayName);
            Assert.AreEqual("noodles", catalog.Get(501).IconSheet);
            Assert.AreEqual(0, catalog.Get(501).IconIndex);

            Assert.AreEqual("Wooden Spoon", catalog.Get(601).DisplayName);
            Assert.AreEqual("cookware", catalog.Get(601).IconSheet);
            Assert.AreEqual(0, catalog.Get(601).IconIndex);

            for (int chain = 1; chain <= ItemCatalog.ChainCount; chain++)
            {
                for (int level = 1; level <= ItemCatalog.MaxLevel; level++)
                {
                    var item = catalog.Get(chain * 100 + level);
                    Assert.IsNotEmpty(item.IconSheet, $"ID {item.Id} 缺少图标资源名");
                    Assert.AreEqual(level - 1, item.IconIndex, $"ID {item.Id} 图标槽位错误");
                }
            }
        }
    }
}
