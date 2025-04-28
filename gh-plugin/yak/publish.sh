#!/bin/bash

set -e

# 現在のディレクトリを保存
ORIGINAL_DIR=$(pwd)
# 現在のスクリプトのディレクトリに移動
cd "$(dirname "$0")"

# 初期化
rm -rf ./artifacts

# csproj 内の <Version> タグを取得し VERSION 変数に代入
VERSION=$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' ../GHCodeSync.csproj)
echo "Extracted version from csproj: $VERSION"

# manifest.yml のバージョンを更新
sed -i '' "s/version: .*/version: ${VERSION}/" ./manifest.yml # macOS

# ビルド
dotnet build -p:NoCopy=true ../GHCodeSync.csproj -c Release -o ./artifacts/bin

# コピー
cp -r ./manifest.yml ./artifacts/bin
# cp -r ../examples ./artifacts/bin

# パッケージング
cd ./artifacts/bin
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" build

# 生成されたyakファイルの名前を取得
YAK_FILE=$(find . -maxdepth 1 -name "*.yak" -print -quit)

if [ -z "$YAK_FILE" ]; then
    echo "No .yak file found."
    exit 1
fi

# パッケージをアップロード
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" push "$YAK_FILE"

# 元のディレクトリに戻る
cd "$ORIGINAL_DIR"