# kimeraassist — VRChatアバター診断・修復用Unity Editorツール

**リポジトリ:** https://github.com/KAFKA2306/kimeraassist

Unity上のVRChatアバターで発生する、Expression Parameters、FX Animator、Expression Menu、PhysBone、Renderer Bounds、Transform異常などを診断・修復するためのEditorツール集です。

特定のアバターで発生した問題を解決するために作られたコードを含みます。すべてのアバター・SDK・ツール構成へ安全に適用できる汎用修復ツールではありません。

## Vision

壊れたアバターを自動で全部直すことではなく、何が壊れているかを先に見つけ、変更予定を理解し、必要な箇所だけ直して差分と実動作を確認できるトラブルシューティング体験を作ります。

## Design philosophy

- diagnosisとmutationを分離する
- automatic repairより原因理解と差分確認を優先する
- backup、Git差分、Undo可能性を先に確保する
- Expression Parameters、Animator、Bounds、PhysBoneなどを別のfailure domainとして扱う
- Unity上の修復成功をVRChat runtime成功として扱わない
- 同名parameterやmissing referenceを意味確認なしに一括修復しない

## Why / 差別化

価値はEditor scriptの数ではありません。症状を確認し、診断し、限定的に修復し、差分を確認し、Play Mode、SDK Build & Test、必要ならVRChat内確認へ進む一連の安全な切り分けを同じ流れで実行できることにあります。

## 主な機能

コミット履歴で確認できる主な処理:

- FaceEmo関連パラメーターの診断
- `FaceLock`、`Face_variation`などの同期補助
- FX Animator内の遷移条件から不足パラメーター候補を検出
- Expression MenuとExpression Parametersの型・参照確認
- 不要または空のFXパラメーター整理
- PhysBoneのMissing Reference候補修復
- VRCFury、Avatar Optimizer、Modular Avatar、NDMF関連の競合診断
- NaN / Infinityを含むTransformの検出・補正
- 不正なRenderer / Collider Boundsの検出・補正
- 一括修復用Editor入口とCLI入口

## 重要な警告

**このツールはAnimator、Expression Parameters、Menu、Transform、Boundsなどを変更する可能性があります。**

使用前に必ず次を行ってください。

1. UnityプロジェクトをGit管理する
2. 専用ブランチを作る
3. アバターPrefabのバックアップを取る
4. Console Errorを保存する
5. 修復前のDescriptor、FX、Menu、Parametersを記録する
6. 一つずつ機能を実行する
7. 差分を確認してから保存する

## 想定する問題

### 表情が選択できない

- MenuのParameter名とExpression Parametersが一致しない
- Parameter型がBool / Int / Floatで不一致
- FX AnimatorのConditionに使われるParameterが未登録
- Descriptorが別のParameters Assetを参照している
- FaceEmo固有Parameterが欠けている

### Build時にMissingReferenceException

- PhysBone、Collider、Constraintが削除済みComponentを参照している
- Modular Avatar統合後に参照先が変わった
- NDMFビルド時に一時オブジェクトへ不正参照が残る

### Invalid AABB

- TransformのPosition / Rotation / ScaleにNaNまたはInfinityがある
- Renderer Boundsが非有限値になっている
- ColliderのCenter / Sizeが壊れている
- メッシュ頂点やBlendShapeに非有限座標がある

## 使用方法

Unityプロジェクトの`Assets/Editor/`配下など、Editorスクリプトとして認識される場所へ配置します。

実際のメニュー名と入口は、現在のC#ファイル内の`MenuItem`属性を正としてください。

推奨手順:

```text
診断のみ実行
  → 検出結果を確認
  → 対象Assetをバックアップ
  → 個別修復を実行
  → Git差分を確認
  → Play Mode
  → VRChat SDK Build & Test
  → クライアント内確認
```

## 修復後の確認

- Unity Consoleに新しいErrorがないか
- Avatar DescriptorのPlayable Layers
- Expression Parametersの合計コスト
- Expression Menuの全ボタン
- FX Animatorの全Parameter型
- Write DefaultsやLayer Weightへの影響
- PhysBoneとColliderの参照
- Modular Avatarビルド結果
- Avatar Optimizer適用後の動作
- 表情、まばたき、リップシンク
- VRChat内での同期

## 自動修復の限界

- 同名Parameterでも意味が同じとは限りません
- 不足Parameterを追加してもAnimatorロジック自体が正しいとは限りません
- NaNを0へ置き換えると見た目が変わる可能性があります
- Boundsを再設定するとCulling挙動が変わる可能性があります
- Missing Referenceを削除すると機能そのものが失われる場合があります
- ツール間競合は各ツールのバージョンに依存します

## 対応環境

README作成時点では、正確なUnity、VRChat SDK、VRCFury、Modular Avatar、NDMF、Avatar Optimizerの対応バージョンを固定する設定は確認していません。

実運用前に、使用中のVCCプロジェクトで依存関係をロックし、検証済み組み合わせをREADMEまたはmanifestへ追記してください。

## 改修優先度

- Dry Runと変更予定一覧
- Asset変更前の自動バックアップ
- Undo対応
- 修復レポートJSON
- 対応バージョンmanifest
- EditMode Test
- サンプル用の最小壊れたPrefab
- 一括修復と個別修復の分離

## 注意

- 本ツールはVRChat公式または各アバター制作者の公式ツールではありません
- 購入アバターや第三者Assetをリポジトリへ追加しないでください
- 修復後の動作・アップロード可否は利用者が確認してください
- 問題の原因が分からない状態で「全部修復」を実行しないでください

**README最終監査:** 2026-09-07
