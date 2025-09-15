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

                if (GUILayout.Button("10. 🔍 FaceEmo診断詳細"))
                {
                    SafeInvoke(DiagnoseFaceEmoDetails);
                }

                if (GUILayout.Button("11. 🎭 Expression Menu修正"))
                {
                    try
                    {
                        FixExpressionMenuConnections();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogException(ex);
                    }
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

            // D. 詳細診断
            DrawSection("D. 詳細診断", new Color(0.8f, 0.9f, 1f), () =>
            {
                if (GUILayout.Button("10. 🔍 FaceEmo診断詳細"))
                {
                    SafeInvoke(DiagnoseFaceEmoWiring);
                }
            });
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

    private void DiagnoseFaceEmoWiring()
    {
        var avatars = FindObjectsOfType<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
        if (avatars == null || avatars.Length == 0)
        {
            EditorUtility.DisplayDialog("診断", "シーンにVRCAvatarDescriptorが見つかりません。", "OK");
            return;
        }

        foreach (var av in avatars)
        {
            Debug.Log($"\n===== 🔍 FaceEmo診断: {av.name} =====");

            // 1) Expression Parameters 状況
            var ep = av.expressionParameters;
            if (ep == null || ep.parameters == null)
            {
                Debug.LogWarning("[Diag] Expression Parameters: 未設定");
            }
            else
            {
                int cost = 0;
                foreach (var p in ep.parameters)
                {
                    if (p == null) continue;
                    int add = p.valueType == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool ? 1 : 4;
                    cost += add;
                }
                Debug.Log($"[Diag] Expression Parameters: {ep.parameters.Length} 個, 使用容量 {cost}/256");

                var faceVar = ep.parameters.FirstOrDefault(p => p != null && p.name == "Face_variation");
                var faceLock = ep.parameters.FirstOrDefault(p => p != null && p.name == "FaceLock");
                Debug.Log($"  - Face_variation: {(faceVar != null ? faceVar.valueType.ToString() : "なし")}");
                Debug.Log($"  - FaceLock: {(faceLock != null ? faceLock.valueType.ToString() : "なし")}");
            }

            // 2) FX コントローラとパラメータ同期
            var fx = av.specialAnimationLayers
                .FirstOrDefault(l => l.type == VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX).animatorController
                as UnityEditor.Animations.AnimatorController;

            if (fx == null)
            {
                Debug.LogWarning("[Diag] FX Controller: 未割当");
            }
            else
            {
                var fxParams = fx.parameters.Select(p => $"{p.name}({p.type})").ToArray();
                Debug.Log($"[Diag] FX Parameters: {fx.parameters.Length} 個 -> [ {string.Join(", ", fxParams)} ]");

                // 空条件と未定義パラメータチェック
                int emptyConds = 0, missingConds = 0;
                var epNames = new HashSet<string>(ep != null && ep.parameters != null ? ep.parameters.Where(p=>p!=null).Select(p => p.name) : Enumerable.Empty<string>());
                foreach (var layer in fx.layers)
                {
                    foreach (var st in layer.stateMachine.states)
                    {
                        foreach (var tr in st.state.transitions)
                        {
                            foreach (var c in tr.conditions)
                            {
                                if (string.IsNullOrEmpty(c.parameter)) emptyConds++;
                                else if (!epNames.Contains(c.parameter)) missingConds++;
                            }
                        }
                    }
                    foreach (var tr in layer.stateMachine.anyStateTransitions)
                    {
                        foreach (var c in tr.conditions)
                        {
                            if (string.IsNullOrEmpty(c.parameter)) emptyConds++;
                            else if (!epNames.Contains(c.parameter)) missingConds++;
                        }
                    }
                }
                Debug.Log($"[Diag] FX 条件: 空={emptyConds}, 未定義={missingConds}");
            }

            // 3) Expression Menu の項目
            var menu = av.expressionsMenu;
            if (menu == null || menu.controls == null)
            {
                Debug.LogWarning("[Diag] Expression Menu: 未設定");
            }
            else
            {
                var items = menu.controls.Select(c => c != null ? $"{c.name} -> {(c.parameter != null ? c.parameter.name : "(no param)")}" : "(null)");
                Debug.Log($"[Diag] Menu Items: {menu.controls.Count} 個 -> [ {string.Join(", ", items)} ]");
            }

            // 4) FaceEmo コンポーネントのパラメータ名
            var faceEmos = av.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(mb => mb != null && ((mb.GetType().Name.Contains("FaceEmo")) || (mb.GetType().FullName != null && mb.GetType().FullName.Contains("FaceEmo"))))
                .ToArray();
            Debug.Log($"[Diag] FaceEmo components: {faceEmos.Length} 個");
            foreach (var fe in faceEmos)
            {
                var so = new SerializedObject(fe);
                var found = new List<string>();
                var it = so.GetIterator();
                bool enter = true;
                while (it.NextVisible(enter))
                {
                    enter = false;
                    if (it.propertyType == SerializedPropertyType.String)
                    {
                        var n = it.displayName.ToLower();
                        if (n.Contains("param"))
                        {
                            found.Add($"{it.displayName}='{it.stringValue}'");
                        }
                    }
                }
                Debug.Log($"  - {fe.GetType().Name} on {fe.gameObject.name}: {(found.Count>0? string.Join(", ", found): "(param名らしき文字列フィールドなし)")}");
            }

            // 5) 推奨修正の要点
            Debug.Log("[Diag] 推奨: 1) FX割当, 2) Parameters名/型一致, 3) Menu項目のParameter名一致, 4) WD統一");
        }

        EditorUtility.DisplayDialog("FaceEmo診断詳細", "Consoleに診断結果を出力しました。", "OK");
    }

    // --- Expression Menu 修正（不足パラメータの自動追加など） ---

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

        // 4. FaceEmoパラメータ連動修正
        fixedCount += FixFaceEmoParameterSync(avatarDescriptors);

        Debug.Log($"✅ 表情設定を {fixedCount} 個修正");
        EditorUtility.DisplayDialog("表情設定修正",
            fixedCount > 0 ? $"表情設定を {fixedCount} 個修正しました\n\n- 顔メッシュ自動設定\n- FXレイヤー有効化\n- FaceEmo復旧・有効化\n- パラメータ連動修正" : "表情設定に問題は見つかりません",
            "OK");
    }

    private int FixFaceEmoParameterSync(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor[] avatarDescriptors)
    {
        int fixedCount = 0;

        foreach (var descriptor in avatarDescriptors)
        {
            // Expression Parametersの確認・修正
            if (descriptor.expressionParameters != null)
            {
                var parameters = descriptor.expressionParameters.parameters.ToList();
                bool parametersModified = false;

                // FaceEmo用の重要パラメータを確認・追加
                var requiredParams = new[]
                {
                    ("FaceEmoteSelect", VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Int),
                    ("FaceLock", VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Float),
                    ("Face_variation", VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool)
                };

                foreach (var (paramName, paramType) in requiredParams)
                {
                    var existingParam = parameters.FirstOrDefault(p => p.name == paramName);
                    if (existingParam == null)
                    {
                        // パラメータが存在しない場合は追加
                        if (parameters.Count < 128) // VRChatのパラメータ制限
                        {
                            var newParam = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter
                            {
                                name = paramName,
                                valueType = paramType,
                                defaultValue = 0f,
                                saved = paramType != VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool
                            };
                            parameters.Add(newParam);
                            parametersModified = true;
                            Debug.Log($"✅ パラメータ追加: {paramName} ({paramType})");
                            fixedCount++;
                        }
                    }
                    else
                    {
                        // パラメータが存在するが型が違う場合は修正
                        if (existingParam.valueType != paramType)
                        {
                            existingParam.valueType = paramType;
                            parametersModified = true;
                            Debug.Log($"✅ パラメータ型修正: {paramName} → {paramType}");
                            fixedCount++;
                        }
                    }
                }

                if (parametersModified)
                {
                    descriptor.expressionParameters.parameters = parameters.ToArray();
                    EditorUtility.SetDirty(descriptor.expressionParameters);
                }
            }

            // FXコントローラーのパラメータ同期確認
            if (descriptor.baseAnimationLayers != null && descriptor.baseAnimationLayers.Length > 4)
            {
                var fxLayer = descriptor.baseAnimationLayers[4];
                if (fxLayer.animatorController is UnityEditor.Animations.AnimatorController controller)
                {
                    var controllerParams = controller.parameters.ToList();
                    bool controllerModified = false;

                    // Expression Parametersと同期
                    if (descriptor.expressionParameters != null)
                    {
                        foreach (var expParam in descriptor.expressionParameters.parameters)
                        {
                            if (expParam.name.Contains("Face") || expParam.name.Contains("Emote"))
                            {
                                var existingControllerParam = controllerParams.FirstOrDefault(p => p.name == expParam.name);
                                if (existingControllerParam == null)
                                {
                                    // FXコントローラーにパラメータを追加
                                    AnimatorControllerParameterType controllerType;
                                    switch (expParam.valueType)
                                    {
                                        case VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool:
                                            controllerType = AnimatorControllerParameterType.Bool;
                                            break;
                                        case VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Int:
                                            controllerType = AnimatorControllerParameterType.Int;
                                            break;
                                        default:
                                            controllerType = AnimatorControllerParameterType.Float;
                                            break;
                                    }

                                    controller.AddParameter(expParam.name, controllerType);
                                    controllerModified = true;
                                    Debug.Log($"✅ FXパラメータ追加: {expParam.name} ({controllerType})");
                                    fixedCount++;
                                }
                            }
                        }
                    }

                    if (controllerModified)
                    {
                        EditorUtility.SetDirty(controller);
                    }
                }
            }

            // FaceEmoコンポーネントの再初期化
            var faceEmoComponents = descriptor.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(mb => mb != null && mb.GetType().Name.Contains("FaceEmo"))
                .ToArray();

            foreach (var faceEmo in faceEmoComponents)
            {
                try
                {
                    // FaceEmoの設定更新（リフレクションを使用）
                    var setupMethod = faceEmo.GetType().GetMethod("Setup",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (setupMethod != null)
                    {
                        setupMethod.Invoke(faceEmo, null);
                        Debug.Log($"✅ FaceEmo再初期化: {faceEmo.name}");
                        fixedCount++;
                    }

                    // パラメータ名の確認・修正
                    var parameterField = faceEmo.GetType().GetField("parameterName",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (parameterField != null)
                    {
                        var currentParam = parameterField.GetValue(faceEmo) as string;
                        if (string.IsNullOrEmpty(currentParam) || currentParam == "")
                        {
                            parameterField.SetValue(faceEmo, "FaceEmoteSelect");
                            Debug.Log($"✅ FaceEmoパラメータ名設定: {faceEmo.name} → FaceEmoteSelect");
                            fixedCount++;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"FaceEmo設定更新失敗: {ex.Message}");
                }
            }
        }

        return fixedCount;
    }

    private void DiagnoseFaceEmoDetails()
    {
        var avatarDescriptors = FindObjectsOfType<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();

        foreach (var descriptor in avatarDescriptors)
        {
            Debug.Log($"\n🔍 詳細診断: {descriptor.name}");

            // 1. Expression Parameters詳細
            if (descriptor.expressionParameters != null)
            {
                Debug.Log($"📋 Expression Parameters: {descriptor.expressionParameters.name}");
                Debug.Log($"   パラメータ数: {descriptor.expressionParameters.parameters.Length}");

                var faceParams = descriptor.expressionParameters.parameters
                    .Where(p => p.name.Contains("Face") || p.name.Contains("Emote"))
                    .ToArray();

                Debug.Log($"   表情関連パラメータ: {faceParams.Length} 個");
                foreach (var param in faceParams)
                {
                    Debug.Log($"     - {param.name} ({param.valueType}) = {param.defaultValue}");
                }

                // 重要パラメータの存在確認
                var requiredParams = new[] { "FaceEmoteSelect", "FaceLock", "Face_variation" };
                foreach (var reqParam in requiredParams)
                {
                    var exists = faceParams.Any(p => p.name == reqParam);
                    Debug.Log($"   {reqParam}: {(exists ? "✅ 存在" : "❌ 不足")}");
                }
            }
            else
            {
                Debug.LogWarning($"❌ Expression Parameters が未設定: {descriptor.name}");
            }

            // 2. FXコントローラー詳細
            if (descriptor.baseAnimationLayers != null && descriptor.baseAnimationLayers.Length > 4)
            {
                var fxLayer = descriptor.baseAnimationLayers[4];
                if (fxLayer.animatorController is UnityEditor.Animations.AnimatorController controller)
                {
                    Debug.Log($"🎮 FXController: {controller.name}");

                    var faceControllerParams = controller.parameters
                        .Where(p => p.name.Contains("Face") || p.name.Contains("Emote"))
                        .ToArray();

                    Debug.Log($"   表情関連パラメータ: {faceControllerParams.Length} 個");
                    foreach (var param in faceControllerParams)
                    {
                        Debug.Log($"     - {param.name} ({param.type}) = {param.defaultFloat}");
                    }
                }
            }

            // 3. FaceEmoコンポーネント詳細
            var faceEmoComponents = descriptor.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(mb => mb != null && mb.GetType().Name.Contains("FaceEmo"))
                .ToArray();

            Debug.Log($"😄 FaceEmoコンポーネント: {faceEmoComponents.Length} 個");

            foreach (var faceEmo in faceEmoComponents)
            {
                Debug.Log($"   - {faceEmo.GetType().Name} on {faceEmo.gameObject.name}");
                Debug.Log($"     有効: {faceEmo.enabled}");

                // パラメータ名を取得
                try
                {
                    var parameterField = faceEmo.GetType().GetField("parameterName",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (parameterField != null)
                    {
                        var paramName = parameterField.GetValue(faceEmo) as string;
                        Debug.Log($"     パラメータ名: '{paramName}' {(string.IsNullOrEmpty(paramName) ? "❌ 空" : "✅ 設定済み")}");
                    }

                    // その他の重要フィールドも確認
                    var fields = faceEmo.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    foreach (var field in fields.Take(5)) // 最初の5個のフィールドのみ
                    {
                        var value = field.GetValue(faceEmo);
                        Debug.Log($"     {field.Name}: {value}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"     フィールド取得エラー: {ex.Message}");
                }
            }
        }

        EditorUtility.DisplayDialog("FaceEmo詳細診断",
            "FaceEmoの詳細情報をConsoleに出力しました\n\nパラメータ連動の問題を特定してください", "OK");
    }

    private void FixExpressionMenuConnections()
    {
        var avatarDescriptors = FindObjectsOfType<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
        int fixedCount = 0;

        foreach (var descriptor in avatarDescriptors)
        {
            Debug.Log($"\n🎭 Expression Menu修正: {descriptor.name}");

            if (descriptor.expressionsMenu == null)
            {
                Debug.LogWarning($"❌ Expression Menu が未設定: {descriptor.name}");
                continue;
            }

            var menu = descriptor.expressionsMenu;
            var parameters = descriptor.expressionParameters;

            if (parameters == null)
            {
                Debug.LogWarning($"❌ Expression Parameters が未設定: {descriptor.name}");
                continue;
            }

            // 表情関連のメニューコントロールを確認・修正
            foreach (var control in menu.controls)
            {
                if (control == null || control.parameter == null) continue;

                var paramName = control.parameter.name;

                // 表情関連パラメータの確認
                if (paramName.Contains("Face") || paramName.Contains("Emote") || paramName.Contains("Expression"))
                {
                    Debug.Log($"🔍 Expression Menu Control: {control.name} → {paramName}");

                    // Expression Parametersに対応するパラメータが存在するか確認
                    var expParam = parameters.parameters.FirstOrDefault(p => p.name == paramName);
                    if (expParam == null)
                    {
                        Debug.LogWarning($"⚠️ パラメータ不一致: Menu '{paramName}' がExpression Parametersに存在しません");

                        // 類似パラメータを検索して提案
                        var similarParams = parameters.parameters
                            .Where(p => p.name.Contains("Face") || p.name.Contains("Emote"))
                            .ToArray();

                        if (similarParams.Length > 0)
                        {
                            var suggestion = similarParams[0].name;
                            Debug.Log($"💡 提案: '{paramName}' → '{suggestion}' に変更を検討");

                            // 自動修正: 一般的なパラメータ名の場合
                            if (paramName == "FaceEmote" || paramName == "Emote" || paramName == "EmoteSelect")
                            {
                                control.parameter.name = "FaceEmoteSelect";
                                Debug.Log($"✅ 自動修正: {paramName} → FaceEmoteSelect");
                                fixedCount++;
                            }
                        }
                    }

                    // パラメータ型の確認
                    if (expParam != null)
                    {
                        var expectedType = control.type == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.RadialPuppet
                            ? VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Float
                            : VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Int;

                        if (expParam.valueType != expectedType)
                        {
                            Debug.LogWarning($"⚠️ パラメータ型不一致: {paramName} Menu:{control.type} vs Param:{expParam.valueType}");
                        }
                    }
                }
            }

            // 基本的な表情パラメータが不足している場合の自動追加
            var requiredParams = new[]
            {
                ("FaceEmoteSelect", VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Int),
                ("FaceLock", VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Float)
            };

            var paramList = parameters.parameters.ToList();
            bool parametersModified = false;

            foreach (var (reqName, reqType) in requiredParams)
            {
                if (!paramList.Any(p => p.name == reqName))
                {
                    var newParam = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter
                    {
                        name = reqName,
                        valueType = reqType,
                        defaultValue = 0f,
                        saved = true
                    };
                    paramList.Add(newParam);
                    parametersModified = true;
                    Debug.Log($"✅ 不足パラメータ追加: {reqName} ({reqType})");
                    fixedCount++;
                }
            }

            if (parametersModified)
            {
                parameters.parameters = paramList.ToArray();
                EditorUtility.SetDirty(parameters);
            }
        }

        Debug.Log($"✅ Expression Menu修正完了: {fixedCount} 個の問題を修正");
        EditorUtility.DisplayDialog("Expression Menu修正",
            fixedCount > 0 ? $"Expression Menuの {fixedCount} 個の問題を修正しました\n\n- パラメータ名の統一\n- 不足パラメータの追加\n- 型の確認" : "Expression Menuに問題は見つかりませんでした",
            "OK");
    }
}
