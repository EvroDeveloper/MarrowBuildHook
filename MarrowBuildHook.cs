#if UNITY_EDITOR
using HarmonyLib;
using SLZ.MarrowEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Assertions.Must;

namespace MarrowBuildHook
{
    [InitializeOnLoad]
    public static class MarrowBuildHook
    {
        public static List<Action<IEnumerable<GameObject>>> ExternalGameObjectProcesses = new();


        static MethodInfo setIconEnabled;
        static MethodInfo SetIconEnabled => setIconEnabled = setIconEnabled ??
            Assembly.GetAssembly(typeof(Editor))
            ?.GetType("UnityEditor.AnnotationUtility")
            ?.GetMethod("SetIconEnabled", BindingFlags.Static | BindingFlags.NonPublic);

        public static void SetGizmoIconEnabled(Type type, bool on)
        {
            if (SetIconEnabled == null) return;
            const int MONO_BEHAVIOR_CLASS_ID = 114; // https://docs.unity3d.com/Manual/ClassIDReference.html
            SetIconEnabled.Invoke(null, new object[] { MONO_BEHAVIOR_CLASS_ID, type.Name, on ? 1 : 0 });
        }

        static MarrowBuildHook()
        {
            Harmony h = new($"holadivinus.{typeof(MarrowBuildHook).Name}.patches");

            // One of the dumbest solutions i've come up with
            // wait 2 "editor updates" so GUIStyle is initted, meaning
            // we can load UltEvent types without it triggering an errored init
            // of required ult assets
            EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
            {
                var doBuild = typeof(MarrowContentBuildScript).GetMethod("DoBuild", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(typeof(AddressablesPlayerBuildResult));
                var doBuildPrefixM = typeof(MarrowBuildHook).GetMethod(nameof(doBuildPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                h.Patch(doBuild, prefix: doBuildPrefixM);
            };

            SetGizmoIconEnabled(typeof(EDITOR_ONLY), false);
            SetGizmoIconEnabled(typeof(MODIFY_PASS), false);
            SetGizmoIconEnabled(typeof(REPARENT_PASS), false);
        }
        private static void doBuildPrefix(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext)
        {
            // Close Open Prefab UI's
            var openPrefab = PrefabStageUtility.GetCurrentPrefabStage();
            if (openPrefab != null)
            {
                MethodInfo savePrefabMethod = typeof(PrefabStage).GetMethod("SavePrefab", BindingFlags.NonPublic | BindingFlags.Instance);
                savePrefabMethod.Invoke(openPrefab, null);

                StageUtility.GoBackToPreviousStage();
            }

            List<string> errors = new();
            Dictionary<string, string>  modifiedPrefabs = new();
            foreach (var entry in aaContext.assetEntries)
            {
                if (entry.MainAssetType == typeof(GameObject))
                {
                    modifiedPrefabs.Add(entry.AssetPath, File.ReadAllText(entry.AssetPath));

                    var prefab = PrefabUtility.LoadPrefabContents(entry.AssetPath);
                    if (prefab.name == "VarEnsurer") continue;
                    try
                    {
                        ProcessGameObject(new GameObject[] { prefab }); // yayyyyy, array allocation
                    } catch(Exception) { }
                    PrefabUtility.SaveAsPrefabAsset(prefab, entry.AssetPath);
                    PrefabUtility.UnloadPrefabContents(prefab);
                }
            }

            EditorApplication.delayCall += () =>
            {
                foreach (var changed in modifiedPrefabs)
                    File.WriteAllText(changed.Key, changed.Value);
            };
        }

        public static void BigPrint(object msg)
        {
            Debug.Log(msg);
            File.AppendAllText("C:\\Users\\Holadivinus\\Downloads\\test.txt", msg.ToString() + Environment.NewLine);
        }

        public static void ProcessGameObject(IEnumerable<GameObject> gameObject)
        {
            foreach (var externalPass in ExternalGameObjectProcesses)
            {
                try
                {
                    externalPass.Invoke(gameObject);
                } catch(Exception) { }
            }

            var passes = gameObject.SelectMany(g => g.GetComponentsInChildren<IBuildPass>(true)).Where(bp => bp.PassWhenInactive || ((Component)bp).gameObject.activeInHierarchy).ToList();
            passes.Sort((a, b) => Comparer<int>.Default.Compare(a.PassPriority, b.PassPriority));
            foreach (var pass in passes)
                pass.OnBuild();
        }
    }
}
#endif
