@echo off
setlocal EnableDelayedExpansion

REM =============================================
REM 現在の作業ディレクトリを保存
set "ORIGINAL_DIR=%cd%"

REM バッチファイルのあるディレクトリに移動
cd /d "%~dp0"

REM =============================================
REM artifacts フォルダが存在していれば削除
if exist artifacts (
    rmdir /s /q artifacts
)

REM =============================================
REM csprojファイル内の <Version> タグの内容を抽出
set "CSPROJ=..\GHCodeSync.csproj"
set "VERSION="

for /f "usebackq delims=" %%A in (`findstr /R "<Version>.*</Version>" "%CSPROJ%"`) do (
    set "line=%%A"
    REM <Version>以降の文字列を取得
    set "VERSION=!line:*<Version>=!"
    REM </Version>より前の文字列に切り出す
    for /f "delims=<" %%B in ("!VERSION!") do (
        set "VERSION=%%B"
    )
    goto :FoundVersion
)
:FoundVersion
if defined VERSION (
    echo Extracted version from csproj: %VERSION%
) else (
    echo ERROR: <Version> tag not found in %CSPROJ%.
    exit /b 1
)

REM =============================================
REM manifest.yml の version 行を更新
REM ※ -replace で正規表現のマルチラインモードを有効にするため "(?m)" を付加
powershell -Command "(Get-Content -Raw 'manifest.yml') -replace '(?m)^version:.*','version: %VERSION%' | Set-Content 'manifest.yml'"

REM =============================================
REM dotnet でプロジェクトをビルド（成果物は artifacts\bin に出力）
dotnet build -p:NoCopy=true ..\GHCodeSync.csproj -c Release -o artifacts\bin
if errorlevel 1 (
    echo ERROR: dotnet build failed.
    exit /b 1
)

REM manifest.yml を artifacts\bin にコピー
copy manifest.yml artifacts\bin\ >nul
REM ※ 必要に応じて examples フォルダのコピーも追加可能
REM xcopy ..\examples artifacts\bin\examples /E /I /Y

REM =============================================
REM packaging（yak）処理
pushd artifacts\bin

REM 指定された yak.exe のパス
set "YAK_EXE=D:\Program Files\Rhino 8\System\Yak.exe"
if not exist "%YAK_EXE%" (
    echo ERROR: yak executable not found at "%YAK_EXE%".
    popd
    exit /b 1
)

REM yak build の実行
"%YAK_EXE%" build
if errorlevel 1 (
    echo ERROR: yak build failed.
    popd
    exit /b 1
)

REM カレントディレクトリ内で最初に見つかった .yak ファイルを取得
set "YAK_FILE="
for %%F in (*.yak) do (
    set "YAK_FILE=%%F"
    goto :FoundYak
)
:FoundYak
if not defined YAK_FILE (
    echo ERROR: No .yak file found.
    popd
    exit /b 1
)

REM yak push の実行
"%YAK_EXE%" push "%YAK_FILE%"
if errorlevel 1 (
    echo ERROR: yak push failed.
    popd
    exit /b 1
)

popd

REM =============================================
REM 元のディレクトリへ戻る
cd /d "%ORIGINAL_DIR%"

echo Build and packaging completed successfully.
endlocal
exit /b 0