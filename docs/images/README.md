# Möbius screenshots

- `mobius-gallery.jpg`: 実際のMöbius 0.8.3で、縦横比の違う6本のデモ動画を一覧表示した画面。
- `mobius-focus.jpg`: 同じアプリで動画をクリックして拡大した画面。下部にはシーク、FrameNumber、音量の操作部を表示。

Windows上のMöbiusを起動し、アプリのウィンドウを直接撮影しました。UIの描き直し、合成、デザイン変更は行っていません。README用にウィンドウを1020×560に調整しています。表示設定によって実際の見え方は変わります。

動画素材は、組み込みのimagegenで生成した風景・抽象画像を、FFmpegで16:9、9:16、1:1、2:1のデモMP4にしたものです。これらは静止画像を動画化した説明用素材であり、実写映像や性能測定用の動画ではありません。業務素材やユーザーの既存動画は使用していません。デモ用MP4はリポジトリに含めていません。

画面内のMöbiusの名称・ロゴ・アイコンには [TRADEMARKS.md](../../TRADEMARKS.md) が適用されます。画像をソースコードのMIT Licenseに含めるものではありません。

## デモ素材の生成プロンプト

使用方法：組み込みのimagegen（CLI/APIキーによる生成ではありません）。生成したのは動画欄の素材のみで、アプリの画面は生成していません。

```text
Use case: photorealistic-natural. Asset type: original demo video source contact sheet for a real video preview application screenshot. Generate ONE image, landscape 3:2 aspect ratio, precisely divided into a perfectly regular 3-column by 2-row grid of six equal SQUARE images. Edge-to-edge panels, NO gaps, NO borders, NO rounded corners, NO text, NO labels, NO logos, NO UI. Every panel is distinct, premium cinematic photographic footage with natural color and no people. Top left: turquoise alpine lake reflecting a mountain at dawn, centered mountain. Top middle: overhead turquoise ocean waves curving around dark basalt shore, centered curving surf. Top right: wet Japanese-style city street at blue hour with abstract warm illuminated windows, no readable signage, no logos. Bottom left: soft terracotta desert dunes with elegant shadow lines, centered sweeping dune. Bottom middle: original flowing silk-like 3D ribbons, violet and pale teal on charcoal, elegant abstract motion design. Bottom right: minimalist white concrete architectural courtyard with a central olive tree and hard late-afternoon sunlight. Compose each square so its central vertical and horizontal crops remain attractive. All six panels tack sharp, richly detailed, balanced cinematic lighting, restrained saturation. This is media artwork only, do NOT depict any computer or app.
```
