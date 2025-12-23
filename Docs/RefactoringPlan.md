# リファクタリング計画（現行実装反映版）

**目的**: 現在の実装を整理し、残タスク（セットアップ固定化、誤検出防止強化、シーン分割）を段階的に進める。

---

## 0. 現在の実装状況
- ✅ `QRManager`（Singleton）: MRUK から Trackable を取得し、`OnQRAdded / OnQRUpdated / OnQRLost` を発火。`QRInfo` で `firstPose/lastPose/lastSeenTime` を保持。`lostTimeout` で喪失判定。
- ✅ `QRObjectPositioner`: `OnQRUpdated` を購読し、IQR（過去5秒）で外れ値を除外したロバスト平均で Cube/Sphere を追従。Prefab 指定必須。
- ✅ `CubeColorOnQr`: 検出/喪失で色を変更。
- ✅ `HitValidator`: `OnQRLost` で複合判定（カメラ向き + 喪失ウィンドウ）。ハンマーは実物前提で Unity 側距離/スイングは不使用。
- ✅ `CameraOrientationMonitor`: 可視 UUID 数でカメラ向きを判定。
- ✅ `QRHitDetector`: 旧実装を削除済み（リカバリシーン含む）。

---

## 1. 目標
1) セットアップ時に座標をロックし、プレイ中は更新しない（突然移動防止）。  
2) 誤検出防止を「UUID集合＋距離しきい値（例: ≤1m）」で強化。  
3) Setup / Gameplay / End シーンを分け、ロック/リトライを明確化。  
4) 旧コンポーネント整理（QRHitDetector 等）。

---

## 2. 責務分離（To-Be）
- `QRManager`: MRUKトラッキングの唯一の入口。`OnQRAdded/OnQRUpdated/OnQRLost` を発火し、`lostTimeout` 管理と `lastSeenTime` 更新を担う。
- `QRPoseLocker`（新規）: セットアップ期間の収集・IQRロック・状態管理（Idle/Collecting/Locked/Failed/Retry中）。API 例: `BeginCollect()`, `Abort()`, `Retry()`, `GetLockedPose(uuid)`.
- `QRObjectPositioner`: `QRPoseLocker` の「確定 Pose」を読み込み、Prefab を配置。プレイ中は更新しない。
- `CubeColorOnQr`: 視覚フィードバックのみ（検出/喪失で色変更）。
- `QRTrustMonitor`（新規）: QRごとの既知UUID集合を管理。距離しきい値（例: ≤1m）を満たすUUIDのみ集合に採用し、可視集合と比較して信頼度を算出。

---

## 3. 座標ロックのロジック
- 収集窓: GameLoad/セットアップ開始後の10秒（可変パラメータ）。
- サンプル条件: `IsTracked==true` のみ採用。最小サンプル数を閾値化（不足なら Failed）。
- 集計: 軸ごとに IQR で外れ値除去 → 平均 or 中央値でロバスト平均。回転はスパイク除外 + 軽い Slerp ローパス。
- ロック: 条件を満たしたら Pose を固定。以後プレイ中は更新しない。再セットアップ時に履歴をクリア。

---

## 4. イベントフロー
- `QRManager` → `OnQRUpdated` で生ポーズ供給。
- `QRPoseLocker` が収集中なら履歴蓄積、ロック判定。ロック完了/失敗時に UI/ゲーム進行へ通知。
- `QRObjectPositioner` は Locked 以降に1回だけ配置（またはロック更新時に再配置）。
- `QRTrustMonitor` は既知UUID集合と現在可視集合を比較し、信頼度を露出（距離しきい値≤1mを適用）。

---

## 5. シーン構成（実運用向け）
- Setupシーン: QR確認/ロックを実施。ロック成功で次へ、失敗時はリトライ導線。
- GamePlayシーン: ロック済み Pose を使用して固定配置。QR更新は行わない（必要なら明示的に再セットアップへ戻す）。
- GameEnd/Resultシーン: スコア表示など。必要なら次ラウンド開始前に Setup に戻し、再ロックを許可。
- 共有オブジェクト（`QRManager`, `QRPoseLocker`, `QRTrustMonitor`）は `DontDestroyOnLoad` または ScriptableObject/シングルトンでシーン間を跨いで保持。ロック状態も一貫管理。

---

## 6. フェーズ計画

### フェーズA: 後片付けと安全網
- [x] シーンから `QRHitDetector` を無効化/削除（HitValidator に一本化）。
- [ ] `QRObjectPositioner` に Prefab が必ず割り当てられているか確認（ログで null が出ない状態）。
- [ ] `lostTimeout` の妥当値確認（短すぎて誤喪失しないか）。

### フェーズB: 座標ロック（セットアップ10秒のIQR固定）
- [ ] 新規 `QRPoseLocker` を追加（上記ロジックとAPIで実装）。
- [ ] `QRObjectPositioner` は Locked Pose を一度だけ配置し、プレイ中は更新しない（再セットアップ時のみ更新）。

### フェーズC: 誤検出防止（UUID集合＋距離しきい値）
- [ ] 新規 `QRTrustMonitor`：既知UUID集合を記録（距離≤1m内のみ採用）、可視集合と比較して信頼度判定。未知UUID増加や既知UUID消失で警告/再セットアップを促す。
- [ ] 距離しきい値を Inspector パラメータ化（初期値 ≤1m、運用で調整）。

### フェーズD: シーン分割とフロー
- [ ] Setup→Gameplay→End の遷移を整備。`QRManager/QRPoseLocker/QRTrustMonitor` をシーン跨ぎで保持。

---

## 7. テスト計画（実装後）
- 収集中に頭・カメラを大きく動かし、ロック後に位置が安定するか。
- 収集時間を 5s/10s で比較し、スパイク耐性をログで確認。
- サンプル不足・追跡不安定時に Failed となりリトライ導線が出るか。
- 複数QR同時ロックの挙動（各UUIDで独立にロックできるか）。
- Hit判定: `HitValidator` がカメラ向き + 喪失ウィンドウのみで動作し、旧 `QRHitDetector` に依存しないこと。

---

## 8. 導入のポイント
- 喪失検出は `QRManager` に一本化し、下流では独自に判定しない。
- セットアップとプレイでフローを分け、プレイ中の座標更新を止めることで突然の移動を防ぐ。
- ロック結果（Pose/品質情報）を UI に露出し、ユーザーに「ロック成功/失敗」を明示する。
- UUID集合＋距離しきい値（例: ≤1m）を用いて、単純な個数判定より堅牢に誤検出防止を行う。
