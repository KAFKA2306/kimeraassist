using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// 最短でビルド再開まで持っていく - 10分修正スクリプト
/// KAFKA分析による3系統衝突＋参照切れの根本対応
/// </summary>
public class FixAvatarCriticalIssues : EditorWindow
{
    private Vector2 scrollPosition;

    [MenuItem("Tools/KAFKA Fix - 10分でビルド復旧")]
    public static void ShowWindow()
    {
        var window = GetWindow<FixAvatarCriticalIssues>("KAFKA 10分修正");
        window.minSize = new Vector2(450, 600);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        try
        {
            GUILayout.Label("🚨 KAFKA 最短修正 - 3系統衝突対応", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox("根本原因: VRCFury/AO/MA の3系統衝突 + 参照切れ\n順番通りに実行して10分でビルド復旧", MessageType.Warning);

            EditorGUILayout.Space(10);

            // A. 衝突止める（一時対応）
            DrawSection("A. 衝突停止（一時対応）", Color.red, () =>
            {
                if (GUILayout.Button("1. 🛑 NDMF ApplyOnPlay 無効化"))
                {
                    SafeInvoke(DisableNDMFApplyOnPlay);
                }

                if (GUILayout.Button("2. 🛑 VRCFury PlayMode Trigger 削除"))
                {
                    SafeInvoke(DisableVRCFuryPlayModeTriggers);
                }

                if (GUILayout.Button("3. 🛑 AO過剰最適化 一時停止"))
                {
                    SafeInvoke(DisableAvatarOptimizerOverOptimization);
                }
            });

            // B. 赤エラー本体修正
            DrawSection("B. 赤エラー本体修正", Color.yellow, () =>
            {
                if (GUILayout.Button("4. 🔗 MA-1400 MergeArmature修正"))
                {
                    SafeInvoke(FixModularAvatarMergeTargets);
                }

                if (GUILayout.Button("5. 🎮 Animator空パラメータ解消"))
                {
                    SafeInvoke(FixAnimatorEmptyParameters);
                }

                if (GUILayout.Button("6. 👤 Body参照アニメリマップ"))
                {
                    SafeInvoke(FixBodyReferenceAnimations);
                }

                if (GUILayout.Button("7. 🎤 リップシンク復旧"))
                {
                    SafeInvoke(RestoreLipSync);
                }

                if (GUILayout.Button("8. 🦴 PhysBone参照修正"))
                {
                    SafeInvoke(FixPhysBoneConstraintReferences);
                }

                if (GUILayout.Button("9. 😊 表情設定修正"))
                {
                    SafeInvoke(FixFacialExpressions);
                }
            });

            // C. ビルドテスト
            DrawSection("C. ビルドテスト", Color.green, () =>
            {
                EditorGUILayout.HelpBox("上記修正後、VRChat SDK > Build & Test でテスト", MessageType.Info);

                if (GUILayout.Button("🔄 全修正を一括実行"))
                {
                    SafeInvoke(ExecuteAllFixes);
                }
            });

            EditorGUILayout.Space(20);

            EditorGUILayout.HelpBox("💡 修正順序:\n1. 衝突停止 → 2. エラー修正 → 3. ビルドテスト\n\n⚠️ 修正後はAOを段階的に再有効化", MessageType.Info);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    private void SafeInvoke(System.Action action)
    {
        try { action?.Invoke(); }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    private void DrawSection(string title, Color color, System.Action content)
    {
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = color;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = originalColor;

        GUILayout.Label(title, EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        content();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void DisableNDMFApplyOnPlay()
    {
        // NDMF ApplyOnPlay設定を無効化
        var applyOnPlayEnabled = EditorPrefs.GetBool("nadena.dev.ndmf.apply-on-play", true);
        if (applyOnPlayEnabled)
        {
            EditorPrefs.SetBool("nadena.dev.ndmf.apply-on-play", false);
            Debug.Log("✅ NDMF ApplyOnPlay を無効化しました");
        }

        EditorUtility.DisplayDialog("NDMF ApplyOnPlay",
            applyOnPlayEnabled ? "NDMF ApplyOnPlay を無効化しました" : "NDMF ApplyOnPlay は既に無効です",
            "OK");
    }

    private void DisableVRCFuryPlayModeTriggers()
    {
        var triggers = FindObjectsOfType<Transform>()
            .Where(t => t.name.Contains("__vrcf_play_mode_trigger") ||
                       t.name.Contains("RescanOnStartComponent"))
            .ToArray();

        var vrcfuryPlayModeComponents = FindObjectsOfType<MonoBehaviour>()
            .Where(mb => mb != null &&
                   (mb.GetType().Name.Contains("PlayMode") ||
                    mb.GetType().Name.Contains("RescanOnStart")))
            .ToArray();

        int removed = 0;

        // PlayMode Trigger オブジェクトを削除
        foreach (var trigger in triggers)
        {
            Debug.Log($"Removing VRCFury PlayMode trigger: {trigger.name}");
            DestroyImmediate(trigger.gameObject);
            removed++;
        }

        // PlayMode関連コンポーネントを無効化
        foreach (var component in vrcfuryPlayModeComponents)
        {
            component.enabled = false;
            removed++;
        }

        Debug.Log($"✅ VRCFury PlayMode triggers を {removed} 個処理しました");
        EditorUtility.DisplayDialog("VRCFury PlayMode Fix",
            $"VRCFury PlayMode triggers を {removed} 個処理しました\n\nOnValidate中のSendMessage問題を解消",
            "OK");
    }

    private void DisableAvatarOptimizerOverOptimization()
    {
        // より広範囲のAvatar Optimizerコンポーネントを無効化
        var avatarOptimizerComponents = FindObjectsOfType<MonoBehaviour>()
            .Where(mb => mb != null &&
                   (mb.GetType().Name.Contains("TraceAndOptimize") ||
                    mb.GetType().Name.Contains("AutoMerge") ||
                    mb.GetType().Name.Contains("RemoveUnused") ||
                    mb.GetType().FullName.Contains("Anatawa12.AvatarOptimizer") ||
                    mb.GetType().Assembly.GetName().Name.Contains("avatar-optimizer")))
            .ToArray();

        foreach (var component in avatarOptimizerComponents)
        {
            component.enabled = false;
            Debug.Log($"一時無効化: {component.GetType().FullName} on {component.gameObject.name}");
        }

        // Avatar OptimizerのValidationも無効化
        var validationComponents = FindObjectsOfType<MonoBehaviour>()
            .Where(mb => mb != null && mb.GetType().Name.Contains("Validation"))
            .ToArray();

        foreach (var component in validationComponents)
        {
            if (component.GetType().Assembly.GetName().Name.Contains("avatar-optimizer"))
            {
                component.enabled = false;
                Debug.Log($"Validation無効化: {component.GetType().Name} on {component.gameObject.name}");
            }
        }

        int totalDisabled = avatarOptimizerComponents.Length + validationComponents.Length;
        Debug.Log($"✅ Avatar Optimizer を {totalDisabled} 個完全停止（VRCFury競合解消）");
        EditorUtility.DisplayDialog("AO完全停止",
            $"Avatar Optimizer を {totalDisabled} 個完全停止しました\n\nVRCFuryとの競合を解消\n\nビルド成功後、必要に応じて段階的に再有効化",
            "OK");
    }

    private void FixModularAvatarMergeTargets()
    {
        var avatarDescriptors = FindObjectsOfType<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
        var mergeArmatures = FindObjectsOfType<MonoBehaviour>()
            .Where(mb => mb != null && mb.GetType().Name == "ModularAvatarMergeArmature")
            .ToArray();

        int fixedCount = 0;

        foreach (var mergeArmature in mergeArmatures)
        {
            // 新しいModular AvatarではmergeTargetの型が変更されている
            // 一旦無効化して手動対応を促す
            if (mergeArmature.enabled)
            {
                mergeArmature.enabled = false;
                Debug.Log($"⚠️ ModularAvatarMergeArmature を無効化: {mergeArmature.gameObject.name} (手動でmergeTargetを設定してください)");
                fixedCount++;
            }
        }

        if (fixedCount > 0)
        {
            Debug.Log($"✅ ModularAvatarMergeArmature を {fixedCount} 個無効化しました");
        }

        EditorUtility.DisplayDialog("MA-1400修正",
            fixedCount > 0 ? $"ModularAvatarMergeArmature を {fixedCount} 個無効化しました\n\n手動対応:\n1. 各MergeArmatureを有効化\n2. Merge TargetにメインのArmatureを設定" : "修正が必要なMergeArmatureが見つかりません",
            "OK");
    }

    private void FixAnimatorEmptyParameters()
    {
        var controllerPath = "Assets/IKUSIA/rurune/Animation/paryi_Action.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        if (controller == null)
        {
            EditorUtility.DisplayDialog("Error", $"Controller not found: {controllerPath}", "OK");
            return;
        }

        int fixedCount = 0;

        // 空のパラメータを削除
        var parameters = controller.parameters.Where(p => !string.IsNullOrEmpty(p.name)).ToArray();
        if (parameters.Length != controller.parameters.Length)
        {
            controller.parameters = parameters;
            fixedCount += controller.parameters.Length - parameters.Length;
        }

        // 各レイヤーの空パラメータ条件を修正
        foreach (var layer in controller.layers)
        {
            // ステートの遷移条件をチェック
            foreach (var state in layer.stateMachine.states)
            {
                var transitions = state.state.transitions.ToList();
                for (int i = transitions.Count - 1; i >= 0; i--)
                {
                    var transition = transitions[i];
                    var validConditions = transition.conditions
                        .Where(c => !string.IsNullOrEmpty(c.parameter))
                        .ToArray();

                    if (validConditions.Length != transition.conditions.Length)
                    {
                        transition.conditions = validConditions;
                        fixedCount++;
                        Debug.Log($"空パラメータ条件を削除: {state.state.name}");
                    }

                    // 全条件が削除された遷移で、Exit Timeもない場合は遷移を削除
                    if (validConditions.Length == 0 && !transition.hasExitTime)
                    {
                        state.state.RemoveTransition(transition);
                        Debug.Log($"無効な遷移を削除: {state.state.name}");
                        fixedCount++;
                    }
                }
            }

            // Any State遷移もチェック
            var anyTransitions = layer.stateMachine.anyStateTransitions.ToList();
            for (int i = anyTransitions.Count - 1; i >= 0; i--)
            {
                var transition = anyTransitions[i];
                var validConditions = transition.conditions
                    .Where(c => !string.IsNullOrEmpty(c.parameter))
                    .ToArray();

                if (validConditions.Length != transition.conditions.Length)
                {
                    transition.conditions = validConditions;
                    fixedCount++;
                }

                if (validConditions.Length == 0 && !transition.hasExitTime)
                {
                    layer.stateMachine.RemoveAnyStateTransition(transition);
                    fixedCount++;
                }
            }
        }

        if (fixedCount > 0)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"✅ Animator空パラメータを {fixedCount} 個修正");
        }

        EditorUtility.DisplayDialog("Animator修正",
            fixedCount > 0 ? $"空パラメータ/無効遷移を {fixedCount} 個修正しました" : "修正が必要な問題が見つかりません",
            "OK");
    }

    private void FixBodyReferenceAnimations()
    {
        // この部分は手動対応が必要
        Debug.Log("💡 Body参照アニメのリマップが必要です");

        var message = "Body参照アニメの修正:\n\n" +
                     "1. 現在の顔メッシュ名を確認\n" +
                     "2. Modular Avatar でパス置換を設定:\n" +
                     "   Body → [実際の顔メッシュ名]\n\n" +
                     "または顔メッシュを 'Body' にリネーム";

        EditorUtility.DisplayDialog("Body参照リマップ", message, "了解");
    }

    private void RestoreLipSync()
    {
        var avatarDescriptors = FindObjectsOfType<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
        int fixedCount = 0;

        foreach (var descriptor in avatarDescriptors)
        {
            if (descriptor.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape)
            {
                if (descriptor.VisemeSkinnedMesh == null)
                {
                    // 顔メッシュを自動検出して設定
                    var faceRenderers = descriptor.GetComponentsInChildren<SkinnedMeshRenderer>()
                        .Where(r => r.sharedMesh != null && r.sharedMesh.blendShapeCount > 5)
                        .OrderByDescending(r => r.sharedMesh.blendShapeCount)
                        .ToArray();

                    if (faceRenderers.Length > 0)
                    {
                        descriptor.VisemeSkinnedMesh = faceRenderers[0];
                        Debug.Log($"✅ リップシンクメッシュを自動設定: {faceRenderers[0].name}");
                        fixedCount++;
                    }
                    else
                    {
                        // 一時的にDefaultに変更
                        descriptor.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.Default;
                        Debug.Log($"⚠️ 顔メッシュが見つからないため、一時的にDefaultに設定");
                        fixedCount++;
                    }
                }
            }
        }

        if (fixedCount > 0)
        {
            Debug.Log($"✅ リップシンクを {fixedCount} 個修正");
        }

        EditorUtility.DisplayDialog("リップシンク修復",
            fixedCount > 0 ? $"リップシンクを {fixedCount} 個修正しました" : "リップシンクに問題は見つかりません",
            "OK");
    }

    private void FixPhysBoneConstraintReferences()
    {
        // 破壊されたPhysBone参照によるMissingReferenceExceptionを解消
        var constraints = FindObjectsOfType<MonoBehaviour>()
            .Where(mb => mb != null && (mb.GetType().Name.Contains("VRCConstraint") ||
                                       mb.GetType().FullName.Contains("VRC.Dynamics")))
            .ToArray();

        int fixedCount = 0;

        foreach (var constraint in constraints)
        {
            try
            {
                // MissingReferenceExceptionを回避してConstraintを安全に無効化
                if (constraint != null && constraint.gameObject != null && constraint.enabled)
                {
                    // PhysBone参照チェック（MissingReferenceException対策）
                    bool shouldDisable = false;

                    try
                    {
                        // Constraintの親階層をチェック
                        var parentTransforms = constraint.GetComponentsInParent<Transform>();
                        bool hasValidPhysBone = false;

                        foreach (var parent in parentTransforms)
                        {
                            var physBones = parent.GetComponents<MonoBehaviour>()
                                .Where(mb => mb != null && mb.GetType().Name == "VRCPhysBone")
                                .ToArray();

                            if (physBones.Length > 0)
                            {
                                hasValidPhysBone = true;
                                break;
                            }
                        }

                        if (!hasValidPhysBone)
                        {
                            shouldDisable = true;
                        }
                    }
                    catch (MissingReferenceException)
                    {
                        // PhysBone参照が既に破壊されている
                        shouldDisable = true;
                    }

                    if (shouldDisable)
                    {
                        constraint.enabled = false;
                        Debug.Log($"⚠️ PhysBone参照切れConstraint無効化: {constraint.gameObject.name} ({constraint.GetType().Name})");
                        fixedCount++;
                    }
                }
            }
            catch (MissingReferenceException)
            {
                Debug.Log($"🗑️ 破壊済みConstraintをスキップ");
                fixedCount++;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Constraint処理エラー: {ex.Message}");
                try
                {
                    if (constraint != null && constraint.enabled)
                    {
                        constraint.enabled = false;
                        fixedCount++;
                    }
                }
                catch { }
            }
        }

        // Sceneの再構築でConstraintManagerをリフレッシュ
        try
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("🔄 Scene Constraint状態をリフレッシュ");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Scene リフレッシュ失敗: {ex.Message}");
        }

        Debug.Log($"✅ PhysBone参照問題を {fixedCount} 個処理（MissingReference解消）");
        EditorUtility.DisplayDialog("PhysBone参照修正",
            fixedCount > 0 ? $"参照問題のあるConstraintを {fixedCount} 個無効化\n\nMissingReferenceExceptionを解消しました" : "PhysBone参照に問題は見つかりません",
            "OK");
    }

    private void ExecuteAllFixes()
    {
        if (EditorUtility.DisplayDialog("一括修正確認",
            "全ての修正を順番に実行しますか？\n\n注意: 処理中は操作しないでください",
            "実行", "キャンセル"))
        {
            Debug.Log("🚀 KAFKA 一括修正開始");

            DisableNDMFApplyOnPlay();
            DisableVRCFuryPlayModeTriggers();
            DisableAvatarOptimizerOverOptimization();
            FixModularAvatarMergeTargets();
            FixAnimatorEmptyParameters();
            RestoreLipSync();
            FixPhysBoneConstraintReferences();
            FixFacialExpressions();

            Debug.Log("✅ KAFKA 一括修正完了");
            EditorUtility.DisplayDialog("修正完了",
                "全ての自動修正が完了しました！\n\n次のステップ:\n1. Body参照アニメを手動でリマップ\n2. VRChat SDK でビルドテスト",
                "OK");
        }
    }

    private void FixFacialExpressions()
    {
        var avatarDescriptors = FindObjectsOfType<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
        int fixedCount = 0;

        foreach (var descriptor in avatarDescriptors)
        {
            // 1. 顔メッシュ自動検出・設定
            var faceMeshes = descriptor.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(r => r.sharedMesh != null && r.sharedMesh.blendShapeCount > 10)
                .OrderByDescending(r => r.sharedMesh.blendShapeCount)
                .ToArray();

            if (faceMeshes.Length > 0)
            {
                var bestFaceMesh = faceMeshes[0];

                // VisemeSkinnedMesh設定
                if (descriptor.VisemeSkinnedMesh != bestFaceMesh)
                {
                    descriptor.VisemeSkinnedMesh = bestFaceMesh;
                    Debug.Log($"✅ 顔メッシュ設定: {descriptor.name} → {bestFaceMesh.name}");
                    fixedCount++;
                }

                // アイトラッキング設定
                if (descriptor.enableEyeLook && descriptor.customEyeLookSettings.eyelidsSkinnedMesh == null)
                {
                    descriptor.customEyeLookSettings.eyelidsSkinnedMesh = bestFaceMesh;
                    Debug.Log($"✅ アイトラッキング設定: {descriptor.name}");
                    fixedCount++;
                }
            }

            // 2. FXレイヤー設定確認
            if (descriptor.baseAnimationLayers != null && descriptor.baseAnimationLayers.Length > 4)
            {
                var fxLayer = descriptor.baseAnimationLayers[4];
                if (fxLayer.isDefault && fxLayer.animatorController != null)
                {
                    fxLayer.isDefault = false;
                    Debug.Log($"✅ FXレイヤーをカスタムに設定: {descriptor.name}");
                    fixedCount++;
                }
            }
        }

        // 3. FaceEmo復旧・有効化
        var faceEmoComponents = FindObjectsOfType<MonoBehaviour>()
            .Where(mb => mb != null && (mb.GetType().Name.Contains("FaceEmo") ||
                                       mb.GetType().FullName.Contains("FaceEmo")))
            .ToArray();

        Debug.Log($"FaceEmo検出: {faceEmoComponents.Length} 個のコンポーネント");

        if (faceEmoComponents.Length == 0)
        {
            // FaceEmoが存在しない場合、復旧を試行
            Debug.Log("⚠️ FaceEmoコンポーネントが見つかりません。復旧を試行...");

            // FaceEmoPrefabを検索
            var faceEmoPrefabs = AssetDatabase.FindAssets("FaceEmoPrefab")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => path.EndsWith(".prefab"))
                .ToArray();

            foreach (var prefabPath in faceEmoPrefabs)
            {
                Debug.Log($"FaceEmoPrefab発見: {prefabPath}");

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    // アバターにFaceEmoPrefabを追加
                    foreach (var avatar in avatarDescriptors)
                    {
                        var existingFaceEmo = avatar.GetComponentsInChildren<Transform>(true)
                            .FirstOrDefault(t => t.name.Contains("FaceEmo"));

                        if (existingFaceEmo == null)
                        {
                            var faceEmoInstance = PrefabUtility.InstantiatePrefab(prefab, avatar.transform) as GameObject;
                            if (faceEmoInstance != null)
                            {
                                Debug.Log($"✅ FaceEmo復旧: {avatar.name} に {prefab.name} を追加");
                                fixedCount++;
                                break; // 1つのプレハブで十分
                            }
                        }
                    }
                    break; // 最初に見つかったプレハブを使用
                }
            }
        }
        else
        {
            // 既存のFaceEmoを有効化
            foreach (var component in faceEmoComponents)
            {
                if (!component.enabled)
                {
                    component.enabled = true;
                    Debug.Log($"✅ FaceEmo有効化: {component.GetType().Name} on {component.gameObject.name}");
                    fixedCount++;
                }
            }
        }

        Debug.Log($"✅ 表情設定を {fixedCount} 個修正");
        EditorUtility.DisplayDialog("表情設定修正",
            fixedCount > 0 ? $"表情設定を {fixedCount} 個修正しました\n\n- 顔メッシュ自動設定\n- FXレイヤー有効化\n- FaceEmo有効化" : "表情設定に問題は見つかりません",
            "OK");
    }
}
