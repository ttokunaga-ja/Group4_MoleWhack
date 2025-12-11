# QR コード認識喪失による当たり判定システム - セットアップガイド

## 🎯 システム概要

**実物のハンマーで QR コードを叩く** → **QR が隠れて認識喪失** → **当たり判定成功**

このシステムは、物理的な道具（プラスチック製ピコピコハンマー等）で QR コードを叩くことで、仮想オブジェクト（Sphere）との「当たり判定」を実現します。

---

## 📋 動作フロー

```
【ゲーム開始】
  ↓
✓ QR コード #1 を Quest 3S カメラに向ける
  ↓
✓ Cube（青色マーカー）+ Sphere（赤色ターゲット）が表示される
  ↓
【プレイヤーがハンマーを振る】
  ↓
✓ ハンマーが QR コードに当たる
  ↓
✓ QR コードが一瞬隠れる
  ↓
✓ QR 認識喪失イベント発火
  ↓
★★★ 当たり判定成功！★★★
  ↓
✓ Sphere が消える（視覚的フィードバック）
✓ スコア加算
✓ ログに HIT SUCCESS 記録
  ↓
✓ Cube は残る（QR 位置マーカーとして）
  ↓
【QR コードが再び見える】
  ↓
✓ Sphere が再出現（次のターゲット）
```

---

## 🛠️ Unity Hierarchy セットアップ

### **Step 1: QRHitDetector GameObject を作成**

```
Hierarchy:
├── OVRCameraRig
├── Cube
├── QRCodeTracker (既存)
│   └── QRCodeTracker_MRUK.cs
├── QRObjectPositioner (既存)
│   └── QRObjectPositioner.cs
└── QRHitDetector ✨ ← 新規作成
    └── QRHitDetector.cs
```

**作成手順:**
1. Hierarchy で右クリック → **Create Empty**
2. 新規 GameObject を `QRHitDetector` に名前変更
3. `QRHitDetector.cs` をアタッチ

### **Step 2: Inspector 設定**

#### **QRHitDetector コンポーネント:**

| 項目 | 設定値 | 説明 |
|------|--------|------|
| **QR Code Tracker** | QRCodeTracker | Hierarchy の QRCodeTracker を指定 |
| **Enable Hit Logging** | ✓ チェック | コンソールに当たり判定ログ出力 |
| **On Hit Success** | （イベント設定） | 当たり判定成功時の処理（スコア加算等） |

---

## 🎮 実行例

### **テスト手順:**

1. **QR コードを印刷**
   - 2〜3 枚の QR コードを用意
   - A4 用紙に印刷

2. **QR コードをテーブルに配置**
   - テーブル上に QR コードを固定

3. **Quest 3S を装着**
   - アプリを起動

4. **QR コードを見る**
   - Cube（青）と Sphere（赤）が表示される

5. **ハンマーで叩く**
   - プラスチック製ピコピコハンマーで QR コードを叩く
   - QR が一瞬隠れる

6. **結果確認**
   - Sphere が消える ✅
   - Cube は残る ✅
   - ログに `[HIT_SUCCESS] ★★★ HIT DETECTED ★★★` 表示 ✅

---

## 📊 ログ出力例

```
[QRHitDetector] [START] ✓ QRHitDetector ready
[QRHitDetector] [OnEnable] ✓ QR Lost event listener registered

【ハンマーで QR を叩く】

[QRHitDetector] ========================================
[QRHitDetector] [HIT_SUCCESS] ★★★ HIT DETECTED ★★★
[QRHitDetector] ========================================
[QRHitDetector] [HIT_SUCCESS] QR UUID: dfb9d052-ac38-15bd-0c0d-ba2263565688
[QRHitDetector] [HIT_SUCCESS] Total Hits: 1
[QRHitDetector] [HIT_SUCCESS] Time: 5.43s
[QRHitDetector] ========================================
[QRHitDetector] [HIT_SUCCESS] ✓ OnHitSuccess event invoked

[QRObjectPositioner] [QR_LOST] UUID: dfb9d052-ac38-15bd-0c0d-ba2263565688
[QRObjectPositioner] [QR_REMOVED] ✓ Sphere destroyed: CollisionSphere
[QRObjectPositioner] [QR_REMOVED]   Remaining Spheres: 0
[QRObjectPositioner] [QR_REMOVED]   ⚠ Cube marker remains for position reference
```

---

## 🎯 スコアシステムの実装例

### **Option A: シンプルなスコア加算**

```csharp
using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    [SerializeField] private QRHitDetector hitDetector;
    [SerializeField] private TextMeshProUGUI scoreText;
    
    private int score = 0;

    private void Start()
    {
        // QRHitDetector の OnHitSuccess イベントに登録
        hitDetector.OnHitSuccess.AddListener(OnQRHit);
        UpdateScoreUI();
    }

    private void OnQRHit(string uuid)
    {
        score += 10;  // 1ヒットあたり 10 点
        UpdateScoreUI();
        Debug.Log($"[ScoreManager] Score: {score}");
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score: {score}";
        }
    }
}
```

---

## 🔧 パラメータ調整

### **Sphere の見やすさ調整**

```
QRObjectPositioner:
- Sphere Scale: 0.15 → 0.2（大きめ）
- Sphere Height Offset: 0.25 → 0.3（高め）
```

### **Cube の透明度調整**

`QRObjectPositioner.cs` の `CreateCubeMarker()` で：

```csharp
material.color = new Color(0.3f, 0.8f, 1.0f, 0.5f);  // alpha を 0.5 に
```

---

## 📝 よくある質問

### **Q1: QR コードを叩いても反応しない**

**原因:**
- QR コードが完全に隠れていない
- 認識喪失の検出が遅い

**解決策:**
- ハンマーを大きく振る
- QR コードをしっかり隠す
- QR コードのサイズを小さくする（5cm 角程度）

### **Q2: Sphere が消えてから復活しない**

**原因:**
- QR コードが再び見えていない

**解決策:**
- ハンマーを退けて QR コードをカメラに向ける
- QR コードの角度を調整

### **Q3: 複数の QR コードで同時にプレイしたい**

**設定:**
- 現在の実装で既に対応済み
- 各 QR コードに Sphere が配置される
- それぞれ独立して当たり判定が機能

---

## 🎮 ゲームモード例

### **モグラ叩きモード**

1. 複数の QR コードをテーブルに配置
2. ランダムに Sphere が光る（アニメーション追加）
3. 光っている Sphere の QR コードを叩く
4. 制限時間内に何個叩けるかを競う

### **スピードモード**

1. 1 つの QR コードを使用
2. Sphere が消えたらすぐ再出現
3. 連続ヒット数を計測
4. コンボシステムの実装

---

## 🚀 次のステップ

1. **スコアシステムの実装** - UnityEvent で簡単に連携
2. **エフェクトの追加** - Sphere 消滅時にパーティクル
3. **サウンド追加** - ヒット時に効果音
4. **UI 表示** - リアルタイムスコア表示

---

## 完成！🎉

これで実物のハンマーで QR コードを叩く → 当たり判定成功のシステムが完成しました。

**動作確認:**
- QR コードを見る → Sphere 表示 ✅
- ハンマーで叩く → QR 隠れる → Sphere 消滅 ✅
- ログに HIT SUCCESS 記録 ✅
