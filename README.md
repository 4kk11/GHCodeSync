<div align="center">
    <h1>GHCodeSync</h1>
    <div>
        <a href="https://marketplace.visualstudio.com/items?itemName=ghcodesync">
            <img src="https://img.shields.io/visual-studio-marketplace/v/ghcodesync.svg?label=VSCode%20Marketplace&color=blue" alt="VSCode Marketplace">
        </a>
        <a href="https://www.food4rhino.com/en/app/ghcodesync">
            <img src="https://img.shields.io/badge/McNeel%20Packages-latest-blue" alt="McNeel Packages">
        </a>
    </div>
    <br>
    <img src="art\logo.png" alt="Logo" width="400">
</div>

## 概要
GrasshopperのC#スクリプトコンポーネントのコーディングをVSCodeで行うための連携ツールです。   
WebSocketベースの双方向通信により、VSCodeの強力な開発支援機能をGrasshopperのスクリプト開発で活用できます。

https://github.com/user-attachments/assets/22f60aaa-47c8-48e5-adde-4709fa11a6ea

## システム要件

- Rhinoceros: 8.18.25100.11001 以降
- VSCode: 1.70.0 以降

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

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。詳細は[LICENSE](LICENSE)ファイルをご覧ください。