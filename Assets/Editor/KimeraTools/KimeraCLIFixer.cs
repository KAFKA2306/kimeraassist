using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Kimera.Editor
{
    public static class KimeraCLIFixer
    {
        // Entry point for Unity batchmode: -executeMethod Kimera.Editor.KimeraCLIFixer.RunOneClick [-kimeraPrefab="Assets/..prefab"]
        public static void RunOneClick()
        {
            try
            {
                string prefabPath = GetArg("-kimeraPrefab");

                if (!string.IsNullOrEmpty(prefabPath))
                {
                    Debug.Log($"[KimeraCLI] Running One-Click Fix on prefab: {prefabPath}");
                    KimeraAvatarFixer.RunAutoFixOnPrefabPath(prefabPath);
                    return;
                }

                // Fallback: try current selection (unlikely in batchmode)
                var go = Selection.activeGameObject;
                if (go != null)
                {
                    var desc = go.GetComponentInParent<VRCAvatarDescriptor>();
                    if (desc != null)
                    {
                        Debug.Log("[KimeraCLI] Running One-Click Fix on selected avatar in scene.");
                        KimeraAvatarFixer.RunAutoFixForSelection();
                        return;
                    }
                }

                // Final fallback: search for first avatar descriptor prefab under Assets
                var guids = AssetDatabase.FindAssets("t:Prefab");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    if (prefab.GetComponentInChildren<VRCAvatarDescriptor>(true) != null)
                    {
                        Debug.Log($"[KimeraCLI] Detected avatar prefab at {path}. Running fix.");
                        KimeraAvatarFixer.RunAutoFixOnPrefabPath(path);
                        return;
                    }
                }

                Debug.LogError("[KimeraCLI] No target specified and no avatar prefab found. Provide -kimeraPrefab=Assets/your.prefab");
            }
            catch (Exception e)
            {
                Debug.LogError($"[KimeraCLI] Exception: {e}");
                throw;
            }
        }

        private static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            // formats: -name=value or -name value
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                {
                    return a.Substring(name.Length + 1).Trim('"');
                }
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    return args[i + 1].Trim('"');
                }
            }
            return null;
        }
    }
}

