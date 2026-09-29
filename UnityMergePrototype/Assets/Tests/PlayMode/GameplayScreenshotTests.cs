using System.Collections;
using System.IO;
using MergePrototype.Core;
using MergePrototype.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MergePrototype.Tests.PlayMode
{
    /// <summary>
    /// 扩展验证：在 PlayMode 中真实模拟「生成 → 合成 → 获得金币」流程，
    /// 并把运行画面渲染为 1080×1920 PNG，供人工核对界面效果。
    /// </summary>
    public class GameplayScreenshotTests
    {
        private static string SavePath
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(projectRoot, "Temp", "merge-screenshot-test-save.json");
            }
        }

        private static string ScreenshotPath
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(projectRoot, "Screenshots", "gameplay-prototype.png");
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
        public IEnumerator SimulateGameplay_AndCaptureScreenshot()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;

            var presenter = Object.FindObjectOfType<GamePresenter>();
            Assert.IsNotNull(presenter);
            var state = presenter.State;

            // 1) 连续点击生成器 14 次（第一次填满，后续点击保持棋盘不变）。
            for (int i = 0; i < 14; i++)
            {
                presenter.GeneratorButton.onClick.Invoke();
            }
            yield return null;
            Assert.AreEqual(EnergyModel.InitialEnergy, state.Energy.Current);
            Assert.AreEqual(0, state.Board.GetEmptyIndices().Count);

            // 2) 制造一次真实合成：保证棋盘上有两个相同物品后走 HandleDrop。
            // 视觉回归测试不关注格子解锁流程，先将棋盘设为可操作状态。
            for (int i = 0; i < BoardModel.CellCount; i++)
                state.Board.SetUnlockState(i, CellUnlockState.Unlocked);
            state.Board.SetItemAt(0, 101);
            state.Board.SetItemAt(1, 101);
            var merge = state.HandleDrop(0, 1);
            Assert.AreEqual(DropResultType.Merged, merge.Type);
            Assert.AreEqual(102, state.Board.GetItemAt(1));

            // 3) 完成一单（放置订单所需物品后提交）。
            var order = state.Orders.GetOrder(0);
            for (int i = 0; i < order.Count; i++)
            {
                state.Board.SetItemAt(20 + i, order.ItemId);
            }
            bool submitted = state.TrySubmitOrder(0, out int reward, out string submitError);
            Assert.IsTrue(submitted, submitError);
            Assert.AreEqual(reward, state.Coins);

            presenter.SetStatus($"合成成功：甜点 L2！订单完成 +{reward} 金币");
            presenter.RefreshAll();
            yield return null;

            // 4) 渲染 1080×1920 截图（手动 cam.Render()，无需等待帧结束）。
            CaptureCanvas(presenter.GetComponent<Canvas>(), ScreenshotPath);
            Assert.IsTrue(File.Exists(ScreenshotPath), "截图应已生成");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                // 非 headless 环境由 PNG 像素探针验证画布确实有内容。
                var probe = new Texture2D(2, 2, TextureFormat.RGB24, false);
                Assert.IsTrue(probe.LoadImage(File.ReadAllBytes(ScreenshotPath)), "截图 PNG 应可解码");
                var pixels = probe.GetPixels32();
                bool varied = false;
                for (int i = 1; i < pixels.Length; i++)
                {
                    if (!pixels[i].Equals(pixels[0])) { varied = true; break; }
                }
                Assert.IsTrue(varied, "非 headless 环境下截图不应是纯色空帧");
                Object.Destroy(probe);
            }
            else
            {
                Debug.LogWarning("[GameplayScreenshotTests] -nographics 下跳过像素内容断言，仅验证截图文件生成。");
            }
            Debug.Log($"[GameplayScreenshotTests] 截图已保存到 {ScreenshotPath}");
        }

        /// <summary>把 ScreenSpace-Overlay 画布临时切到相机模式并渲染到 PNG。</summary>
        private static void CaptureCanvas(Canvas canvas, string path)
        {
            const int width = 1080;
            const int height = 1920;

            var camGo = new GameObject("ScreenshotCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.11f, 0.12f, 0.16f);
            cam.orthographic = true;
            cam.nearClipPlane = -10f;
            cam.farClipPlane = 10f;

            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;

            var oldMode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;

            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            canvas.renderMode = oldMode;
            canvas.worldCamera = null;
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(camGo);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}
