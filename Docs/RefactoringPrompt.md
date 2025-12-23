# Refactoring Prompt Set (for Codex Agent)  

現行のリファクタリング計画（Docs/RefactoringPlan.md）をフェーズごとに実行するためのプロンプト集です。各フェーズ完了後に次フェーズへ進んでください。  

---

## フェーズA: 後片付けと安全網
- 目的: 旧コンポーネント整理と設定確認。  
- プロンプト例:  
```
シーンから QRHitDetector を無効化/削除し、HitValidator での判定に一本化してください。QRObjectPositioner に Cube/Sphere Prefab が確実に割り当てられているか確認し、null ログが出ない状態にしてください。lostTimeout が短すぎないか確認し、必要なら妥当値に調整してください。コード変更後は概要を報告してください。
```

---

## フェーズB: 座標ロック（セットアップ10秒の IQR 固定）
- 目的: セットアップ時のみ座標を収集・固定し、プレイ中は更新しない。  
- プロンプト例:  
```
QRPoseLocker を新規追加し、Idle/Collecting/Locked/Failed/Retry の状態管理と API (BeginCollect, Abort, Retry, GetLockedPose) を実装してください。収集は OnQRUpdated の Pose を 10 秒間（IsTracked=true のみ）履歴化し、IQR で外れ値除外後にロバスト平均でロックします。サンプル不足なら Failed。QRObjectPositioner は Locked Pose を一度だけ配置し、プレイ中は更新しないように変更してください。変更点を報告してください。
```

---

## フェーズC: 誤検出防止（UUID集合＋距離しきい値）
- 目的: 既知UUID集合と距離しきい値で信頼度判定を強化。  
- プロンプト例:  
```
QRTrustMonitor を新規追加し、セットアップ時に QR ごとに既知UUID集合を記録してください。そのQRが見えているフレームで同時取得したUUIDのみ、かつ距離しきい値（初期 ≤1m）内のものを集合に入れてください。プレイ中は現在可視集合と比較し、既知集合の十分な割合が見えているか、未知UUIDが急増していないかを判定して信頼度を公開してください。信頼度低下時は警告/再セットアップを促す仕組みを追加してください。距離しきい値は Inspector から調整可能にしてください。変更点を報告してください。
```

---

## フェーズD: シーン分割とフロー
- 目的: Setup→Gameplay→End の明確なフローとシングルトン維持。  
- プロンプト例:  
```
Setup シーンで収集→ロック→信頼度チェックを行い、成功で Gameplay へ遷移、失敗ならリトライ導線を用意してください。Gameplay シーンではロック済み Pose を固定配置し、QR 更新は行わないでください。End/Result シーンから次ラウンド用に Setup へ戻れるようにしてください。QRManager/QRPoseLocker/QRTrustMonitor を DontDestroyOnLoad などでシーン跨ぎ維持し、ロック状態も一貫管理してください。シーン遷移と状態管理の概要を報告してください。
```

---

## 補足
- 上記プロンプトはフェーズ完了後に次フェーズへ進む想定です。  
- 実装前に必ず最新の `Docs/RefactoringPlan.md` を確認し、差分があればプロンプトを適宜調整してください。  
