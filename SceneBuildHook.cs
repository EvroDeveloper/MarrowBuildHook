#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MarrowBuildHook
{
    public static class SceneBuildHook
    {
        [UnityEditor.Callbacks.PostProcessScene(0)]
        public static void BuildCallBack()
        {
            List<Scene> activeScenes = new();
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                activeScenes.Add(EditorSceneManager.GetSceneAt(i));

            foreach (var s in activeScenes)
            {
                try
                {
                    MarrowBuildHook.ProcessGameObject(s.GetRootGameObjects());
                } catch (Exception) { }
            }
        }
    }
}
#endif