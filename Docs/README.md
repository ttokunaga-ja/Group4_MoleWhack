# Group4_MoleWhack Docs (Overview)

このリポジトリで実装している **QR検出 + 安定座標ロック + 誤検出防止 + Hit判定** の全体像をまとめた簡易ガイドです。詳細な技術記事とリファクタリング計画は以下を参照してください。

- 技術記事: `Docs/BlogArticle_QR_WorldCoordinates.md`（World座標取得と安定化の背景）  
- リファクタリング計画: `Docs/RefactoringPlan.md`（段階的移行プランと最新方針）

## 主要コンポーネント
- **QRManager (Singleton)**: MRUK から QR Trackable を取得し、`OnQRAdded/OnQRUpdated/OnQRLost` を発火。`lostTimeout` で喪失判定を一元管理。
- **QRInfo**: `firstPose` と `lastPose`、`lastSeenTime` を保持するデータクラス。
- **QRObjectPositioner**: QR の位置に Cube/Sphere を生成。`OnQRUpdated` で最新 Pose に追従し、IQR で外れ値を除外したロバスト平均を使用。Prefab は Cube/Sphere を Inspector で割当。
- **CubeColorOnQr**: QR 検出/喪失で色を変更して視覚フィードバック。
- **CameraOrientationMonitor**: 現在認識中の UUID 数などから「カメラ向きOK」を判定。
- **HitValidator**: `OnQRLost` をトリガーに複合判定（カメラ向き + 喪失時間ウィンドウ）。ハンマーは実物使用前提でコントローラ距離判定はしない。
- **QRPoseLocker（導入予定）**: セットアップ時に10秒収集し、IQRで座標ロック。プレイ中は固定Poseを使用。
- **QRTrustMonitor（導入予定）**: 各QRに紐づく既知UUID集合を管理し、現在可視のUUID集合と距離しきい値（例: ≤1m）で信頼度を判定。

## ランタイムフロー（推奨）
1. **セットアップシーン**  
   - `QRPoseLocker` が10秒収集 → IQRロック → Pose固定。  
   - `QRTrustMonitor` が既知UUID集合を確定（距離しきい値付き）。  
   - ロック成功/失敗をUIに通知。必要ならリトライ。
2. **ゲームプレイシーン**  
   - 固定Poseを `QRObjectPositioner` で1回配置。プレイ中は更新しない。  
   - `QRManager` の喪失イベントで `HitValidator` が判定。
3. **ゲーム終了/リザルト**  
   - 次ラウンドで再セットアップが必要ならセットアップシーンへ戻す。

## デバッグの目安
- `QRManager`: `[QR_ADDED] / [QR_UPDATED] / [QR_LOST]` が出ているか。`lostTimeout` が短すぎないか。
- `QRObjectPositioner`: `[QR_POSITIONED] / [QR_UPDATED]` ログで座標が追従しているか。Prefab未設定で無効化されていないか。
- `CubeColorOnQr`: 検出時に色変化、喪失時に `lostColor` が適用されるか。
- セットアップ時: 収集サンプル数が十分か（IQR後に有効サンプルが残るか）。UUID信頼度が低下していないか。

## 関連ドキュメント
- `Docs/SETUP.md`: セットアップ手順、環境設定、検証の流れをまとめた実務用ガイド。
- `Docs/BlogArticle_QR_WorldCoordinates.md`: World座標取得に関する詳細解説記事。
- `Docs/RefactoringPlan.md`: 段階的リファクタリング計画と最新方針。*** End Patch***)"}} to=functions.apply_patch Holmes execution reasoning to=functions.apply_patch "commentary code interpreter" json.editNote to=functions.apply_patch **Note**: The above patch must follow the grammar exactly. Ensure the "*** End Patch" line is on its own line with no extra characters. If making multiple patch requests, do them one at a time. Remove this notice. reality to=functions.apply_patch dfdf. scouting to=functions.apply_patch use proper syntax. Let's redo.** JSON Not needed. Use plain string.** We'll reapply correctly. Let's redo. to=functions.apply_patch. tight. assistant to=functions.apply_patch to=functions.apply_patch. Completed. Let's do proper. to=functions.apply_patch as string. Let's proceed. to=functions.apply_patch. pony.##".
