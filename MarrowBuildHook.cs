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
using static UnityEngine.EventSystems.EventTrigger;

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
            Dictionary<string, string> modifiedPrefabs = new();

            UnityEditor.EditorApplication.CallbackFunction fixup = null;
            fixup = () =>
            {
                var dictCopy = modifiedPrefabs.ToArray();
                foreach (var changed in dictCopy)
                {
                    try
                    {
                        File.WriteAllText(changed.Key, changed.Value);
                        modifiedPrefabs.Remove(changed.Key);
                        Debug.LogWarning("[MBH] Restored prefab: " + changed.Key);
                    } catch(Exception)
                    {
                        Debug.LogWarning("[MBH] Failed to restore prefab: " + changed.Key);
                        Debug.LogWarning("[MBH] Retrying...");
                        // in case of restoration failure, we'll try again next delayCall
                    }
                }
                if (modifiedPrefabs.Count > 0)
                    EditorApplication.delayCall += fixup; // (try again ...)
                else
                {
                    // Finally, force reimport the restored prefabs.
                    AssetDatabase.Refresh();
                }
            };
            EditorApplication.delayCall += fixup;

            foreach (var entry in aaContext.assetEntries)
            {
                if (entry.MainAssetType == typeof(GameObject))
                {
                    if (entry.AssetPath.EndsWith(".obj") || entry.AssetPath.EndsWith(".fbx") || entry.AssetPath.EndsWith(".blend")) continue;
                    modifiedPrefabs.Add(entry.AssetPath, File.ReadAllText(entry.AssetPath));

                    try
                    {
                        var prefab = PrefabUtility.LoadPrefabContents(entry.AssetPath);
                        try
                        {
                            ProcessGameObject(new GameObject[] { prefab }); // yayyyyy, array allocation
                            PrefabUtility.SaveAsPrefabAsset(prefab, entry.AssetPath);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[MBH]: Failed to Process prefab: " + entry.AssetPath);
                            Debug.LogWarning(ex);
                        }
                        PrefabUtility.UnloadPrefabContents(prefab);
                    } catch (Exception ex)
                    {
                        Debug.LogWarning("[MBH]: Failed to Process prefab: " + entry.AssetPath);
                        Debug.LogWarning(ex);
                    }
                }
            }

            
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
                } catch(Exception ex) 
                {
                    Debug.LogWarning("[MBH]: Failed to Process externalPass for gameobjects: [" + string.Join(", ", gameObject.Select(g => g.name)) + "]");
                    Debug.LogWarning(ex);
                }
            }

            try
            {
                var passes = gameObject.SelectMany(g => g.GetComponentsInChildren<IBuildPass>(true)).Where(bp => bp.PassWhenInactive || ((Component)bp).gameObject.activeInHierarchy).ToList();
                passes.Sort((a, b) => Comparer<int>.Default.Compare(a.PassPriority, b.PassPriority));
                foreach (var pass in passes)
                {
                    try
                    {
                        pass.OnBuild();
                    } catch(Exception ex)
                    {
                        Debug.LogWarning("[MBH]: Failed to Execute IBuildPass " + pass.GetType().Name + " on " + ((Component)pass).gameObject.name);
                        Debug.LogWarning(ex);
                    }
                }
            } catch(Exception ex)
            {
                Debug.LogWarning("[MBH]: Failed to Collect IBuildPasses for gameobjects: [" + string.Join(", ", gameObject.Select(g => g.name)) + "]");
                Debug.LogWarning(ex);
            }
        }
    }
}
#endif
