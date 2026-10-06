# Möbiusのビルド

既存のWindows / .NET 9 / WinForms / libmpv / OpenGL / Inno Setup構成を使用します。アプリの実装ファイルは公開準備で変更していません。

## 必要な開発環境

- Windows x64、.NET 9 SDK。
- ネイティブのHap読み取り部を再ビルドする場合：Visual Studio C++ Build Tools（x64、Windows SDK）とFFmpeg 8.1 shared SDKの `include/` と `lib/`。
- インストーラー作成：Inno Setup 6.7.3で確認。日本語、スペイン語、フランス語、ポルトガル語の標準言語ファイルが必要です。中国語は `installer/Languages/ChineseSimplified.isl` に収録しています。

## 管理対象

`source/` はC#本体とネイティブ読み取り部、`tests/` は合成データを使ったレイアウト・形式判定・フレーム番号計算テスト、`installer/` はインストーラーです。`source/Assets/` のブランド素材はMIT対象外です。

`app/`、`dist/`、`runtime/`、bin/obj、ユーザー動画、実行ログはGitに含めません。既存の.NET publishとネイティブビルドコマンドもそのまま使えます。

## 1. アプリ本体

リポジトリのルートで実行します。

```powershell
dotnet publish source/VideoMosaic.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:PublishSingleFile=false -p:DebugType=None -o app
dotnet run --project tests/LayoutTests.csproj -c Release
```

## 2. 再生用のネイティブ依存物

実行時には次の6ファイルを `app/Mobius.exe` と同じディレクトリに配置します。

|ファイル|由来|
|---|---|
|libmpv-2.dll|[mpv Windows builds](https://github.com/shinchiro/mpv-winbuild-cmake/releases)、x86_64 baseline版|
|avformat-62.dll、avcodec-62.dll、avutil-60.dll、swresample-6.dll|[FFmpeg shared builds](https://github.com/BtbN/FFmpeg-Builds/releases)、FFmpeg 8.1 win64 LGPL shared版|
|hap_demux.dll|このリポジトリの `source/hap_demux.c` からビルド|

基準にしたlibmpvは `mpv-dev-x86_64-20260903-git-69e63f425a.7z`、FFmpegは `ffmpeg-n8.1-latest-win64-lgpl-shared-8.1.zip` です。基準DLLのハッシュは [native-runtime-sha256.json](native-runtime-sha256.json) に記載しています。上流の `latest` は変化するため、同名ファイルでも同一内容とは限りません。

DLLはこのリポジトリに同梱していません。入手元とライセンスの詳細は [THIRD-PARTY.md](../THIRD-PARTY.md) を参照してください。公開バイナリー配布には、使用したバイナリーに対応するソース・ライセンス表示の整備が必要です。

Hap読み取り部をビルドする場合は、Visual Studioの **x64 Native Tools Command Prompt** を開き、リポジトリのルートで実行します。`app/` は上のpublishで作成済みである必要があります。

```bat
source\build-native.cmd "C:\dependencies\ffmpeg-shared-sdk"
```

これは `app/hap_demux.dll` を生成します。FFmpeg shared SDKの `bin/` から上記4つの共有DLLも配置してください。SDK・DLLのアーキテクチャとメジャーバージョンを揃えます。

## 3. 一括ビルドと固定名インストーラー

準備済みの6つのDLLを `runtime/` に置き、PowerShell 7から実行します。

```powershell
./scripts/Build.ps1 -NativeRuntimeDirectory ./runtime -Compiler 'C:/Program Files (x86)/Inno Setup 6/ISCC.exe'
```

結果は `app/` と **`dist/Mobius-Setup.exe`** です。`-Compiler` を省略するとアプリ本体とDLLの配置まで行います。既に `app/` を用意した場合は次のコマンドでもインストーラーだけ生成できます。

```powershell
./installer/Build.ps1 -Compiler 'C:/Program Files (x86)/Inno Setup 6/ISCC.exe'
```

アプリのバージョンは `source/VideoMosaic.csproj` の `Version` からインストーラーへ渡します。公開ファイル名はバージョンによらず `Mobius-Setup.exe` です。インストール、レジストリ変更、Git操作、公開はビルド中には行いません。

## 4. 動作確認

```powershell
./app/Mobius.exe --verify
./app/Mobius.exe --self-test "C:/test-media/01_landscape.mp4" "C:/test-media/02_portrait.mov"
```

`--verify` は言語・圧縮処理等の確認です。`--self-test` の全項目には、横長・縦長・正方形・2:1・21:9・SAR・回転・Hap等の合成素材セットが必要です。任意の2本だけでは全項目は通過しません。素材はリポジトリに含めていません。

GPUを使用する検証は実際のウィンドウを開きます。検証用プロセスではユーザーの設定を保存しません。結果のJSONは実行ファイルの隣に出力され、Gitおよびインストーラーの収録対象から除外します。
