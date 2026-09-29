using System.Collections.Generic;
using MergePrototype.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MergePrototype.EditorTools
{
    /// <summary>
    /// 可重复生成主场景 Assets/Scenes/Main.unity，并保证它是 Build Settings 第一个场景。
    /// 菜单与批处理（-executeMethod）共用同一入口。
    /// </summary>
    public static class MainSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Merge Prototype/Build Main Scene")]
        public static void BuildMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 相机：竖屏 uGUI 纯色背景。
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.11f, 0.12f, 0.16f);
            camera.orthographic = true;
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            // 事件系统（GameBootstrap 运行时也会兜底创建）。
            var eventGo = new GameObject("EventSystem");
            eventGo.AddComponent<EventSystem>();
            eventGo.AddComponent<StandaloneInputModule>();

            // 启动入口。
            var bootstrapGo = new GameObject("GameBootstrap");
            bootstrapGo.AddComponent<GameBootstrap>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            // 保证 Main 是 Build Settings 第一个场景。
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[MainSceneBuilder] 已生成 {ScenePath} 并设为第一个构建场景。");
        }

        /// <summary>在 Editor 中打开主场景（供 GUI 启动参数 -executeMethod 使用）。</summary>
        [MenuItem("Merge Prototype/Open Main Scene")]
        public static void OpenMainScene()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                BuildMainScene();
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[MainSceneBuilder] 已打开 {ScenePath}，点击 Play 开始验证。");
        }
    }
}
