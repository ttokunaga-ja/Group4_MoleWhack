# QR コード座標オブジェクト配置システム - セットアップガイド

## 概要

このシステムは、QR コード上に自動的にオブジェクトを配置し、QR の位置が更新されるとリアルタイムで追跡します。複数の QR コードが検出された場合、それぞれの上に異なるオブジェクトが配置されます。

---

## 📋 システム構成

### 1. **QRCodeTracker_MRUK.cs** (既存・拡張版)
- **役割**: QR コード検出・追跡の中核
- **新機能**: イベント発行
  - `OnQRDetected`: QR 検出時にイベント発行
  - `OnQRLost`: QR 喪失時にイベント発行

### 2. **QRObjectPositioner.cs** (新規)
- **役割**: QR コード座標にオブジェクトを配置・更新
- **機能**:
  - 複数 QR に対応
  - 位置オフセット設定可能
  - 回転トラッキング（オプション）
  - スケール調整可能

---

## 🛠️ Hierarchy セットアップ

### Step 1: QRObjectPositioner GameObject を作成

```
Hierarchy:
├── OVRCameraRig
├── Cube
├── QRCodeTracker (既存)
│   └── (スクリプト: QRCodeTracker_MRUK)
└── QRObjectPositioner ✨ ← 新規作成
    └── (スクリプト: QRObjectPositioner)
```

**作成手順:**
1. Hierarchy で右クリック → **Create Empty**
2. 新規 GameObject を `QRObjectPositioner` に名前変更
3. `QRObjectPositioner.cs` をアタッチ

### Step 2: インスペクタ設定

#### **QRObjectPositioner コンポーネント:**

| 項目 | 設定値 | 説明 |
|------|--------|------|
| **QR Code Tracker** | QRCodeTracker | シーン内の QRCodeTracker を指定 |
| **Object Prefab** | （任意） | QR 上に配置するオブジェクト（キューブ推奨） |
| **Position Offset** | (0, 0.3, 0) | QR 座標からのオフセット（例：上に 30cm） |
| **Rotate With QR** | ✓ チェック | QR の回転に追従するか |
| **Object Scale** | 0.2 | 配置オブジェクトのスケール |

---

## 🎯 使用例

### **パターン 1: シンプルなキューブ配置**

```
設定:
- Object Prefab: デフォルト（Cube.fbx）
- Position Offset: (0, 0.1, 0)  ← QR の上 10cm
- Rotate With QR: ✓ チェック
- Object Scale: 0.15
```

**結果**: QR 上 10cm の位置に、回転追従する青いキューブが表示されます。

### **パターン 2: カスタムオブジェクト（スフィア）**

1. Hierarchy で **3D Object → Sphere** を作成
2. 不要なコンポーネント（Rigidbody）を削除
3. Inspector で **All** 表示 → **Prefabs → Drag into Project folder**
4. QRObjectPositioner の **Object Prefab** に指定

---

## 📊 動作フロー

```
【QR #1 が見える場面】

QRCodeTracker_MRUK.Update()
  ↓
GetTrackables() → QR #1 を検出
  ↓
OnQRCodeDetected(trackable)
  ↓
OnQRDetected イベント発行
  ↓
QRObjectPositioner.OnQRDetected() 呼ばれる
  ↓
✓ Object #1 を生成・配置 (QR #1 の座標)
  ↓
毎フレーム Update() で位置更新
  ↓
【QR #2 が見える場面】
  ↓
OnQRDetected イベント発行
  ↓
✓ Object #2 を生成・配置 (QR #2 の座標)
  ↓
【2 つの QR が同時に見える】
  ↓
両方のオブジェクトが対応する座標に表示される
  ↓
【QR #1 が見えなくなる】
  ↓
OnQRLost イベント発行
  ↓
✗ Object #1 を自動削除
  ↓
Object #2 だけが残る
```

---

## 🔍 デバッグ・確認

### ログ出力例

```
[QRCodeTracker_MRUK] [START] ✓ Initialization complete
[QRObjectPositioner] [START] ✓ QRObjectPositioner ready
[QRObjectPositioner] [OnEnable] ✓ Event listeners registered

[QRCodeTracker_MRUK] [QR_DETECTED] ★★★ QR CODE #1 ★★★
[QRCodeTracker_MRUK] [QR_DETECTED] ✓ OnQRDetected event invoked

[QRObjectPositioner] [QR_DETECTED] UUID: dfb9d052-ac38-15bd-0c0d-ba2263565688
[QRObjectPositioner] [QR_DETECTED] Position: (2.51, -1.32, 2.87)
[QRObjectPositioner] [QR_POSITIONED] ✓ Object created for QR: dfb9d052-ac38-15bd-0c0d-ba2263565688
[QRObjectPositioner] [QR_POSITIONED]   Name: QR_Object_dfb9d052
[QRObjectPositioner] [QR_POSITIONED]   Total QR Objects: 1

【2番目の QR が検出される】

[QRObjectPositioner] [QR_POSITIONED] ✓ Object created for QR: 7fa855f2-5a12-ea47-0c03-e4aeba0450ce
[QRObjectPositioner] [QR_POSITIONED]   Total QR Objects: 2

【1番目の QR が喪失される】

[QRCodeTracker_MRUK] [QR_LOST] QR code lost: dfb9d052-ac38-15bd-0c0d-ba2263565688
[QRObjectPositioner] [QR_LOST] UUID: dfb9d052-ac38-15bd-0c0d-ba2263565688
[QRObjectPositioner] [QR_REMOVED] ✓ Object destroyed: QR_Object_dfb9d052
[QRObjectPositioner] [QR_REMOVED]   Remaining QR Objects: 1
```

---

## 🎮 実行確認

### **期待される動作:**

1. ✅ QR コード #1 を見る → **オブジェクト #1 が表示**
2. ✅ QR コード #2 を見る（#1 も見える） → **オブジェクト #1 と #2 が同時表示**
3. ✅ QR コード #1 を隠す → **オブジェクト #1 が消える、#2 は残る**
4. ✅ QR コード #1 を見せる → **オブジェクト #1 が再表示**
5. ✅ すべてを隠す → **全オブジェクト消滅**

---

## 📝 カスタマイズ例

### **例 1: QR の正確な中心に配置（オフセットなし）**

```
Position Offset: (0, 0, 0)
```

### **例 2: QR の上 20cm、スケール大きめ**

```
Position Offset: (0, 0.2, 0)
Object Scale: 0.5
```

### **例 3: QR の前方に配置（Z軸方向）**

```
Position Offset: (0, 0, 0.15)
```

### **例 4: QR 回転に追従しない（固定方向）**

```
Rotate With QR: ☐ チェック外す
```

---

## ⚙️ スクリプト API

### **QRObjectPositioner の公開メソッド:**

```csharp
// 現在配置されているオブジェクト数を取得
int count = qrObjectPositioner.GetPositionedObjectCount();

// 全オブジェクトを手動削除
qrObjectPositioner.ClearAllObjects();

// イベントに独自の処理を追加
qrCodeTracker.OnQRDetected += (uuid, pos, rot) => 
{
    // カスタム処理
};
```

---

## 🐛 トラブルシューティング

| 問題 | 原因 | 解決策 |
|------|------|--------|
| オブジェクトが表示されない | Object Prefab が未指定 | QRObjectPositioner の Inspector で Prefab を設定 |
| オブジェクトが動かない | QRCodeTracker が見つからない | QRCodeTracker を Manual で割り当て |
| イベントが発火しない | OnEnable が呼ばれていない | GameObject がアクティブになっているか確認 |
| オブジェクトが消えない | OnQRLost イベント未発行 | QRCodeTracker の OnQRCodeLost() で OnQRLost?.Invoke() を確認 |

---

## 📌 ベストプラクティス

1. **Prefab は事前に用意**
   - ランタイム生成より Prefab 使用が推奨

2. **オフセットは慎重に**
   - 正負を確認して設定

3. **スケールは実環境で調整**
   - Quest 3S で実測して最適化

4. **複数オブジェクト同時表示時**
   - パフォーマンス監視（Draw Call 数）

---

## 📖 関連ドキュメント

- `MRUK_Reinitialization_Guide.md` - MRUK 再初期化の確認方法
- `QRCodeTracker_MRUK.cs` - QR 検出の実装詳細

---

## 完成！🎉

これで複数の QR コード上に自動的にオブジェクトが配置されるシステムが完成しました。

**次のステップ:**
- 複数 QR コード（3〜5枚）で動作確認
- カスタムオブジェクト Prefab の作成
- パフォーマンス測定と最適化
