using System.Collections;
using System.IO;
using MergePrototype.Core;
using MergePrototype.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MergePrototype.Tests.PlayMode
{
    public class TestAVariantPlayModeTests
    {
        private static string SavePath
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(projectRoot, "Temp", "merge-test-a-playmode-save.json");
            }
        }

        [SetUp]
        public void SetUp()
        {
            GameBootstrap.VariantOverride = GameVariant.TestA;
            GameBootstrap.SavePathOverride = SavePath;
            try { if (File.Exists(SavePath)) File.Delete(SavePath); } catch { }
        }

        [TearDown]
        public void TearDown()
        {
            GameBootstrap.VariantOverride = null;
            GameBootstrap.SavePathOverride = null;
            try { if (File.Exists(SavePath)) File.Delete(SavePath); } catch { }
        }

        [UnityTest]
        public IEnumerator MainScene_CanLaunchTestAWithoutChangingCurrentRules()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;

            var presenter = Object.FindObjectOfType<GamePresenter>();
            Assert.IsNotNull(presenter);
            Assert.AreEqual(GameVariant.TestA, presenter.State.Variant);
            Assert.AreEqual(501, presenter.State.Board.GetItemAt(0));
            Assert.AreEqual(5, presenter.State.Catalog.RegularChainCount);
            Assert.AreEqual(7, presenter.State.Catalog.MaxRegularLevel);
            Assert.AreEqual(int.MaxValue, presenter.State.VariantConfig.MaxPlayerLevel);
        }
    }
}
