# Möbiusの配布

ソースコードと一般ユーザー向け配布は、Publicリポジトリ `masatakap/M-bius` に一元化します。別のダウンロード専用リポジトリは作りません。

## 現在の段階

アプリ本体0.8.3、中国語を含むインストーラー改訂2を基準にソースを公開します。ここに記載するRelease操作は今後の公開手順です。ソースのpushやローカルのインストーラー作成だけでは、GitHub Releaseや最新版リンクは作成されません。

現在のネイティブ依存物にはGPL/LGPLのバイナリーを含みます。MITは独自ソースに適用し、配布物全体のライセンスを置き換えるものではありません。バイナリー公開前に、実際に同梱する版に対応したソース、ビルド情報、ライセンス表示を揃えてください。上流サイトへのリンクだけで対応ソースの準備が完了したことにはしません。[THIRD-PARTY.md](../THIRD-PARTY.md)を参照してください。

## 手動リリース

1. `source/VideoMosaic.csproj` の `Version`、CHANGELOG、既知の問題を更新する。
2. [ビルド手順](BUILD.md)でアプリと `dist/Mobius-Setup.exe` を生成する。
3. 再生・インストール・更新を検証し、上記の依存物の配布準備を完了する。
4. 公開用ファイルに秘密情報、業務素材、個人情報、PC固有のログが含まれないことを確認する。
5. 対応するソースcommitに `v1.0.0` 等のバージョンタグを作る。開発版なら `v0.8.3` のように実際の版を使用し、任意に1.0.0へ読み替えない。
6. GitHubのReleasesで、そのタグからリリースを作成する。バージョン、日付、追加機能、修正内容、既知の問題を記載する。
7. **`Mobius-Setup.exe`** を同じ名前で添付する。SHA-256と、配布に必要な依存物の対応ソース／表示も添付または適切な提供方法で揃える。
8. 正式公開版をLatestとして公開し、固定URLがインストーラーを返すことを確認する。DraftやPrereleaseでは正式最新版へのリンクとして使わない。

PowerShellでのハッシュ作成例：

```powershell
$hash = (Get-FileHash ./dist/Mobius-Setup.exe -Algorithm SHA256).Hash
"$hash  Mobius-Setup.exe" | Set-Content ./dist/SHA256SUMS.txt -Encoding utf8
```

## Adobe Portfolio

公式サイトの大きな **「Möbiusを無料ダウンロード」** ボタンへ、次のURLを設定します。

```text
https://github.com/masatakap/M-bius/releases/latest/download/Mobius-Setup.exe
```

正式な最新版Releaseに同名の添付ファイルを毎回置くことで、公式サイト側のURLを変更する必要がなくなります。一般ユーザーにGitHubの操作やアカウント作成を要求しません。初回公開前はこのURLを有効な配布先として案内しないでください。

## 将来のGitHub Actions

Windows runnerで.NET SDK、Visual Studio C++、Inno Setup、版とハッシュを固定したネイティブ依存物を用意し、テスト → publish → ネイティブ部のビルド → インストーラー → Release添付へ進む構成を検討します。必要な段階で導入し、現時点では自動公開workflowを追加しません。

GPUの実機検証は通常のホステッドrunnerだけで代替しません。公開処理はビルドとは分け、リリース時に必要な権限だけを与えます。秘密鍵・アクセストークンはリポジトリへ保存せず、必要になった時点でGitHub側のSecret等を使用します。
