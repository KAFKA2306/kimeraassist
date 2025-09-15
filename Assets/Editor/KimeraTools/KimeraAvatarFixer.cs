using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using UnityEditor.Animations;

namespace Kimera.Editor
{
    public static class KimeraAvatarFixer
    {
        public static void RunAutoFixForSelection()
        {
            // シーン上のGameObject選択が最優先
            var go = Selection.activeGameObject;
            if (go != null)
            {
                var descriptor = go.GetComponentInParent<VRCAvatarDescriptor>();
                if (descriptor == null)
                {
                    Debug.LogError("[Kimera] VRCAvatarDescriptorが見つかりません。アバターのルートを選択してください。");
                    return;
                }
                RunAutoFixInternal(descriptor.gameObject, descriptor);
                return;
            }

            // プレハブアセットが選択されている場合
            var obj = Selection.activeObject;
            if (obj != null)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    RunAutoFixOnPrefabPath(path);
                    return;
                }
            }

            Debug.LogError("[Kimera] アバターのGameObjectまたはPrefabアセットを選択してください。");
        }

        private static void RunAutoFixInternal(GameObject root, VRCAvatarDescriptor descriptor)
        {
            int changed = 0;

            try { changed += EnsureMergeAnimatorPathAbsolute(root); }
            catch (Exception e) { Debug.LogWarning($"[Kimera] MergeAnimatorのPathMode設定中に例外: {e.Message}"); }

            try { changed += EnsureLipSyncAndVisemes(descriptor); }
            catch (Exception e) { Debug.LogWarning($"[Kimera] リップシンク設定中に例外: {e.Message}"); }

            // MergeArmature: ターゲット自動割当（AvatarObjectReferenceに安全にセット）
            try
            {
                int assigned = AutoAssignMergeArmatureTargets(descriptor);
                if (assigned > 0)
                {
                    changed += assigned;
                    Debug.Log($"[Kimera] MergeArmature のターゲットを {assigned} 件自動設定しました。");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Kimera] MergeArmature自動設定で例外: {e.Message}");
            }

            // FaceEmo 連動: Expressions(Parameters/Menu) と FX の配線を補強
            try
            {
                changed += EnsureFaceEmoLinkage(descriptor);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Kimera] FaceEmo連動設定で例外: {e.Message}");
            }

            // Animator の欠落/空パラメータ修正
            try
            {
                int fixedParams = FixAnimatorParameterIssues(descriptor);
                if (fixedParams > 0)
                {
                    changed += fixedParams;
                    Debug.Log($"[Kimera] Animatorの空/未定義パラメータ参照を {fixedParams} 箇所修正しました。");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Kimera] Animatorパラメータ修正で例外: {e.Message}");
            }

            // 2.5) 表情アニメの壊れた参照パスを現在のFace Meshへリダイレクト
            try
            {
                var faceMesh = descriptor.VisemeSkinnedMesh != null ? descriptor.VisemeSkinnedMesh : FindBestFaceMesh(descriptor.gameObject);
                if (faceMesh != null)
                {
                    int retargeted = RetargetExpressionClipsToFaceMesh(root, descriptor, faceMesh);
                    if (retargeted > 0)
                    {
                        changed += retargeted;
                        Debug.Log($"[Kimera] 表情アニメの参照パスを {retargeted} 件修正しました。");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Kimera] 表情アニメ参照パス修正で例外: {e.Message}");
            }

            // 安全対策: Missing(MonoBehaviour)の掃除 + VRC Constraintsの検出/無効化オプション
            try
            {
                int removedMissing = RemoveMissingScripts(root);
                if (removedMissing > 0)
                {
                    changed += removedMissing;
                    Debug.Log($"[Kimera] Missing(MonoBehaviour) を {removedMissing} 件除去しました。");
                }

                int disabledConstraints = DisableVRCDynamicsConstraints(root);
                if (disabledConstraints > 0)
                {
                    changed += disabledConstraints; // 便宜上カウント
                    Debug.LogWarning($"[Kimera] 物理ボーン依存によるビルド失敗を回避するため VRC Constraints を {disabledConstraints} 件一時的に無効化しました。必要に応じて再有効化してください。");
                }

                int disabledInvalidMergeArm = DisableInvalidMergeArmatures(root);
                if (disabledInvalidMergeArm > 0)
                {
                    changed += disabledInvalidMergeArm;
                    Debug.LogWarning($"[Kimera] 統合先未設定のModular Avatar MergeArmatureを {disabledInvalidMergeArm} 件無効化しました（MA-1400回避）。");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Kimera] クリーンアップ処理で例外: {e.Message}");
            }

            if (changed > 0)
            {
                EditorUtility.SetDirty(descriptor);
                EditorSceneManager.MarkSceneDirty(descriptor.gameObject.scene);
                Debug.Log($"[Kimera] 自動修正を完了しました（{changed}件更新）。シーンを保存するか、PrefabならApplyを実行してください。");
            }
            else
            {
                Debug.Log("[Kimera] 変更はありませんでした。設定は既に適切です。");
            }
        }

        public static void RunAutoFixOnPrefabPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError("[Kimera] Prefabパスが空です。");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                var descriptor = root != null ? root.GetComponentInChildren<VRCAvatarDescriptor>(true) : null;
                if (descriptor == null)
                {
                    Debug.LogError($"[Kimera] Prefab内にVRCAvatarDescriptorが見つかりません: {assetPath}");
                    return;
                }

                RunAutoFixInternal(descriptor.gameObject, descriptor);
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                Debug.Log($"[Kimera] Prefabへ保存しました: {assetPath}");
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int EnsureMergeAnimatorPathAbsolute(GameObject root)
        {
            int changed = 0;

            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            if (behaviours == null || behaviours.Length == 0) return 0;

            int found = 0;
            foreach (var mb in behaviours)
            {
                if (mb == null) continue; // missing script
                var t = mb.GetType();
                var ns = t.Namespace ?? string.Empty;
                // MergeAnimator 判定を緩める（名前と名前空間の一部一致）
                if (!string.Equals(t.Name, "MergeAnimator", StringComparison.Ordinal) || !ns.ToLowerInvariant().Contains("modular_avatar"))
                {
                    continue;
                }

                found++;

                // pathMode のメンバー（プロパティ or フィールド）を探索
                var prop = t.GetProperty("pathMode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                var field = t.GetField("pathMode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

                Type enumType = prop != null ? prop.PropertyType : field != null ? field.FieldType : null;
                if (enumType == null || !enumType.IsEnum)
                {
                    // 一部バージョンで型が分かりにくい場合は、アセンブリ内から PathMode を推定
                    enumType = t.Assembly.GetTypes().FirstOrDefault(x => x.IsEnum && x.Name.Equals("PathMode", StringComparison.OrdinalIgnoreCase));
                }

                if (enumType == null || !enumType.IsEnum)
                {
                    Debug.LogWarning($"[Kimera] {t.FullName} の pathMode 型を特定できませんでした。");
                    continue;
                }

                object absoluteValue = null;
                try
                {
                    absoluteValue = Enum.GetValues(enumType).Cast<object>().FirstOrDefault(v => string.Equals(v.ToString(), "Absolute", StringComparison.OrdinalIgnoreCase));
                }
                catch { }

                if (absoluteValue == null)
                {
                    // Fallback: 1 を Absolute とみなす（0:Relative, 1:Absolute であるケースが多い）
                    try { absoluteValue = Enum.ToObject(enumType, 1); } catch { }
                }

                if (absoluteValue == null)
                {
                    Debug.LogWarning("[Kimera] PathMode.Absolute を解決できませんでした。");
                    continue;
                }

                try
                {
                    bool updated = false;
                    if (prop != null && prop.CanWrite)
                    {
                        var cur = prop.GetValue(mb, null);
                        if (!Equals(cur, absoluteValue))
                        {
                            prop.SetValue(mb, absoluteValue);
                            updated = true;
                        }
                    }
                    else if (field != null)
                    {
                        var cur = field.GetValue(mb);
                        if (!Equals(cur, absoluteValue))
                        {
                            field.SetValue(mb, absoluteValue);
                            updated = true;
                        }
                    }

                    if (updated)
                    {
                        EditorUtility.SetDirty(mb);
                        changed++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Kimera] MergeAnimator pathMode 設定失敗: {ex.Message}");
                }
            }

            if (found == 0)
            {
                Debug.Log("[Kimera] MergeAnimator コンポーネントが見つかりませんでした（対象に未追加の可能性）。");
            }

            return changed;
        }

        private static int EnsureLipSyncAndVisemes(VRCAvatarDescriptor descriptor)
        {
            int changed = 0;

            // Face Meshの決定
            var faceMesh = descriptor.VisemeSkinnedMesh;
            if (faceMesh == null)
            {
                faceMesh = FindBestFaceMesh(descriptor.gameObject);
                if (faceMesh != null)
                {
                    descriptor.VisemeSkinnedMesh = faceMesh;
                    changed++;
                }
            }

            if (faceMesh == null)
            {
                Debug.LogWarning("[Kimera] Face Mesh（SkinnedMeshRenderer）を特定できませんでした。手動で指定してください。");
                return changed;
            }

            // LipSyncモードをVisemeBlendShapeへ
            if (descriptor.lipSync != VRCAvatarDescriptor.LipSyncStyle.VisemeBlendShape)
            {
                descriptor.lipSync = VRCAvatarDescriptor.LipSyncStyle.VisemeBlendShape;
                changed++;
            }

            // Viseme配列を自動割当（可能な範囲で）。不足分は安全な既存シェイプで補完。
            var names = GetBlendShapeNames(faceMesh);
            var visemes = BuildVisemeArrayWithFallback(names);

            if (visemes != null)
            {
                var before = descriptor.VisemeBlendShapes != null ? string.Join(",", descriptor.VisemeBlendShapes) : "";
                var after = string.Join(",", visemes);
                if (before != after)
                {
                    descriptor.VisemeBlendShapes = visemes;
                    changed++;
                }
            }

            return changed;
        }

        private static int FixAnimatorParameterIssues(VRCAvatarDescriptor descriptor)
        {
            int fixedCount = 0;
            if (descriptor == null) return 0;

            IEnumerable<AnimatorController> AllControllers()
            {
                foreach (var l in descriptor.baseAnimationLayers)
                {
                    var ac = l.animatorController as AnimatorController; if (ac != null) yield return ac;
                }
                foreach (var l in descriptor.specialAnimationLayers)
                {
                    var ac = l.animatorController as AnimatorController; if (ac != null) yield return ac;
                }
            }

            foreach (var ac in AllControllers())
            {
                var paramNames = new HashSet<string>(ac.parameters.Select(p => p.name));

                foreach (var layer in ac.layers)
                {
                    var sm = layer.stateMachine;
                    fixedCount += FixStateMachine(sm, paramNames);
                }
            }

            return fixedCount;
        }

        private static int FixStateMachine(AnimatorStateMachine sm, HashSet<string> paramNames)
        {
            int fixedCount = 0;
            if (sm == null) return 0;

            foreach (var child in sm.states)
            {
                var s = child.state;
                if (s.speedParameterActive && (string.IsNullOrEmpty(s.speedParameter) || !paramNames.Contains(s.speedParameter))) { s.speedParameterActive = false; fixedCount++; }
                if (s.timeParameterActive && (string.IsNullOrEmpty(s.timeParameter) || !paramNames.Contains(s.timeParameter))) { s.timeParameterActive = false; fixedCount++; }
                if (s.mirrorParameterActive && (string.IsNullOrEmpty(s.mirrorParameter) || !paramNames.Contains(s.mirrorParameter))) { s.mirrorParameterActive = false; fixedCount++; }
                if (s.cycleOffsetParameterActive && (string.IsNullOrEmpty(s.cycleOffsetParameter) || !paramNames.Contains(s.cycleOffsetParameter))) { s.cycleOffsetParameterActive = false; fixedCount++; }

                // state transitions
                foreach (var t in s.transitions)
                {
                    var conds = t.conditions;
                    if (conds == null || conds.Length == 0) continue;
                    var filtered = conds.Where(c => !string.IsNullOrEmpty(c.parameter) && paramNames.Contains(c.parameter)).ToArray();
                    if (filtered.Length != conds.Length)
                    {
                        t.conditions = filtered;
                        fixedCount++;
                    }
                }

                // recurse blend trees motions if needed (no param fix here)
            }

            // Any transitions on the state machine itself
            foreach (var t in sm.anyStateTransitions)
            {
                var conds = t.conditions;
                if (conds == null || conds.Length == 0) continue;
                var filtered = conds.Where(c => !string.IsNullOrEmpty(c.parameter) && paramNames.Contains(c.parameter)).ToArray();
                if (filtered.Length != conds.Length)
                {
                    t.conditions = filtered;
                    fixedCount++;
                }
            }
            foreach (var t in sm.entryTransitions)
            {
                var conds = t.conditions;
                if (conds == null || conds.Length == 0) continue;
                var filtered = conds.Where(c => !string.IsNullOrEmpty(c.parameter) && paramNames.Contains(c.parameter)).ToArray();
                if (filtered.Length != conds.Length)
                {
                    t.conditions = filtered;
                    fixedCount++;
                }
            }

            // sub state machines
            foreach (var sub in sm.stateMachines)
            {
                fixedCount += FixStateMachine(sub.stateMachine, paramNames);
            }

            return fixedCount;
        }

        private static int RetargetExpressionClipsToFaceMesh(GameObject root, VRCAvatarDescriptor descriptor, SkinnedMeshRenderer faceMesh)
        {
            int changed = 0;
            if (faceMesh == null) return 0;

            var clips = CollectAllClipsFromDescriptor(descriptor);
            if (clips.Count == 0) return 0;

            var newPath = AnimationUtility.CalculateTransformPath(faceMesh.transform, root.transform);

            foreach (var clip in clips)
            {
                bool clipChanged = false;

                var floatBindings = AnimationUtility.GetCurveBindings(clip);
                foreach (var b in floatBindings)
                {
                    if (b.type != typeof(SkinnedMeshRenderer)) continue;
                    if (string.IsNullOrEmpty(b.propertyName) || !b.propertyName.StartsWith("blendShape.")) continue;

                    var targetObj = AnimationUtility.GetAnimatedObject(root, b);
                    if (targetObj != null) continue; // 既に解決できる → 触らない

                    var curve = AnimationUtility.GetEditorCurve(clip, b);
                    if (curve == null) continue;

                    var newBinding = new EditorCurveBinding
                    {
                        path = newPath,
                        propertyName = b.propertyName,
                        type = typeof(SkinnedMeshRenderer)
                    };

                    AnimationUtility.SetEditorCurve(clip, b, null);
                    AnimationUtility.SetEditorCurve(clip, newBinding, curve);
                    clipChanged = true;
                }

                if (clipChanged)
                {
                    EditorUtility.SetDirty(clip);
                    changed++;
                }
            }

            return changed;
        }

        private static List<AnimationClip> CollectAllClipsFromDescriptor(VRCAvatarDescriptor descriptor)
        {
            var list = new List<AnimationClip>();
            if (descriptor == null) return list;

            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.animatorController == null) continue;
                CollectClipsFromController(layer.animatorController as AnimatorController, list);
            }
            foreach (var layer in descriptor.specialAnimationLayers)
            {
                if (layer.animatorController == null) continue;
                CollectClipsFromController(layer.animatorController as AnimatorController, list);
            }

            return list.Distinct().ToList();
        }

        private static void CollectClipsFromController(AnimatorController controller, List<AnimationClip> list)
        {
            if (controller == null) return;
            foreach (var layer in controller.layers)
            {
                CollectClipsFromStateMachine(layer.stateMachine, list);
            }
            // 直接参照される場合
            foreach (var clip in controller.animationClips)
            {
                if (clip != null) list.Add(clip);
            }
        }

        private static void CollectClipsFromStateMachine(AnimatorStateMachine sm, List<AnimationClip> list)
        {
            if (sm == null) return;
            foreach (var state in sm.states)
            {
                CollectClipsFromMotion(state.state.motion, list);
            }
            foreach (var sub in sm.stateMachines)
            {
                CollectClipsFromStateMachine(sub.stateMachine, list);
            }
        }

        private static void CollectClipsFromMotion(Motion motion, List<AnimationClip> list)
        {
            if (motion == null) return;
            var clip = motion as AnimationClip;
            if (clip != null)
            {
                list.Add(clip);
                return;
            }

            var bt = motion as BlendTree;
            if (bt != null)
            {
                foreach (var child in bt.children)
                {
                    CollectClipsFromMotion(child.motion, list);
                }
            }
        }

        private static int RemoveMissingScripts(GameObject root)
        {
            int total = 0;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                // UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScript はないため、SerializedObjectで削除
                var go = t.gameObject;
                var so = new SerializedObject(go);
                var compProp = so.FindProperty("m_Component");
                if (compProp == null) continue;

                for (int i = compProp.arraySize - 1; i >= 0; i--)
                {
                    var elem = compProp.GetArrayElementAtIndex(i);
                    var objRef = elem.FindPropertyRelative("component");
                    if (objRef != null && objRef.objectReferenceValue == null)
                    {
                        // Missing(MonoBehaviour)
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                        total++;
                        break; // 1つ以上あれば1カウント
                    }
                }
            }
            return total;
        }

        private static int DisableVRCDynamicsConstraints(GameObject root)
        {
            int count = 0;
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in behaviours)
            {
                if (mb == null) continue;
                var t = mb.GetType();
                var ns = t.Namespace ?? string.Empty;
                if (!ns.StartsWith("VRC.Dynamics")) continue;

                // 基底に VRCConstraintBase を持つか、型名に Constraint を含むものを対象
                bool looksConstraint = t.Name.EndsWith("Constraint", StringComparison.Ordinal) || InheritsFrom(t, "VRC.Dynamics.VRCConstraintBase");
                if (!looksConstraint) continue;

                var beh = mb as Behaviour;
                if (beh != null && beh.enabled)
                {
                    beh.enabled = false;
                    EditorUtility.SetDirty(beh);
                    count++;
                }
            }
            if (count > 0) EditorSceneManager.MarkSceneDirty(root.scene);
            return count;
        }

        private static bool InheritsFrom(Type t, string baseTypeFullName)
        {
            var cur = t;
            while (cur != null && cur != typeof(object))
            {
                if (string.Equals(cur.FullName, baseTypeFullName, StringComparison.Ordinal)) return true;
                cur = cur.BaseType;
            }
            return false;
        }

        private static SkinnedMeshRenderer FindBestFaceMesh(GameObject root)
        {
            var candidates = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (candidates == null || candidates.Length == 0) return null;

            SkinnedMeshRenderer best = null;
            int bestScore = -1;

            foreach (var smr in candidates)
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;

                var count = mesh.blendShapeCount;
                int score = 0;

                for (int i = 0; i < count; i++)
                {
                    var bs = mesh.GetBlendShapeName(i);
                    if (LooksLikeViseme(bs)) score++;
                }

                // 名前にface/head/eyeが入っていたらボーナス
                var lower = smr.name.ToLowerInvariant();
                if (lower.Contains("face") || lower.Contains("head") || lower.Contains("mesh_face"))
                {
                    score += 2;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = smr;
                }
            }

            return best;
        }

        private static bool LooksLikeViseme(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var n = name.ToLowerInvariant();
            if (n.StartsWith("vrc.v_")) return true;

            // よくある別名（日本語配布でのA/I/U/E/Oなど）
            string[] hints = { "sil", "pp", "ff", "th", "dd", "kk", "ch", "ss", "nn", "rr", "aa", "e", "ih", "oh", "ou", "a", "i", "u", "o" };
            return hints.Any(h => n.Contains(h));
        }

        private static string[] GetBlendShapeNames(SkinnedMeshRenderer smr)
        {
            var mesh = smr.sharedMesh;
            if (mesh == null) return Array.Empty<string>();

            var list = new List<string>();
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                list.Add(mesh.GetBlendShapeName(i));
            }
            return list.ToArray();
        }

        private static string[] BuildVisemeArrayWithFallback(string[] blendShapeNames)
        {
            // VRChatの順序（15個）に沿って割当
            var order = new (string key, string[] aliases)[]
            {
                ("sil", new [] { "vrc.v_sil", "sil", "silence", "basis", "neutral" }),
                ("PP",  new [] { "vrc.v_pp", "pp", "p", "m" }),
                ("FF",  new [] { "vrc.v_ff", "ff", "f" }),
                ("TH",  new [] { "vrc.v_th", "th" }),
                ("DD",  new [] { "vrc.v_dd", "dd", "l" }),
                ("kk",  new [] { "vrc.v_kk", "kk", "k", "g" }),
                ("CH",  new [] { "vrc.v_ch", "ch", "t", "ts" }),
                ("SS",  new [] { "vrc.v_ss", "ss", "s", "sh" }),
                ("nn",  new [] { "vrc.v_nn", "nn", "n" }),
                ("RR",  new [] { "vrc.v_rr", "rr", "r" }),
                ("aa",  new [] { "vrc.v_aa", "aa", "a" }),
                ("E",   new [] { "vrc.v_e",  "e" }),
                ("ih",  new [] { "vrc.v_ih", "ih", "i" }),
                ("oh",  new [] { "vrc.v_oh", "oh", "o" }),
                ("ou",  new [] { "vrc.v_ou", "ou", "u" })
            };

            var lowerSet = new HashSet<string>(blendShapeNames.Select(n => n.ToLowerInvariant()));

            string FindMatch(string[] aliases)
            {
                // 優先: vrc.v_*完全一致 → 別名含有一致 → 先頭/末尾一致
                foreach (var a in aliases)
                {
                    var low = a.ToLowerInvariant();
                    if (lowerSet.Contains(low)) return blendShapeNames.First(n => string.Equals(n, a, StringComparison.OrdinalIgnoreCase));
                }

                foreach (var name in blendShapeNames)
                {
                    var low = name.ToLowerInvariant();
                    if (aliases.Any(a => low.Contains(a.ToLowerInvariant()))) return name;
                }

                foreach (var name in blendShapeNames)
                {
                    var low = name.ToLowerInvariant();
                    if (aliases.Any(a => low.StartsWith(a.ToLowerInvariant()) || low.EndsWith(a.ToLowerInvariant()))) return name;
                }

                return string.Empty; // 見つからない場合は空
            }

            var visemes = new string[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                visemes[i] = FindMatch(order[i].aliases);
            }

            // いずれか空がある場合は安全な既存シェイプで埋める
            if (visemes.Any(string.IsNullOrEmpty))
            {
                // neutralに近い候補を優先して選ぶ
                string[] neutralHints = { "vrc.v_sil", "sil", "neutral", "basis" };
                string fallback = blendShapeNames.FirstOrDefault(n => neutralHints.Any(h => n.ToLowerInvariant().Contains(h)))
                                  ?? blendShapeNames.FirstOrDefault()
                                  ?? string.Empty;
                for (int i = 0; i < visemes.Length; i++)
                {
                    if (string.IsNullOrEmpty(visemes[i])) visemes[i] = fallback;
                }
            }

            return visemes;
        }

        private static int AutoAssignMergeArmatureTargets(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null) return 0;
            var root = descriptor.gameObject;
            var mbList = root.GetComponentsInChildren<MonoBehaviour>(true);
            int assigned = 0;

            Transform FindBestArmature()
            {
                var t = descriptor.transform.Find("Armature");
                if (t != null) return t;
                var all = descriptor.GetComponentsInChildren<Transform>(true);
                var byName = all.FirstOrDefault(x => x.name.Equals("Armature", StringComparison.OrdinalIgnoreCase));
                if (byName != null) return byName;
                var anim = descriptor.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                    if (hips != null) return hips.root;
                }
                return descriptor.transform;
            }

            var target = FindBestArmature();

            foreach (var mb in mbList)
            {
                if (mb == null) continue;
                var t = mb.GetType();
                var ns = t.Namespace ?? string.Empty;
                if (!(t.Name.Equals("ModularAvatarMergeArmature", StringComparison.Ordinal) && ns.ToLowerInvariant().Contains("modular_avatar")))
                    continue;

                var so = new SerializedObject(mb);
                var mergeTarget = so.FindProperty("mergeTarget") ?? so.FindProperty("target");
                if (mergeTarget == null) continue;

                // 既に参照があるならスキップ
                bool hasReference = false;
                var iter = mergeTarget.Copy();
                var end = iter.GetEndProperty();
                while (iter.NextVisible(true) && !SerializedProperty.EqualContents(iter, end))
                {
                    if (iter.propertyType == SerializedPropertyType.ObjectReference && iter.objectReferenceValue != null)
                    {
                        hasReference = true; break;
                    }
                }
                if (hasReference) continue;

                // 中の最初のObjectReferenceにArmature Transformを設定
                bool wrote = false;
                var p = mergeTarget.Copy();
                var pEnd = p.GetEndProperty();
                while (p.NextVisible(true) && !SerializedProperty.EqualContents(p, pEnd))
                {
                    if (p.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        p.objectReferenceValue = target;
                        wrote = true;
                        break;
                    }
                }

                if (wrote)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(mb);
                    assigned++;
                }
            }

            return assigned;
        }

        private static int DisableInvalidMergeArmatures(GameObject root)
        {
            int count = 0;
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in behaviours)
            {
                if (mb == null) continue;
                var t = mb.GetType();
                var ns = t.Namespace ?? string.Empty;
                if (!t.Name.Equals("ModularAvatarMergeArmature", StringComparison.Ordinal) || !ns.ToLowerInvariant().Contains("modular_avatar")) continue;

                var so = new SerializedObject(mb);
                bool hasTargetField = false;
                bool hasNonNullTarget = false;

                var it = so.GetIterator();
                bool enterChildren = true;
                while (it.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (it.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        var name = it.name.ToLowerInvariant();
                        if (name.Contains("target") || name.Contains("merge"))
                        {
                            hasTargetField = true;
                            if (it.objectReferenceValue != null)
                            {
                                hasNonNullTarget = true;
                                break;
                            }
                        }
                    }
                }

                if (hasTargetField && !hasNonNullTarget)
                {
                    var beh = mb as Behaviour;
                    if (beh != null && beh.enabled)
                    {
                        beh.enabled = false;
                        EditorUtility.SetDirty(beh);
                        count++;
                    }
                }
            }
            return count;
        }

        private static int EnsureFaceEmoLinkage(VRCAvatarDescriptor descriptor)
        {
            int changed = 0;
            if (descriptor == null) return 0;

            // 1) Ensure Expressions assets exist
            var menu = descriptor.expressionsMenu;
            var parameters = descriptor.expressionParameters;

            if (parameters == null)
            {
                // 生成して割当（プロジェクトに保存）
                parameters = ScriptableObject.CreateInstance<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters>();
                parameters.parameters = Array.Empty<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter>();
                var path = "Assets/Generated_Kimera_ExpressionsParameters.asset";
                AssetDatabase.CreateAsset(parameters, AssetDatabase.GenerateUniqueAssetPath(path));
                descriptor.expressionParameters = parameters;
                EditorUtility.SetDirty(descriptor);
                changed++;
                Debug.Log("[Kimera] Expression Parameters を新規作成・割当しました。");
            }

            if (menu == null)
            {
                menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                var path = "Assets/Generated_Kimera_ExpressionsMenu.asset";
                AssetDatabase.CreateAsset(menu, AssetDatabase.GenerateUniqueAssetPath(path));
                descriptor.expressionsMenu = menu;
                EditorUtility.SetDirty(descriptor);
                changed++;
                Debug.Log("[Kimera] Expression Menu を新規作成・割当しました。");
            }

            // 2) Ensure parameters Face_variation (Bool) and FaceLock (Float)
            changed += EnsureParameter(parameters, "Face_variation", VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool);
            changed += EnsureParameter(parameters, "FaceLock", VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Float);

            // 3) Ensure menu controls exist
            if (menu.controls == null) menu.controls = new List<VRCExpressionsMenu.Control>();
            bool hasFaceVar = menu.controls.Any(c => (c != null && c.parameter != null && c.parameter.name == "Face_variation"));
            bool hasFaceLock = menu.controls.Any(c => (c != null && c.parameter != null && c.parameter.name == "FaceLock"));

            if (!hasFaceVar)
            {
                var ctrl = new VRCExpressionsMenu.Control
                {
                    name = "Face Variation",
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = "Face_variation" }
                };
                menu.controls.Insert(0, ctrl);
                EditorUtility.SetDirty(menu);
                changed++;
                Debug.Log("[Kimera] Expression Menu に Face_variation トグルを追加しました。");
            }

            if (!hasFaceLock)
            {
                var ctrl = new VRCExpressionsMenu.Control
                {
                    name = "Face Lock",
                    type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = "FaceLock" }
                };
                menu.controls.Add(ctrl);
                EditorUtility.SetDirty(menu);
                changed++;
                Debug.Log("[Kimera] Expression Menu に FaceLock ラジアルを追加しました。");
            }

            return changed;
        }

        private static int EnsureParameter(VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters parameters, string name, VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType type)
        {
            int changed = 0;
            if (parameters.parameters == null) parameters.parameters = Array.Empty<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter>();

            var list = parameters.parameters.ToList();
            var existing = list.FirstOrDefault(p => p != null && p.name == name);
            if (existing != null)
            {
                if (existing.valueType != type)
                {
                    Debug.LogWarning($"[Kimera] Expression Parameter '{name}' は既に存在しますが型が異なります: {existing.valueType}。変更は行いませんでした。");
                }
                return 0;
            }

            // 容量チェック（Bool=1, Int/Float=4）
            int cost = 0;
            foreach (var p in list)
            {
                if (p == null) continue;
                cost += (p.valueType == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool) ? 1 : 4;
            }
            int add = (type == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool) ? 1 : 4;
            const int MaxCost = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.MAX_PARAMETER_COST; // 256
            if (cost + add > MaxCost)
            {
                Debug.LogWarning($"[Kimera] Expression Parameters の容量が上限({MaxCost})を超えるため '{name}' を追加できませんでした。不要なパラメータを削除してから再実行してください。");
                return 0;
            }

            var param = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter
            {
                name = name,
                valueType = type,
                saved = true,
                defaultValue = 0f
            };
            list.Add(param);
            parameters.parameters = list.ToArray();
            EditorUtility.SetDirty(parameters);
            changed++;
            Debug.Log($"[Kimera] Expression Parameter を追加: {name} ({type})");
            return changed;
        }
    }

    public class KimeraOneClickWindow : EditorWindow
    {
        [MenuItem("Tools/Kimera/One-Click Fix...")]
        public static void Open()
        {
            var wnd = GetWindow<KimeraOneClickWindow>(true, "Kimera One-Click Fix");
            wnd.minSize = new Vector2(360, 140);
        }

        private void OnGUI()
        {
            var selected = Selection.activeGameObject;
            var descriptor = selected != null ? selected.GetComponentInParent<VRCAvatarDescriptor>() : null;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("選択中のアバター", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("GameObject", selected != null ? selected.name : "(未選択)");
            EditorGUILayout.LabelField("Descriptor", descriptor != null ? descriptor.gameObject.name : "(未検出)");

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(descriptor == null))
            {
                if (GUILayout.Button("One-Click Fix (Expressions & LipSync)", GUILayout.Height(30)))
                {
                    if (selected == null)
                    {
                        ShowNotification(new GUIContent("アバターのルートを選択してください"));
                    }
                    else if (descriptor == null)
                    {
                        ShowNotification(new GUIContent("VRCAvatarDescriptorが見つかりません"));
                    }
                    else
                    {
                        KimeraAvatarFixer.RunAutoFixForSelection();
                        ShowNotification(new GUIContent("修正完了。Consoleを確認してください。"));
                    }
                }
            }

            EditorGUILayout.HelpBox("アバターのルート（VRCAvatarDescriptorを持つ）を選択して、ボタンを押すだけで表情参照パスとリップシンク設定を一括修正します。", MessageType.Info);
        }
    }
}
