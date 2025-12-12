# SETUP ガイド（Unity × Quest 3S, QR 検出/座標ロック）

## 環境要件
- Unity 6000.2.7f2（Unity 6 LTS）
- Meta XR SDK v81 / MRUK v81 / URP 17.2.0
- ビルドターゲット: Android, ARM64, OpenGLES3, IL2CPP

## パッケージ
`com.meta.xr.mrutilitykit` / `com.meta.xr.sdk.all` / `com.meta.xr.sdk.core` / `com.unity.xr.openxr`

## シーン構成（推奨）
1. **Setup シーン**: QR 収集・座標ロック・UUID信頼度チェック
2. **Gameplay シーン**: 固定Poseで配置。プレイ中は座標更新しない
3. **End/Result シーン**: 次ラウンド前に必要なら再セットアップ

`QRManager`, `QRPoseLocker`, `QRTrustMonitor` は `DontDestroyOnLoad` 等でシーンを跨いで維持。

## ヒエラルキーの基本
- `MRUtilityKit`（MRUK コンポーネント付き）
- `QRManager`（Singleton）
- `QRObjectPositioner`（Cube/Sphere Prefab を Inspector で割当）
- `CameraOrientationMonitor` / `HitValidator`（複合判定用）
- Cube Prefab に `CubeColorOnQr` を付与（検出/喪失で色変化）

## セットアップ手順（Setup シーン例）
1. MRUK 設定: Scene Understanding / QR Code Tracking を有効化
2. Prefab 割当: `QRObjectPositioner` に Cube/Sphere Prefab を設定（未設定の場合は Resources/Prefabs から自動ロードも可）
3. 収集開始: `QRPoseLocker` が 10秒間サンプル収集（`IsTracked==true` のみ採用）
4. ロック判定: IQR で外れ値除外後、ロバスト平均を算出し Pose を固定。サンプル不足なら Failed → 再セットアップ
5. UUID 信頼度: `QRTrustMonitor` が既知UUID集合を確定し、以後は **距離しきい値（例: ≤1m）** 以内の UUID のみ集合に採用。プレイ中も可視集合と比較して信頼度を監視。
6. ロック完了後: Gameplay シーンへ遷移し、固定Poseを `QRObjectPositioner` で1回配置。

## ビルド・デプロイ
```bash
# 例: APK ビルド後のデプロイ
adb install -r Group4_MoleWhack.apk
adb shell am start -n com.UnityTechnologies.com.unity.template.urpblank/com.unity3d.player.UnityPlayerGameActivity
```

## 簡易検証フロー
- `QRManager`: `[QR_ADDED] / [QR_UPDATED] / [QR_LOST]` ログが出ているか、`lostTimeout` が短すぎないか確認。
- `QRObjectPositioner`: `[QR_POSITIONED] / [QR_UPDATED]` が出ており、Prefab 未設定で無効化されていないか。
- セットアップ: 収集サンプル数が十分か、IQR後に有効サンプルが残るか、ロック完了を UI/ログで通知。
- 信頼度: 既知UUID集合と現在可視集合の比較で急な未知UUID増加/既知UUID消失がないか（距離しきい値 ≤1m を適用）。
- Hit 判定: `HitValidator` でカメラ向き + 喪失時間ウィンドウが機能しているか。

## トラブルシュート
- **Prefab 未設定で無効化**: `QRObjectPositioner` に Cube/Sphere Prefab を割当（または Resources/Prefabs に配置）。
- **座標がジャンプする**: セットアップ時の IQR ロックを有効化し、窓長/係数を調整。プレイ中は更新しない。
- **喪失判定が早すぎる**: `lostTimeout` を延長。`IsTracked==false` の Trackable は更新から除外。
- **UUID 信頼度が低下**: 距離しきい値（例: ≤1m）を適用し、未知UUID混入を抑制。必要なら再セットアップ。

## 参考ドキュメント
- 技術記事: `Docs/BlogArticle_QR_WorldCoordinates.md`  
- リファクタリング計画: `Docs/RefactoringPlan.md`
