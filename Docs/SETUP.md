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

## ヒエラルキーの基本（現行）
- `MRUtilityKit`（MRUK コンポーネント付き）
- `QRManager`（Singleton）
- `QRPoseLocker`（30秒収集で IQR 固定）
- `QRTrustMonitor`（距離しきい値付きの既知UUID集合管理）
- `QRObjectPositioner`（Respawn/Enemy Prefab を Inspector で割当、Locked Pose を使用）
- `CameraOrientationMonitor` / `HitPipeline` / `HitValidator`（複合判定）

### ヒエラルキー構造図（Unity Hierarchy）

```
Setup
├─ Directional Light
├─ OVRCameraRig
│  ├─ TrackingSpace
│  │  ├─ CenterEyeAnchor
│  │  │  └─ Canvas (UI)
│  │  └─ …(OVR default children)
├─ MRUK_Manager (MRUK components)
├─ MRUtilityKit (MRUK components)
├─ QRPoseLockerRoot
│  ├─ QRPoseLocker (script, DontDestroy)
│  └─ QRTrustMonitor (script, DontDestroy)
├─ QRObjectPositioner (script: Setup_QRObjectPositioner)  // Prefabs: hole, mole, mole_defeated, Golden_mole, Golden_mole_defeated
├─ CameraOrientationMonitor (script: Common_CameraOrientationMonitor)
├─ HitPipeline (script: Gameplay_HitPipeline)
├─ HitValidator (script: Gameplay_HitValidator)
├─ GameFlowController (script: Common_GameFlowController)
├─ GameSessionManager (script: Common_GameSessionManager)
├─ ScoreManager (script: Common_ScoreManager)
└─ Log (script: Common_LogToFile)
```

### Setupシーン: オブジェクト作成手順
- Directional Light: Hierarchy 右クリック > Light > Directional Light。
- OVRCameraRig: Project の OVR/Prefabs から OVRCameraRig をドラッグ＆ドロップ（または Building Blocks の Camera Rig ブロック）。
- MRUK_Manager / MRUtilityKit: Building Blocks から MR Utility Kit（MRUK）ブロックを追加。
- QRPoseLockerRoot: Hierarchy 右クリック > Create Empty を作成し、`QRPoseLocker` と `QRTrustMonitor` コンポーネントをアタッチ（DontDestroy にする場合はスクリプト内で維持）。
- QRObjectPositioner: Hierarchy 右クリック > Create Empty を作成し、`Setup_QRObjectPositioner` をアタッチ。Inspector で Prefab (hole/mole/mole_defeated/Golden_mole/Golden_mole_defeated) を割当。
- CameraOrientationMonitor: Create Empty に `Common_CameraOrientationMonitor` をアタッチ。
- HitPipeline: Create Empty に `Gameplay_HitPipeline` をアタッチ。
- HitValidator: Create Empty に `Gameplay_HitValidator` をアタッチし、HitPipeline 参照を設定。
- GameFlowController: Create Empty に `Common_GameFlowController` をアタッチ。
- GameSessionManager: Create Empty に `Common_GameSessionManager` をアタッチ。
- ScoreManager: Create Empty に `Common_ScoreManager` をアタッチ。
- Log: Create Empty に `Common_LogToFile` をアタッチ。

### Gameplayシーン: オブジェクト作成手順（UIなし最小構成）
- Directional Light / Global Volume: Hierarchy 右クリックで追加（レンダリング用）。
- OVRCameraRig: OVR/Prefabs からドラッグ、または Building Blocks の Camera Rig。
- MRUK_Manager / MRUtilityKit: Building Blocks から MRUK ブロックを追加。
- QRManager: Create Empty に `Common_QRManager` をアタッチ（Bootstrap でも可）。
- QRObjectPositioner: Create Empty に `Setup_QRObjectPositioner` をアタッチ。Prefab (hole/mole/mole_defeated/Golden_mole/Golden_mole_defeated) を割当。
- CameraOrientationMonitor: Create Empty に `Common_CameraOrientationMonitor` をアタッチ。
- HitPipeline: Create Empty に `Gameplay_HitPipeline` をアタッチ。
- HitValidator: Create Empty に `Gameplay_HitValidator` をアタッチし、HitPipeline を参照に設定。
- GameFlowController: Create Empty に `Common_GameFlowController` をアタッチ。
- GameSessionManager: Create Empty に `Common_GameSessionManager` をアタッチ。
- ScoreManager: Create Empty に `Common_ScoreManager` をアタッチ。
- (Optional) SingleSceneSetupBootstrap: Create Empty に `Gameplay_SingleSceneSetupBootstrap` をアタッチ（Gameplay単独起動用の暫定措置）。
- UI は最小構成では不要。デバッグ表示を行う場合のみ Canvas を追加。

### Resultsシーン: オブジェクト作成手順
- Directional Light: Hierarchy 右クリック > Light > Directional Light。
- OVRCameraRig: OVR/Prefabs からドラッグ（または Camera Rig ブロック）。
- ResultUI: Canvas を World Space で配置し、`Results_ResultUIController` をアタッチ。Replay/Setup ボタンを配置し、GameFlowController に遷移イベントを接続。Text/TMP でスコアや統計表示。
- GameFlowController: Create Empty に `Common_GameFlowController` をアタッチ（DontDestroy のものがあれば省略可）。
- GameSessionManager: Create Empty に `Common_GameSessionManager` をアタッチ（セッション状態を参照）。
- ScoreManager: Create Empty に `Common_ScoreManager` をアタッチ。
Gameplay
```
Gameplay
├─ Directional Light
├─ Global Volume
├─ OVRCameraRig
│  ├─ TrackingSpace
│  │  ├─ CenterEyeAnchor
│  │  │  └─ Canvas (Gameplay UI/XRUI)
│  │  └─ …(OVR default children)
├─ MRUK_Manager (MRUK components)
├─ MRUtilityKit (MRUK components)
├─ QRManager (script: Common_QRManager)
├─ QRObjectPositioner (script: Setup_QRObjectPositioner)  // Prefabs: hole, mole, mole_defeated, Golden_mole, Golden_mole_defeated
├─ CameraOrientationMonitor (script: Common_CameraOrientationMonitor)
├─ HitPipeline (script: Gameplay_HitPipeline)
├─ HitValidator (script: Gameplay_HitValidator)
├─ GameFlowController (script: Common_GameFlowController)
├─ GameSessionManager (script: Common_GameSessionManager)
├─ ScoreManager (script: Common_ScoreManager)
└─ (Optional) SingleSceneSetupBootstrap (script: Gameplay_SingleSceneSetupBootstrap)
```

Results
```
Results
├─ Directional Light
├─ OVRCameraRig
│  ├─ TrackingSpace
│  │  ├─ CenterEyeAnchor
│  │  │  └─ Canvas (Results UI)
│  │  └─ …(OVR default children)
├─ ResultUI (Canvas + script: Results_ResultUIController)
├─ GameFlowController (script: Common_GameFlowController)
├─ GameSessionManager (script: Common_GameSessionManager)
└─ ScoreManager (script: Common_ScoreManager)
```

### Interaction UI ベストプラクティス（Unity 6 + Meta XR SDK v83 想定）
- EventSystem: StandaloneInputModule ではなく Interaction SDK の PointableCanvasModule を使用。  
- Canvas: World Space。Interaction SDK の Quick Actions（Add Ray/Poke Interaction to Canvas）で RayCanvas/PokeCanvas を付与し、CenterEyeAnchor を Event Camera に設定。  
- Text: TextMeshPro (Mobile/Distance Field)。Outline/Underlay で可読性を確保。  
- Passthrough: OVRPassthroughLayer は Underlay 推奨。UI 透明度は控えめにし、URP 透明バグ対策で AlphaToMask を無効にする（必要に応じて）。  
- Occlusion/Depth: Building Blocks の Occlusion ブロックや MRUK の EnvironmentDepth を活用し、現実物体で UI を遮蔽。  

## セットアップ手順（Setup シーン例）
1. MRUK 設定: Scene Understanding / QR Code Tracking を有効化
2. Prefab 割当: `QRObjectPositioner` に Respawn/EnemyDefault/Enemy1/Defeated を設定（未設定は Resources/Prefabs から自動ロード: hole/mole/Golden_mole 等）
3. 収集開始: `QRPoseLocker` が 30秒サンプル収集（`IsTracked==true` のみ採用）
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
- **Prefab 未設定で無効化**: `QRObjectPositioner` に Respawn/Enemy Prefab を割当（または Resources/Prefabs に配置）。
- **座標がジャンプする**: セットアップ時の IQR ロックを有効化し、窓長/係数を調整。プレイ中は更新しない（Locked Pose を使う）。
- **喪失判定が早すぎる**: `lostTimeout` を延長。`IsTracked==false` の Trackable は更新から除外。
- **UUID 信頼度が低下**: 距離しきい値（例: ≤1m）を適用し、未知UUID混入を抑制。必要なら再セットアップ。

## 参考ドキュメント
- 技術記事: `Docs/BlogArticle_QR_WorldCoordinates.md`  
- リファクタリング計画: `Docs/RefactoringPlan.md`
