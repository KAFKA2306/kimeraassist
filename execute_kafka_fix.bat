@echo off
echo =======================================================
echo          🚀 KAFKA 10分修正 - 自動実行
echo =======================================================
echo.
echo 君の分析に基づく3系統衝突対応を実行します...
echo.

echo [1/3] Unity Editorの起動確認...
echo      プロジェクト: devtools
echo      修正スクリプト: Assets/Editor/FixAvatarCriticalIssues.cs

echo.
echo [2/3] 修正内容:
echo      ✅ NDMF ApplyOnPlay 無効化
echo      ✅ VRCFury PlayMode Trigger 削除
echo      ✅ AO過剰最適化 一時停止
echo      ✅ MA-1400 MergeArmature 修正
echo      ✅ Animator空パラメータ 解消
echo      ✅ リップシンク 復旧
echo      ✅ PhysBone参照切れ 対応

echo.
echo [3/3] 実行手順:
echo      1. Unity Editor が開いたら
echo      2. Tools ^> KAFKA Fix - 10分でビルド復旧
echo      3. 「全修正を一括実行」をクリック
echo      4. VRChat SDK ^> Build ^& Test でテスト

echo.
echo ⚠️  手動対応（1つだけ）:
echo      Body参照アニメ → 顔メッシュ名リマップ
echo      （スクリプトからガイドが表示されます）

echo.
echo =======================================================
pause

echo Unity Editorを起動中...
start "" "%USERPROFILE%\AppData\Roaming\UnityHub\Unity Hub.exe" -- --projectPath "%cd%"

echo.
echo Unity起動完了後、上記手順に従ってください
echo 修正完了まで約10分です
pause