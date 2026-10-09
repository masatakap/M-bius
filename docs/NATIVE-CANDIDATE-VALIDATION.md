# Native runtime candidate validation / 再生ライブラリー候補の検証

## English

**Status: built and tested on one Windows machine; not approved for public distribution.**

On 2026-10-09, the separately rebuilt runtime completed the tests below in a copy of Möbius 0.8.3. Application source code was unchanged. The original six native DLLs and existing installer retained their recorded SHA-256 hashes. No installer integration, GitHub Release publication or website change was performed.

### Build and retained evidence

- Build commit: `8072b5a1e9b0356d6b2511327c679241e484330c`.
- Successful [GitHub Actions run 37881999018](https://github.com/masatakap/M-bius/actions/runs/37881999018).
- 20 locked source archives; all 97 file hashes in the candidate manifest verified after downloading the artifacts.
- Original source archives, applied patch, build scripts, configuration, notices and matching MinGW runtime source package preserved locally with both downloaded artifacts.

| Artifact | SHA-256 |
| --- | --- |
| `native-candidate-binaries-37881999018.zip` | `764a24d13f82a8f20f0dd5abce6ee098e85962af999ff00d29940a293f164cf1` |
| `native-candidate-37881999018.zip` | `64d3efe41cc4842862bf258e7df131e08e6adf87d1ac310fea828740631af27f` |

Runtime identifiers:

- `libmpv-2.dll`: `cc748135492958fd5403c1accb4371307ff138e7650341668119d1664ad6c0f2`.
- `hap_demux.dll`: `1635d3941f8c2405c40f59fe8b2b4a4fa7f0d47f736d2777069419c52c5008ff`.
- FFmpeg/ffprobe report `8.1.2-mobius-experimental`; mpv client API reports 2.5.

Actions artifacts expire after 30 days. They are validation artifacts, not a permanent corresponding-source distribution. Accepted release sources and notices must be published and retained with the eventual release. The CI manifest's `windows_playback_verified: false` describes its state at packaging time; this subsequent report does not modify the archived manifest.

### Test environment and results

Windows 11 Pro, build 26300, x64; NVIDIA GeForce RTX 4070 Ti, driver 32.0.16.1656. A Parsec virtual display adapter was also present. These results do not establish compatibility with other GPUs, drivers or Windows 10.

| Check | Result |
| --- | --- |
| Native startup | libmpv loads, accepts the application's startup options, initializes and shuts down; Hap exports present; FFmpeg and ffprobe start successfully |
| `--verify` | 96/96 without media; 114/114 including four Hap samples (the latter includes the original 96 checks) |
| `--self-test`, 12 synthetic files | Follow-up run: 55/55; GPU compositor active; all eight Hap GPU format/pixel checks pass |
| Four-format `--media-test` | All 21 stages pass: H.264 MP4, Hap MOV, fractional-rate MPEG-4 MP4, and an MPEG-TS stream with an `.mp4` filename |
| MXF `--media-test` | All 21 stages pass across four 1080p/30 samples using MPEG-2 4:2:2 or DNxHD, including uppercase extensions and a Japanese filename |
| Exit after loading 12 videos | Success; window hidden in 17.43 ms; cleanup 923.26 ms; no remaining GPU engines; all disposals completed |
| Exit during loading | Success; window hidden in 12.22 ms; cleanup 43.39 ms; no remaining GPU engines; all disposals completed |

Media tests cover advancing playback, pause, seek to 60%, return to the start, frame 120, holding the requested frame paused, and invalid/out-of-range frame input. They also check the existing control-bar layout and appearance. H.264 reported active `nvdec`; Hap reported `GPU BC texture + shader`. MPEG-4, MPEG-2 4:2:2 and DNxHD samples used software decoding. The MPEG-TS sample successfully used the application's existing fallback. These checks are not a performance benchmark or a claim that every codec has GPU decoding.

**Initial failure retained:** the first candidate self-test passed 54/55 checks, failing `chrome-idle-release-hides` (automatic hiding of the controls). No GPU failure was reported. A separate copy with the original runtime then passed 55/55, and the candidate repeated the test successfully at 55/55 without application changes. The initial failure's cause is undetermined; it must not be described as a confirmed fix. Local reports retain the initial failure, baseline control and candidate rerun.

### Before adopting this runtime

1. Complete the included-component, per-file notice and compiler-runtime license review. FFmpeg's LGPL 2.1-or-later runtime string and disabled GPL/nonfree build flags alone do not establish distribution compliance.
2. Review the [candidate's feature differences](NATIVE-REBUILD.md#candidate-scope-and-differences-to-check), including omitted optional upstream libraries. Subtitle/color output and external Lua C modules still need verification if required.
3. Perform representative user-media and additional hardware checks. Sony X-OCN support is unchanged; the synthetic MXF tests do not establish support for every MXF codec.
4. Preserve final corresponding sources, patches, notices and build information in permanent release assets; then integrate and test the installer separately.

Raw local reports and private media filenames are not included in this public document.

## 日本語

**状態：別構成の再ビルドとWindows実機での検証が完了。一般公開用への採用はまだです。**

2026年10月9日、Möbius 0.8.3の検証用コピーで確認しました。アプリ本体のソースコードは変更していません。既存のDLL 6個とインストーラーはSHA-256の一致を確認し、元の状態を維持しています。インストーラーへの組み込み、Releaseの公開、Webサイトの変更は行っていません。

ビルドしたコミット、成功したActions実行、成果物のハッシュは上の表に記載しています。固定したソース20件と、成果物一覧の97ファイルすべてのハッシュを検証しました。ソース原本、変更パッチ、ビルド設定、ライセンス表示、MinGWランタイムの対応ソースもローカルに保存しています。Actionsの保存期間は30日のため、正式採用時には長期保存できる配布先へ移す必要があります。

検証機はWindows 11 Pro／RTX 4070 Tiです。主な結果は次のとおりです。

- 基本検証は96項目、Hap素材を加えた検証は基本分を含む114項目が成功しました。
- 12本の動画を使った統合テストは再実行で55項目すべて成功し、GPU合成とHapのGPU描画も確認できました。
- 通常のMP4、Hap、29.97fpsの素材、拡張子がMP4で実体がMPEG-TSの素材について、再生・シーク・フレーム指定など21段階の検証が成功しました。
- MPEG-2 4:2:2／DNxHDのMXF 4本も21段階の検証が成功しました。大文字の拡張子と日本語ファイル名を含みます。
- 12本読み込み後の終了では、約17msで画面が消え、約923msで後処理が完了しました。読み込み途中の終了も成功し、両方ともGPU描画エンジンは残っていません。単発の測定値であり、速度改善の比較結果ではありません。

H.264はNVDEC、HapはBCテクスチャとシェーダーのGPU経路を確認しました。今回のMPEG-4、MPEG-2 4:2:2、DNxHDはソフトウェアデコードでした。

**最初の統合テストでは、メニューの自動非表示1項目が失敗しました。** その後、元のライブラリーによる比較テストと、候補版の再実行は両方とも全項目成功しています。最初の失敗原因は未特定で、修正済みとは扱っていません。初回・比較・再実行の記録を残しています。

残る作業は、同梱部品とライセンス表示の最終確認、省略した機能との差分確認、必要に応じた字幕・色表示や他のGPUでの検証、対応ソースの長期公開、インストーラーへの組み込みと検証です。**LGPL向け設定でビルドできたことだけで、配布条件の確認がすべて完了したとは判断していません。** また、Sony X-OCNへの対応は従来どおりで、今回のMXF検証は全MXF形式への対応を意味しません。
