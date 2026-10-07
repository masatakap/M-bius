# Native dependency sources / ネイティブ依存物のソース

## English

Status checked on **2026-10-08**: **source preparation is incomplete**. The Möbius 0.8.3 installer is uploaded to a private release draft in this public repository. It has not been published; the `releases/latest/download/Mobius-Setup.exe` URL does not work yet.

### Confirmed and collected

The five source archives below have been downloaded locally and checked for readable archive contents. Their full revisions, download URLs, sizes and SHA-256 hashes are in [native-source-audit.json](native-source-audit.json). These are top-level source snapshots, **not a complete corresponding-source distribution**. GitHub snapshots do not automatically include submodule contents.

| Component | Source revision | Identification |
| --- | --- | --- |
| libmpv | `69e63f425a531f814431fba12750bdb3721357f2` | Runtime `mpv-version` property |
| FFmpeg embedded in libmpv | `9fc8c785e2747c87121ec28f8f10ceab0562384b` | Runtime `ffmpeg-version` property |
| mpv build recipes | `cd1edc11dc6887a50f705717619d879f5a93a488` | [Upstream build run](https://github.com/shinchiro/mpv-winbuild-cmake/actions/runs/33697571182) |
| FFmpeg shared SDK used for Hap | `1005b294ffdf1b4e75f58d7e98362f82462a6a61` | Original SDK `ffmpeg.exe -version` |
| FFmpeg build recipes | `3e6685eda92f9288c15ac320139622dcedca09a4` | [Upstream build run](https://github.com/BtbN/FFmpeg-Builds/actions/runs/34967781095) |

The installed native files match [native-runtime-sha256.json](native-runtime-sha256.json). Native binaries and application behavior have not changed during this investigation.

### What is still missing

1. **libmpv dependency revisions.** Several recipes use moving branches. The available log does not identify every source revision included in the DLL. An [existing upstream issue for this exact DLL](https://github.com/shinchiro/mpv-winbuild-cmake/issues/848) has no maintainer reply as of the check date. Do not replace unknown revisions with today's branch heads or infer them solely from commit dates.
2. **Dependency sources and notices.** Collect the sources and applicable notices for the actual included components of both builds, including submodules, patches and build/install instructions. BtbN recipes contain many pinned revisions, but the corresponding source collection is not yet complete. The original FFmpeg workflow log download returns HTTP 410; identifying its workflow commit does not establish every dependency image input.
3. **Final packaging.** Once the inventory is complete, package the notices with the installer and provide the source archives with the binary release. Rebuild and check the final installer before replacing the draft asset, then publish and test the anonymous download URL.

### Ways to finish

- Obtain the missing source/version information from the binary provider. This preserves the tested runtime. A [source inquiry](https://github.com/shinchiro/mpv-winbuild-cmake/issues/848#issuecomment-6041669942) was posted from `masatakap` on 2026-10-08 (JST). It asks for the missing source archive or dependency revision manifest, or an available alternative build with that information. Awaiting a maintainer response.
- If the original sources cannot be identified, build the runtime from recorded source revisions and preserve the sources and notices as part of that build. Stage it separately, retain the tested runtime, and verify codecs, GPU rendering, Hap, seeking and installation before adopting it.

The current installer SHA-256 is `957075ac386edd56de74570e95ee3612f25f63218183e9e1df1aa0386a8520dd`. This hash verifies the draft file only; it does not indicate that source preparation is complete.

## 日本語

**2026-10-08時点では、依存物のソースの準備は未完了です。** Möbius 0.8.3のインストーラーはGitHub Releaseの下書きにアップロード済みですが、一般公開していません。最新版の固定ダウンロードURLはまだ使用できません。

mpv本体、そこに組み込まれたFFmpeg、Hap用FFmpeg、および両方のビルド手順について、版を特定して5個のソースアーカイブをローカルに確保しました。版・URL・容量・SHA-256は [native-source-audit.json](native-source-audit.json) に記録しています。ライブラリーのバージョンは実際のDLLと元のSDKからも確認しました。

ただし、これだけでは依存物を含む完全な対応ソースになりません。主な未解決点は、**libmpvへ組み込まれた依存ライブラリーすべての版が特定できていないこと**です。同じDLLについて[配布元への既存の問い合わせ](https://github.com/shinchiro/mpv-winbuild-cmake/issues/848)もありますが、確認時点で配布元からの回答はありません。現在の最新版や日付から推測した版を、使用された版として扱いません。

2026-10-08（日本時間）に、`masatakap` から[不足情報の問い合わせ](https://github.com/shinchiro/mpv-winbuild-cmake/issues/848#issuecomment-6041669942)を送信しました。必要なソース一式または依存物の版一覧、もしくはそれらを入手できる別のビルドがあるかを確認しています。現在は配布元の回答待ちです。

配布元から不足情報を得て現在の版を維持するか、ソースの版を記録した構成で再ビルドする必要があります。再ビルドする場合は別の作業用フォルダーで進め、動画形式・GPU描画・Hap・シーク・インストールを再検証してから採用します。調査中にアプリ本体や既存のDLLは変更していません。

依存物のソース、サブモジュール、パッチ、ライセンス表示、ビルド手順が揃ったら、表示を同梱したインストーラーを再生成し、ソースアーカイブとともに公開します。公開後に、GitHubへログインしていない状態でもダウンロードできることを確認します。
