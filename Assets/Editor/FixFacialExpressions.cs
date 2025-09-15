using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.Animations;
using System.Reflection;

#if VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

/// <summary>
/// 表情が変化しない問題を修正 - VRChat表情システム設定
/// </summary>
public class FixFacialExpressions : EditorWindow
{
    private Vector2 scrollPosition;

    [MenuItem("Tools/表情修正 - Fix Facial Expressions")]
    public static void ShowWindow()
    {
        var window = GetWindow<FixFacialExpressions>("表情修正");
        window.minSize = new Vector2(400, 500);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("😊 表情が変化しない問題の修正", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox("VRChatアバターの表情設定を確認・修正します", MessageType.Info);

        EditorGUILayout.Space(10);

        // 1. アバター検出
        DrawSection("1. アバター検出", () =>
        {
            if (GUILayout.Button("🔍 アバターを検出"))
            {
                FindAvatars();
            }
        });

        // 2. 表情メッシュ設定
        DrawSection("2. 表情メッシュ設定", () =>
        {
            EditorGUILayout.HelpBox("顔メッシュのBlendShapeを確認・設定", MessageType.Warning);

            if (GUILayout.Button("👤 顔メッシュ自動設定"))
            {
                SetupFaceMesh();
            }

            if (GUILayout.Button("🎭 BlendShape確認"))
            {
                CheckBlendShapes();
            }
        });

        // 3. FXController設定
        DrawSection("3. FXアニメーター設定", () =>
        {
            EditorGUILayout.HelpBox("表情アニメーションの設定確認", MessageType.Warning);

            if (GUILayout.Button("🎮 FXController確認"))
            {
                CheckFXController();
            }

            if (GUILayout.Button("📝 Expression Menu確認"))
            {
                CheckExpressionMenu();
            }
        });

        // 4. FaceEmo設定
        DrawSection("4. FaceEmo設定", () =>
        {
            EditorGUILayout.HelpBox("FaceEmoコンポーネントの設定確認", MessageType.Warning);

            if (GUILayout.Button("😄 FaceEmo設定確認"))
            {
                CheckFaceEmoSettings();
            }

            if (GUILayout.Button("🔧 FaceEmo修正"))
            {
                FixFaceEmoSettings();
            }
        });

        // 5. 一括修正
        DrawSection("5. 一括修正", () =>
        {
            EditorGUILayout.HelpBox("全ての表情設定を一括で修正します", MessageType.Info);

            if (GUILayout.Button("✨ 表情設定を一括修正"))
            {
                FixAllFacialSettings();
            }
        });

        EditorGUILayout.Space(20);
        EditorGUILayout.HelpBox("💡 修正順序:\n1. アバター検出 → 2. 顔メッシュ設定 → 3. FXController → 4. FaceEmo → 5. 一括修正", MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private void DrawSection(string title, System.Action content)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label(title, EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        content();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void FindAvatars()
    {
#if VRC_SDK_VRCSDK3
        var avatarDescriptors = FindObjectsOfType<VRCAvatarDescriptor>();
#else
        var avatarDescriptors = new MonoBehaviour[0];
        Debug.LogWarning("VRChat SDK not found");
#endif

        Debug.Log($"🔍 アバター検出結果: {avatarDescriptors.Length} 個のアバターを発見");

        foreach (var descriptor in avatarDescriptors)
        {
            Debug.Log($"📋 アバター: {descriptor.gameObject.name}");
            Debug.Log($"   - リップシンク: {descriptor.lipSync}");
            Debug.Log($"   - 顔メッシュ: {(descriptor.VisemeSkinnedMesh ? descriptor.VisemeSkinnedMesh.name : "未設定")}");

            // FXレイヤー確認
            if (descriptor.baseAnimationLayers != null && descriptor.baseAnimationLayers.Length > 4)
            {
                var fxLayer = descriptor.baseAnimationLayers[4]; // FXレイヤー
                Debug.Log($"   - FXController: {(fxLayer.animatorController ? fxLayer.animatorController.name : "未設定")}");
            }
        }

        EditorUtility.DisplayDialog("アバター検出",
            $"{avatarDescriptors.Length} 個のアバターを検出しました\n詳細はConsoleを確認してください", "OK");
    }

    private void SetupFaceMesh()
    {
            var avatarDescriptors = FindObjectsOfType<VRCAvatarDescriptor>();
            int fixedCount = 0;

            foreach (var descriptor in avatarDescriptors)
            {
                EnsureExpressionAssets(descriptor);
            // 顔メッシュを自動検出
            var faceMeshes = descriptor.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(r => r.sharedMesh != null && r.sharedMesh.blendShapeCount > 10)
                .OrderByDescending(r => r.sharedMesh.blendShapeCount)
                .ToArray();

            if (faceMeshes.Length > 0)
            {
                var bestFaceMesh = faceMeshes[0];

                // リップシンク設定
                if (descriptor.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape)
                {
                    if (descriptor.VisemeSkinnedMesh != bestFaceMesh)
                    {
                        descriptor.VisemeSkinnedMesh = bestFaceMesh;
                        Debug.Log($"✅ 顔メッシュ設定: {descriptor.name} → {bestFaceMesh.name}");
                        fixedCount++;
                    }
                }
                else
                {
                    // リップシンクをBlendShapeに変更
                    descriptor.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
                    descriptor.VisemeSkinnedMesh = bestFaceMesh;
                    Debug.Log($"✅ リップシンク有効化: {descriptor.name} → {bestFaceMesh.name}");
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
        }

        EditorUtility.DisplayDialog("顔メッシュ設定",
            fixedCount > 0 ? $"{fixedCount} 個の設定を修正しました" : "修正が必要な問題は見つかりませんでした", "OK");
    }

    private void CheckBlendShapes()
    {
        var avatarDescriptors = FindObjectsOfType<VRCAvatarDescriptor>();

        foreach (var descriptor in avatarDescriptors)
        {
            Debug.Log($"\n📊 BlendShape確認: {descriptor.name}");

            var faceMeshes = descriptor.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(r => r.sharedMesh != null && r.sharedMesh.blendShapeCount > 0)
                .ToArray();

            foreach (var mesh in faceMeshes)
            {
                Debug.Log($"  メッシュ: {mesh.name} ({mesh.sharedMesh.blendShapeCount} BlendShapes)");

                // 重要な表情BlendShapeをチェック
                var importantShapes = new[] { "Blink", "Joy", "Angry", "Sorrow", "Surprised", "vrc.blink_left", "vrc.blink_right" };

                for (int i = 0; i < mesh.sharedMesh.blendShapeCount; i++)
                {
                    var shapeName = mesh.sharedMesh.GetBlendShapeName(i);
                    if (importantShapes.Any(important => shapeName.ToLower().Contains(important.ToLower())))
                    {
                        Debug.Log($"    ✅ 重要BlendShape: {shapeName}");
                    }
                }
            }
        }

        EditorUtility.DisplayDialog("BlendShape確認", "BlendShape情報をConsoleに出力しました", "OK");
    }

    private void CheckFXController()
    {
        var avatarDescriptors = FindObjectsOfType<VRCAvatarDescriptor>();

        foreach (var descriptor in avatarDescriptors)
        {
            Debug.Log($"\n🎮 FXController確認: {descriptor.name}");

            if (descriptor.baseAnimationLayers != null && descriptor.baseAnimationLayers.Length > 4)
            {
                var fxLayer = descriptor.baseAnimationLayers[4];

                if (fxLayer.animatorController != null)
                {
                    Debug.Log($"  ✅ FXController: {fxLayer.animatorController.name}");

                    // パラメータ確認
                    var controller = fxLayer.animatorController as UnityEditor.Animations.AnimatorController;
                    if (controller != null)
                    {
                        var faceParams = controller.parameters
                            .Where(p => p.name.ToLower().Contains("face") ||
                                       p.name.ToLower().Contains("emotion") ||
                                       p.name.ToLower().Contains("expression"))
                            .ToArray();

                        Debug.Log($"  表情パラメータ: {faceParams.Length} 個");
                        foreach (var param in faceParams)
                        {
                            Debug.Log($"    - {param.name} ({param.type})");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"  ⚠️ FXControllerが未設定: {descriptor.name}");
                }
            }
        }

        EditorUtility.DisplayDialog("FXController確認", "FXController情報をConsoleに出力しました", "OK");
    }

    private void CheckExpressionMenu()
    {
        var avatarDescriptors = FindObjectsOfType<VRCAvatarDescriptor>();

        foreach (var descriptor in avatarDescriptors)
        {
            Debug.Log($"\n📝 Expression Menu確認: {descriptor.name}");

            if (descriptor.expressionsMenu != null)
            {
                Debug.Log($"  ✅ Expression Menu: {descriptor.expressionsMenu.name}");
                Debug.Log($"  メニュー項目数: {descriptor.expressionsMenu.controls.Count}");
            }
            else
            {
                Debug.LogWarning($"  ⚠️ Expression Menuが未設定: {descriptor.name}");
            }

            if (descriptor.expressionParameters != null)
            {
                Debug.Log($"  ✅ Expression Parameters: {descriptor.expressionParameters.name}");
                Debug.Log($"  パラメータ数: {descriptor.expressionParameters.parameters.Length}");
            }
            else
            {
                Debug.LogWarning($"  ⚠️ Expression Parametersが未設定: {descriptor.name}");
            }
        }

        EditorUtility.DisplayDialog("Expression Menu確認", "Expression Menu情報をConsoleに出力しました", "OK");
    }

    private void CheckFaceEmoSettings()
    {
        var faceEmoComponents = FindObjectsOfType<MonoBehaviour>()
            .Where(mb => mb != null && mb.GetType().Name.Contains("FaceEmo"))
            .ToArray();

        Debug.Log($"\n😄 FaceEmo設定確認: {faceEmoComponents.Length} 個のコンポーネント");

        foreach (var component in faceEmoComponents)
        {
            Debug.Log($"  - {component.GetType().Name} on {component.gameObject.name}");
            Debug.Log($"    有効: {component.enabled}");
        }

        EditorUtility.DisplayDialog("FaceEmo確認",
            $"{faceEmoComponents.Length} 個のFaceEmoコンポーネントを確認\n詳細はConsoleを参照", "OK");
    }

    private void FixFaceEmoSettings()
    {
        var faceEmoComponents = FindObjectsOfType<MonoBehaviour>()
            .Where(mb => mb != null && mb.GetType().Name.Contains("FaceEmo"))
            .ToArray();

        int fixedCount = 0;

        foreach (var component in faceEmoComponents)
        {
            if (!component.enabled)
            {
                component.enabled = true;
                Debug.Log($"✅ FaceEmo有効化: {component.GetType().Name} on {component.gameObject.name}");
                fixedCount++;
            }
        }

        // パラメータ連動の修正
        fixedCount += FixFaceEmoParameterSync();

        EditorUtility.DisplayDialog("FaceEmo修正",
            fixedCount > 0 ? $"{fixedCount} 個のFaceEmoを修正しました" : "修正が必要な問題は見つかりませんでした", "OK");
    }

    private void EnsureExpressionAssets(VRCAvatarDescriptor descriptor)
    {
#if VRC_SDK_VRCSDK3
        const string autoFolder = "Assets/AutoGenerated";
        if (descriptor.expressionParameters == null)
        {
            if (!AssetDatabase.IsValidFolder(autoFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AutoGenerated");
            }

            var exp = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            AssetDatabase.CreateAsset(exp, $"{autoFolder}/{descriptor.name}_Params.asset");
            descriptor.expressionParameters = exp;
            EditorUtility.SetDirty(descriptor);
            Debug.Log($"✅ Expression Parameters作成: {descriptor.name}_Params");
        }

        if (descriptor.baseAnimationLayers != null && descriptor.baseAnimationLayers.Length > 4)
        {
            var fxLayer = descriptor.baseAnimationLayers[4];
            if (fxLayer.animatorController == null)
            {
                if (!AssetDatabase.IsValidFolder(autoFolder))
                {
                    AssetDatabase.CreateFolder("Assets", "AutoGenerated");
                }

                var controllerPath = $"{autoFolder}/{descriptor.name}_FX.controller";
                var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                fxLayer.animatorController = controller;
                descriptor.baseAnimationLayers[4] = fxLayer;
                EditorUtility.SetDirty(descriptor);
                Debug.Log($"✅ FXController作成: {descriptor.name}_FX.controller");
            }
        }
#endif
    }

    private void CleanUpFXController(AnimatorController controller, VRCExpressionParameters expParams)
    {
        if (controller == null || expParams == null) return;

        var validParams = new HashSet<string>(expParams.parameters.Select(p => p.name));
        foreach (var param in controller.parameters.ToArray())
        {
            if (!validParams.Contains(param.name))
            {
                controller.RemoveParameter(param);
            }
        }

        foreach (var layer in controller.layers)
        {
            CleanStateMachine(layer.stateMachine);
        }

        EditorUtility.SetDirty(controller);
    }

    private void CleanStateMachine(AnimatorStateMachine sm)
    {
        foreach (var child in sm.states.ToArray())
        {
            var state = child.state;
            if (state.motion == null)
            {
                sm.RemoveState(state);
                continue;
            }
            if (state.motion != null && state.name != state.motion.name)
            {
                state.name = state.motion.name;
            }
        }

        foreach (var sub in sm.stateMachines.ToArray())
        {
            CleanStateMachine(sub.stateMachine);
        }
    }

    private void UnifyWriteDefaults(AnimatorController controller, bool value)
    {
        if (controller == null) return;
        foreach (var layer in controller.layers)
        {
            SetWriteDefaultsRecursive(layer.stateMachine, value);
        }
    }

    private void SetWriteDefaultsRecursive(AnimatorStateMachine sm, bool value)
    {
        foreach (var child in sm.states)
        {
            var state = child.state;
            if (state != null && state.writeDefaultValues != value)
            {
                state.writeDefaultValues = value;
                EditorUtility.SetDirty(state);
            }
        }

        foreach (var sub in sm.stateMachines)
        {
            SetWriteDefaultsRecursive(sub.stateMachine, value);
        }
    }

    private int SyncMenuParameters(VRCAvatarDescriptor descriptor)
    {
#if VRC_SDK_VRCSDK3
        int fixedCount = 0;
        if (descriptor.expressionsMenu != null && descriptor.expressionParameters != null)
        {
            var list = descriptor.expressionParameters.parameters.ToList();
            var names = new HashSet<string>(list.Select(p => p.name));

            foreach (var control in descriptor.expressionsMenu.controls)
            {
                if (control == null || control.parameter == null) continue;
                var name = control.parameter.name;
                if (string.IsNullOrEmpty(name)) continue;

                if (!names.Contains(name))
                {
                    list.Add(new VRCExpressionParameters.Parameter
                    {
                        name = name,
                        valueType = VRCExpressionParameters.ValueType.Bool
                    });
                    names.Add(name);
                    fixedCount++;
                    Debug.Log($"✅ Expression Parameter追加: {name}");
                }

                if (descriptor.baseAnimationLayers != null && descriptor.baseAnimationLayers.Length > 4)
                {
                    var fxLayer = descriptor.baseAnimationLayers[4];
                    if (fxLayer.animatorController is AnimatorController controller &&
                        !controller.parameters.Any(p => p.name == name))
                    {
                        controller.AddParameter(name, AnimatorControllerParameterType.Bool);
                        EditorUtility.SetDirty(controller);
                        fixedCount++;
                        Debug.Log($"✅ FXパラメータ追加: {name}");
                    }
                }
            }

            if (fixedCount > 0)
            {
                descriptor.expressionParameters.parameters = list.ToArray();
                EditorUtility.SetDirty(descriptor.expressionParameters);
            }
        }
        return fixedCount;
#else
        return 0;
#endif
    }

    private int FixBlinkAndTrackingComponents(VRCAvatarDescriptor descriptor)
    {
#if VRC_SDK_VRCSDK3
        int fixedCount = 0;
        var comps = descriptor.GetComponentsInChildren<MonoBehaviour>(true)
            .Where(mb => mb != null &&
                        (mb.GetType().Name.Contains("BlinkDisabler") || mb.GetType().Name.Contains("TrackingControlDisabler")))
            .ToArray();

        foreach (var comp in comps)
        {
            var field = comp.GetType().GetField("parameterName",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                var current = field.GetValue(comp) as string;
                if (string.IsNullOrEmpty(current))
                {
                    field.SetValue(comp, "FaceEmoteSelect");
                    EditorUtility.SetDirty(comp);
                    fixedCount++;
                    Debug.Log($"✅ パラメータ名設定: {comp.GetType().Name} on {comp.gameObject.name}");
                }
            }
        }
        return fixedCount;
#else
        return 0;
#endif
    }

    private int FixFaceEmoParameterSync()
    {
#if VRC_SDK_VRCSDK3
        int fixedCount = 0;
        var avatarDescriptors = FindObjectsOfType<VRCAvatarDescriptor>();

        foreach (var descriptor in avatarDescriptors)
        {
            EnsureExpressionAssets(descriptor);
            fixedCount += SyncMenuParameters(descriptor);

            // Expression Parametersの確認・追加
            if (descriptor.expressionParameters != null)
            {
                var parameters = descriptor.expressionParameters.parameters.ToList();
                var faceSelect = parameters.FirstOrDefault(p => p.name == "FaceEmoteSelect");
                if (faceSelect == null)
                {
                    parameters.Add(new VRCExpressionParameters.Parameter
                    {
                        name = "FaceEmoteSelect",
                        valueType = VRCExpressionParameters.ValueType.Int,
                        defaultValue = 0f
                    });
                    descriptor.expressionParameters.parameters = parameters.ToArray();
                    EditorUtility.SetDirty(descriptor.expressionParameters);
                    fixedCount++;
                    Debug.Log("✅ パラメータ追加: FaceEmoteSelect");
                }
                else if (faceSelect.valueType != VRCExpressionParameters.ValueType.Int)
                {
                    faceSelect.valueType = VRCExpressionParameters.ValueType.Int;
                    descriptor.expressionParameters.parameters = parameters.ToArray();
                    EditorUtility.SetDirty(descriptor.expressionParameters);
                    fixedCount++;
                    Debug.Log("✅ FaceEmoteSelect の型を Int に修正");
                }
            }

            // FXControllerにパラメータを同期
            if (descriptor.baseAnimationLayers != null && descriptor.baseAnimationLayers.Length > 4)
            {
                var fxLayer = descriptor.baseAnimationLayers[4];
                if (fxLayer.animatorController is AnimatorController controller)
                {
                    var param = controller.parameters.FirstOrDefault(p => p.name == "FaceEmoteSelect");
                    if (param == null)
                    {
                        controller.AddParameter("FaceEmoteSelect", AnimatorControllerParameterType.Int);
                        EditorUtility.SetDirty(controller);
                        fixedCount++;
                        Debug.Log("✅ FXパラメータ追加: FaceEmoteSelect");
                    }

                    CleanUpFXController(controller, descriptor.expressionParameters);
                    UnifyWriteDefaults(controller, false);
                }
            }

            // FaceEmoコンポーネントのパラメータ名修正と再初期化
            var faceEmos = descriptor.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(mb => mb != null && mb.GetType().Name.Contains("FaceEmo"))
                .ToArray();

            foreach (var faceEmo in faceEmos)
            {
                try
                {
                    var field = faceEmo.GetType().GetField("parameterName",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var current = field.GetValue(faceEmo) as string;
                        if (string.IsNullOrEmpty(current) || current != "FaceEmoteSelect")
                        {
                            field.SetValue(faceEmo, "FaceEmoteSelect");
                            fixedCount++;
                            Debug.Log($"✅ FaceEmoパラメータ名設定: {faceEmo.name} → FaceEmoteSelect");
                        }
                    }

                    var method = faceEmo.GetType().GetMethod("Setup",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    method?.Invoke(faceEmo, null);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"FaceEmo設定更新失敗: {ex.Message}");
                }
            }

            fixedCount += FixBlinkAndTrackingComponents(descriptor);
        }

        return fixedCount;
#else
        Debug.LogWarning("VRChat SDK not found");
        return 0;
#endif
    }

    private void FixAllFacialSettings()
    {
        if (EditorUtility.DisplayDialog("一括修正確認",
            "全ての表情設定を一括で修正しますか？", "実行", "キャンセル"))
        {
            Debug.Log("🚀 表情設定一括修正開始");

            SetupFaceMesh();
            FixFaceEmoSettings();

            // アニメーションレイヤーの重み確認
            var avatarDescriptors = FindObjectsOfType<VRCAvatarDescriptor>();
            foreach (var descriptor in avatarDescriptors)
            {
                if (descriptor.baseAnimationLayers != null && descriptor.baseAnimationLayers.Length > 4)
                {
                    var fxLayer = descriptor.baseAnimationLayers[4];
                    if (fxLayer.isDefault)
                    {
                        fxLayer.isDefault = false;
                        Debug.Log($"✅ FXレイヤーをカスタムに設定: {descriptor.name}");
                    }
                }
            }

            Debug.Log("✅ 表情設定一括修正完了");
            EditorUtility.DisplayDialog("修正完了",
                "表情設定の一括修正が完了しました！\n\n次のステップ:\n1. VRChat SDKでビルドテスト\n2. 表情メニューの動作確認", "OK");
        }
    }
}