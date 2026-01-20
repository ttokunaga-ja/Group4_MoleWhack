# Unity × Meta Quest 3S — 現行実装に合わせた QR 検出と安定配置の実装ガイド ✅

**更新日**: 2026年1月（内容を現在の実装に合わせて更新）  
**対象読者**: Unity MR 開発者（初級〜中級）  
**動作確認環境（参考）**:
- HMD: Meta Quest 3S
- Unity Editor: 6000.2.7f2
- Meta XR SDK: 81.0.0 (packages: `com.meta.xr.sdk.all`, `com.meta.xr.sdk.core`)
- MR Utility Kit (MRUK): 81.0.0 (`com.meta.xr.mrutilitykit`)
- Unity URP: 17.2.0 (`com.unity.render-pipelines.universal`)
- OpenXR: 1.16.0 (`com.unity.xr.openxr`)
- XR Management: 4.5.3 (`com.unity.xr.management`)
- Input System: 1.14.2 (`com.unity.inputsystem`)

> 参考: これらのバージョンは `ProjectSettings/ProjectVersion.txt` と `Packages/manifest.json` に基づきます。

---

## 概要
Meta XR Utility Kit (MRUK) で QR コードを検出し、World 座標を安定して取得・利用するまでの実装をまとめました。  
ポイントは「中央集約の QRManager」「喪失タイムアウト」「IQR（四分位範囲）を使ったスムージング」で、カメラ移動時のスパイクや瞬断に強い座標更新を実現しています。

---

## システム構成（最新版）

- **QRManager（Singleton）**  
  - MRUK から `MRUKTrackable` を取得し、UUID をキーに `QRInfo` を管理  
  - イベント: `OnQRAdded / OnQRUpdated / OnQRLost`  
  - `lostTimeout` を設け、一時的な見失いでは喪失扱いにしない  
  - `enableDetailedLogging` や `detectionCooldown` などの設定が追加されています

- **QRInfo**  
  - `firstPose`（初回検出時の Pose）と `lastPose`（最新の Pose）を保持
  - `lastSeenTime` で最終観測時刻を記録

- **QRObjectPositioner**  
  - `OnQRUpdated` を購読して Prefab（Respawn/Enemy）を生成・追従  
  - IQR 平滑化は `Common_QRPoseSmoother` に委譲（既定: 過去 5 秒、IQR k=1.5）  
  - 回転は軽い Slerp でローパス

- **QRPoseLocker / Prefab Helpers**  
  - `QRPoseLocker` が収集・ロックを担当（セットアップ運用）  
  - `Setup_QRPrefabResolver` / `Setup_QRAnchorFactory` が Prefab 解決・生成を担当

---

## コード構成（抜粋パターン）

### QRManager（中央集約）
- ファイル: `Assets/Scripts/Common/Common_QRManager.cs` ✅
- MRUK の `GetTrackables()` を毎フレーム走査し、`IsTracked` を基準に検出/更新/喪失判定を実施
- `lostTimeout`（既定 1.0s）を超えたトラッキング喪失で `OnQRLost` を発行
- `CurrentTrackedUUIDs` を公開し、カメラ向きチェックなどで利用可能

### QRInfo
- ファイル: `Assets/Scripts/Common/Common_QRInfo.cs` ✅
- `firstPose` / `lastPose` / `firstSeenAt` / `lastSeenTime` / `isTracked` を保持

### QRObjectPositioner（ゲーム固有の配置ロジック）
- ファイル: `Assets/Scripts/Setup/Setup_QRObjectPositioner.cs` ✅
- Respawn（旧 Cube） と Enemy（旧 Sphere） を生成・更新
- `useLockedPoseOnly` をサポート：`QRPoseLocker` によりロックされるまで待機する仕組みあり
- `poseSmoother`（`Common_QRPoseSmoother`）を用いて IQR 平滑化（既定: 5 秒）を適用
- 便利メソッド: `ClearAllEnemies()`, `ClearAllObjects()`, `ForceSpawnMissingEnemies()` など

(注) このリポジトリでは `CubeColorOnQr` は存在せず、代わりに Prefab の色変更や独自の視認フィードバックを `Respawn` / `Enemy` 側で実装してください。

---

## セットアップ手順

1. **Hierarchy**  
   - `MRUtilityKit`（MRUK コンポーネント付き）  
   - `QRManager`（シングルトン）  
   - `QRObjectPositioner`（Respawn/Enemy Prefab を Inspector に割当）  
   - Prefab の視認性を高めたい場合は Prefab 側で色変更等を実装してください（本リポジトリは専用コンポーネントを持ちません）

2. **Meta XR Settings**  
   - Scene Understanding / QR Code Tracking を有効化

3. **パラメータの目安**  
   - `lostTimeout`: 1.0s（瞬断に耐える）  
   - `poseSmoother` の窓長: 5 秒（既定）  
   - Prefab を `Assets/Resources/Prefabs/` に置けば Inspector 未設定でも自動ロード可

---

## デバッグ

```powershell
adb logcat -s Unity
```

- `QRManager` の `[QR_ADDED] / [QR_UPDATED] / [QR_LOST]` を確認
- `QRObjectPositioner` の `[QR_POSITIONED] / [QR_UPDATED]` で座標追従を確認
- Prefab 側のログや見た目で検出/喪失を確認

Tip: `QRManager` の `enableDetailedLogging` を有効にすると、より詳細なログが得られます。

---

## トラブルシュート

- **Prefab 未設定で無効化される**: `QRObjectPositioner` に Cube/Sphere Prefab を割当（または Resources/Prefabs に配置）  
- **座標がジャンプする**: IQR スムージングを有効化し、`sphereHeightOffset/cubeHeightOffset` を調整  
- **イベントが受け取れない**: `QRManager` がシーンに 1 つだけ存在するか確認  

---

## 今後の改善

- IQR 窓長や係数をユースケース別にプロファイルしてデフォルト値を最適化
- 長期安定化のために空間アンカー（OVRSpatialAnchor 等）を導入する
- QR のペイロードを利用したコンテンツ紐付けの実装  

