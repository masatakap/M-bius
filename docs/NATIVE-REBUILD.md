# Native runtime rebuild / 再生ライブラリーの再ビルド

## English

This is a **separate experimental Windows x64 runtime**, not a replacement for the tested Möbius 0.8.3 files. The original `app/` directory, installer and runtime hash manifest remain the baseline. This work does not publish a GitHub Release or change the Adobe Portfolio page.

### Source provenance

`scripts/native/sources.lock.json` records 20 source archives, each with a fixed commit or release and SHA-256. It includes the libplacebo submodules separately; GitHub source snapshots do not contain gitlink contents. The selected mpv and FFmpeg revisions match the previously identified mpv application and Hap SDK sources. Other dependency revisions are new candidate inputs, not a claim about the old DLL's internals.

`fetch_sources.py` verifies hashes before extracting, rejects archive traversal and existing unmarked source directories, and saves license/notice files. All original input archives are preserved, even when a library build changes its extracted working copy. Meson fallback downloads are disabled. LuaJIT’s generated release timestamp is recorded from the locked commit. Compiler caching is optional; ccache checks compiler content, source and build flags, including when restoring a cache from an earlier source lock. The build records its compiler/packages/options, the Möbius source commit and build scripts, and obtains the distribution source package for the installed MinGW runtime. A dependency import check rejects unclassified DLL imports.

The compiler and build-tool environment is recorded rather than fully reproducibly pinned. This preparation does not guarantee byte-identical output or constitute a completed license review. Compiler runtime exceptions, per-file notices and the final source inventory must still be checked before release. Actions artifacts expire after 30 days; accepted final source archives must be preserved with the final release, not left only in Actions.

### Candidate scope and differences to check

| Area | Candidate configuration / required verification |
| --- | --- |
| Video formats | FFmpeg built-in decoders/demuxers, plus dav1d AV1; test MP4, MOV, AVI, MXF, MPEG-TS and unusual aspect ratios |
| GPU composition | mpv OpenGL render API; verify actual Möbius GPU compositor |
| Hardware decoding | Explicit D3D11VA, DXVA2, NVDEC/CUVID; verify drivers, active decode path and fallback |
| Hap | Rebuild the original `source/hap_demux.c` against the same newly built FFmpeg shared SDK; verify existing BC-texture/shader path |
| Embedded scripts | Static LuaJIT included because the current app sets mpv’s `osc` option; external Lua C modules are not validated |
| Subtitles and color | libass/DirectWrite and LittleCMS enabled; subtitle fonts and color output need checks |
| License mode | mpv GPL-only features disabled; FFmpeg GPL/nonfree/version3 options disabled. The final included-component audit is still required |
| Optional upstream features | This initial candidate omits Vulkan, shaderc/glslang, MuJS, optical-disc support, VapourSynth, several external codecs (including JPEG XL), and other optional libraries. It is not feature-equivalent to the previous full build |

The candidate must not replace the baseline just because it compiles. Review the feature differences and restore required capabilities from tracked sources before adoption. Do not describe this as supporting every format supported by the old build until tested.

### Run

Use **Actions → Native runtime candidate (manual) → Run workflow**. It runs only on manual dispatch, on Ubuntu 24.04, with read-only repository permissions. It does not run for pushes or create releases. The source/diagnostic artifact is saved on failure too; binaries are uploaded only after a successful build and source artifact upload.

Local source preparation (Python 3.12 or later):

```powershell
python scripts/native/fetch_sources.py --work-dir ../work/native-rebuild
```

Cross-compilation on Linux with the tool packages and Python environment specified in the workflow:

```sh
bash scripts/native/build-linux.sh /absolute/path/to/separate/native-work
```

For Windows testing, copy the app into a **new** test directory and replace only that copy's native runtime using the candidate. Preserve the baseline and use the application's test mode to avoid saving user preferences. Run `--verify`, the synthetic-media `--self-test`, frame-number/seek/volume tests, GPU decode checks, and shutdown tests. Installer integration and public distribution are later steps.

## 日本語

返信待ちと並行して進める、Windows x64向けの**別構成の検証用ビルド**です。使用ソース20件の版とSHA-256、libplaceboのサブモジュールを固定しました。ソース原本・著作権表示・ビルド設定・ツールチェーンの情報を保存する構成です。

GitHub Actionsから手動で実行します。既存の `app/`、インストーラー、基準DLLは変更せず、GitHub Releaseの公開も行いません。失敗時にもソースと診断ログを保存し、バイナリーはビルド成功とソース保存の後にアップロードします。

まずOpenGL合成、WindowsのGPUデコード、FFmpegの標準デコーダー、AV1、Hap読み取りを対象にします。**初期候補は既存の多機能版と同一ではありません。** Vulkan、MuJS、光学ディスク、VapourSynth、一部外部コーデックなどを省いた構成なので、必要な機能を版管理できるソースから追加し、機能差を確認してから採用します。候補のLGPL向け設定だけで配布条件の確認が完了したとは扱いません。

ビルド後に別のアプリコピーで動画形式・GPU描画・Hap・シーク・フレーム指定・音量・終了処理を検証します。最終的なライセンス表示と対応ソースを確認してから、インストーラーへ組み込みます。Actionsの保存期間は30日のため、採用版のソースはRelease等に長期保存する必要があります。
