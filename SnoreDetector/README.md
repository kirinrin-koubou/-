# いびき検知アプリ (SnoreDetector)

FFT周波数解析を使ったiOSいびき検知アプリです。

## 動作概要

- **常時マイク監視** → FFTでいびき特有の周波数帯（100〜500Hz）を解析
- **いびき検知時のみ録音保存**（検知前3秒のプリロール付き）
- 録音ファイルはアプリ内で再生・共有可能

## ファイル構成

```
SnoreDetector/
├── SnoreDetectorApp.swift   # エントリーポイント
├── AudioEngine.swift        # マイク監視・FFT解析・録音制御
├── SnoreEvent.swift         # 検知イベントのモデル
├── ContentView.swift        # メイン画面
├── EventDetailView.swift    # 録音詳細・再生画面
└── Info.plist               # マイク権限・バックグラウンド設定
```

## Xcodeでのセットアップ

1. Xcode で新規プロジェクト作成（iOS App / SwiftUI）
2. このリポジトリの `SnoreDetector/` フォルダ内のファイルをプロジェクトに追加
3. **Signing & Capabilities** で `Background Modes → Audio` を有効化
4. 実機（iPhone）でビルド・実行

## チューニングパラメータ（AudioEngine.swift）

| パラメータ | デフォルト | 説明 |
|---|---|---|
| `snoreFreqMin/Max` | 100〜500 Hz | いびき判定の周波数帯 |
| `detectionThreshold` | 0.015 | 検知感度（小さくすると敏感） |
| `confirmFrames` | 8フレーム | 誤検知防止の連続フレーム数 |
| `preRollSeconds` | 3秒 | 検知前の保存バッファ |

## 必要環境

- iOS 16.0+
- Xcode 15+
- 実機（シミュレーターはマイク非対応）
