# GHCodeSync

[![VSCode Marketplace](https://img.shields.io/visual-studio-marketplace/v/ghcodesync.svg?label=VSCode%20Marketplace&color=blue)](https://marketplace.visualstudio.com/items?itemName=ghcodesync)
[![McNeel Packages](https://img.shields.io/badge/McNeel%20Packages-latest-blue)](https://www.food4rhino.com/en/app/ghcodesync)

<div align="center">
   <img src="art\logo.png" alt="Logo" width="500">
</div>

Grasshopperのスクリプトコンポーネント開発をVSCodeで行うための連携ツールです。WebSocketベースの双方向通信により、VSCodeの強力な開発支援機能をGrasshopperのスクリプト開発で活用できます。

## 概要

本ツールは、Grasshopperの組み込みエディタの制限（基本的な自動補完のみ、AI支援機能なし、高度なコード分析ツールの不在など）を解決し、現代的な開発環境でのスクリプト開発を可能にします。VSCodeの強力な機能群を活用することで、より効率的で品質の高いパラメトリックデザインの開発を実現します。

## 使用方法

### セットアップ手順

1. **プラグインのインストール**
   - VSCode拡張: Visual Studio Code MarketPlaceから「GHCodeSync」をインストール
   - Grasshopperプラグイン: McNeel Packagesから「GHCodeSync」をインストール

2. **スクリプト編集**
   - Grasshopper上でC#スクリプトコンポーネントを選択
   - ツールバーから「Open with VSCode」ボタンをクリック
   - VSCode上でスクリプトを編集
   - 保存（Ctrl+S）するとGrasshopperに自動で反映される


## 開発者向け情報

### ビルド方法

#### VSCode拡張
```bash
cd vscode-plugin
npm install
npm run compile
```

#### Grasshopperプラグイン
```bash
cd gh-plugin
dotnet build
```

### デバッグ方法

1. VSCode拡張のデバッグ
   - F5キーでデバッグ用のVSCodeインスタンスを起動
   - 拡張機能のデバッグコンソールで通信ログを確認

2. Grasshopperプラグインのデバッグ
   - Visual StudioでGHCodeSync.slnを開く
   - デバッグ設定：Rhinoのパスを指定
   - デバッグ実行（F5）

## 注意事項

- Grasshopperのバージョン要件: 7.0以上
- .NET Framework 4.8以上が必要
- ファイアウォールでWebSocket通信（デフォルトポート:8080）が許可されていることを確認

## ライセンス

MIT License