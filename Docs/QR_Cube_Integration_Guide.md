# QR 認識時の Cube 色変更 - 統合ガイド

## 概要
QR コードが認識されたとき、Hierarchy に配置した **Cube** オブジェクトの色を自動的に変更する機能です。

既存の `QRDetector_ActiveScan.cs` を拡張し、新しいアンカー検出時に `CubeColorOnQr.cs` に通知して色を変えます。

---

## ファイル構成

### 1. `CubeColorOnQr.cs` （新規作成）
**場所**: `Assets/Scripts/CubeColorOnQr.cs`

Cube にアタッチするメインのコンポーネント。QR 認識イベントを受け取り、Cube の色を変更します。

**主なメソッド**:
- `OnQrRecognized(string qrText)`: QRDetector から呼ばれる。QR の UUID をもとに色を変更。
- `ResetToDefault()`: Cube の色をデフォルトにリセット。QRDetector の Start で呼ばれます。
- `TestQrDetection()`: Inspector のコンテキストメニューから呼べるテスト用メソッド。

**Inspector 設定**:
- `Default Color`: QR 検出なし時の初期色（デフォルト: 白）
- `Use Color From Text`: ON の場合、QR の UUID から決定論的に色を生成（ON 推奨）

---

### 2. `QRDetector_ActiveScan.cs` （既存・拡張）
**場所**: `Assets/Scripts/QRDetector_ActiveScan.cs`

既存のアクティブ QR スキャナに以下の機能を追加しました:

**新規フィールド**:
- `targetCube`: Cube オブジェクトの参照。Inspector から設定します。

**新規処理**:
- `Start()` で Cube の初期色をリセット
- `OnNewAnchorDetected()` で新規 QR 検出時に `CubeColorOnQr.OnQrRecognized()` を呼び出し

**ログ出力**:
- `[Cube Color] Notified targetCube to change color on QR detection`

---

## セットアップ手順

### ステップ 1: Cube を Hierarchy に配置
1. Unity Editor で Hierarchy を開く
2. **右クリック** → **3D Object** → **Cube** を選択
3. Cube が Scene に追加されます（デフォルト位置で OK）

### ステップ 2: Cube に `CubeColorOnQr` をアタッチ
1. Hierarchy で作成した **Cube** を選択
2. Inspector の **Add Component** ボタンをクリック
3. **CubeColorOnQr** を検索してアタッチ
4. Inspector で以下を設定:
   - **Default Color**: 希望の初期色（例: 白）
   - **Use Color From Text**: ON（推奨）

### ステップ 3: QRDetector_ActiveScan に Cube を設定
1. Hierarchy で QRDetector_ActiveScan がアタッチされているオブジェクトを選択
   （通常は Manager や Camera オブジェクト）
2. Inspector で `QRDetector_ActiveScan` コンポーネントを見つける
3. **Target Cube** フィールドに、ステップ 1 で作成した **Cube** をドラッグ & ドロップ

### ステップ 4: 検証
1. Play を押す
2. QR コードを認識させる（または Inspector で `CubeColorOnQr` コンポーネントの **Test QR Detection** を実行）
3. Cube の色が変わることを確認

---

## 動作フロー

```
┌─────────────────────────────────────────┐
│ QRDetector_ActiveScan                   │
│ - QR アンカアー検出（監視中）           │
└─────────┬───────────────────────────────┘
          │
          ↓ [新規 QR 検出]
┌─────────────────────────────────────────┐
│ OnNewAnchorDetected()                    │
│ - UUID 取得                              │
└─────────┬───────────────────────────────┘
          │
          ↓ [targetCube.GetComponent]
┌─────────────────────────────────────────┐
│ CubeColorOnQr.OnQrRecognized(UUID)      │
│ - UUID →色生成                          │
│ - Renderer.material.color = 新色        │
└─────────────────────────────────────────┘
          │
          ↓
      ┌─────────┐
      │  CUBE   │ ← 色が変わる！
      └─────────┘
```

---

## 色の決定方法

**Use Color From Text = ON の場合**:
- QR の UUID のハッシュ値から R, G, B 値を生成
- 同じ UUID は常に同じ色になります（決定論的）
- 異なる UUID は異なる色になります（ほぼ確定）

**Use Color From Text = OFF の場合**:
- Inspector で設定した `Default Color` を常に使用

---

## テスト方法

### 方法 1: Inspector コンテキストメニュー
1. Play 中に Hierarchy で **Cube** を選択
2. Inspector で `CubeColorOnQr` コンポーネントを見つける
3. コンポーネント右上の **⋮** メニュー → **Test QR Detection** をクリック
4. Cube の色が変わることを確認

### 方法 2: QRDetector_ActiveScan の pollInterval を短縮
1. `QRDetector_ActiveScan` の Inspector で **Poll Interval** を `0.1` に変更
2. QR コードを繰り返しスキャン（またはカメラを向け直す）
3. 色が変わることを確認

---

## よくある問題と解決法

### Q: Cube の色が変わらない
- `targetCube` が Inspector で正しく設定されているか確認
- `CubeColorOnQr` が Cube にアタッチされているか確認
- Console でログを確認:
  - `[CubeColorOnQr] QR recognized: ...` が出ているか？
  - `[Cube Color] Notified targetCube ...` が出ているか？
- Renderer が Cube に存在するか確認（通常は自動）

### Q: コンパイルエラーが出る
- `CubeColorOnQr.cs` ファイルが `Assets/Scripts/` に存在するか確認
- Unity が新規ファイルをコンパイルするまで 1-2 秒待機
- 必要に応じて **Assets** → **Reimport All** を実行

### Q: 色が毎回異なる
- 通常の動作です。UUID が異なると色も異なります
- 同じ UUID なら同じ色になることを確認するには、QR コードを繰り返しスキャンしてください
- 固定色にしたい場合は `Use Color From Text = OFF` に設定

### Q: Material の色が複数オブジェクトに影響する
- 複数の Cube が同じマテリアルを共有している場合の現象です
- 各 Cube ごとに別々にマテリアルをインスタンス化することで解決します
  （CubeColorOnQr.cs の Start() に以下を追加）:
  ```csharp
  cubeRenderer.material = new Material(cubeRenderer.material);
  ```

---

## 拡張例

### 例 1: 複数の Cube を異なる QR で制御
- 各 Cube に `CubeColorOnQr` をアタッチ
- `QRDetector_ActiveScan` を複数作成するか、1 つの QRDetector から複数 Cube に通知する

### 例 2: フェード効果の追加
- `CubeColorOnQr.cs` の `OnQrRecognized()` を以下のように拡張:
  ```csharp
  StartCoroutine(FadeColor(newColor, fadeDuration: 1f));
  ```

### 例 3: サウンドエフェクト
- `OnQrRecognized()` で AudioSource を再生

---

## ノート

- `CubeColorOnQr` は Cube オブジェクトのみではなく、任意のオブジェクトにアタッチ可能です
- QR の UUID から自動生成される色は、同一の QR であれば常に一貫しています
- 本実装は Oculus Spatial Anchor API（Meta XR SDK）を使用しています

---

**最終更新**: 2025-12-10
