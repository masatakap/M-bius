#!/usr/bin/env bash
# Experimental cross-build. Never installs into the existing Windows application.
set -euo pipefail
REPO=$(cd "$(dirname "$0")/../.." && pwd)
WORK=$(realpath -m "${1:?Pass a separate working directory}")
case "$WORK" in /|"$REPO"|"$REPO/app"|"$REPO/dist") echo 'Unsafe work directory' >&2; exit 1;; esac
mkdir -p "$WORK"/{build,prefix,runtime,logs,provenance,toolchain-sources}
exec > >(tee "$WORK/logs/build.log") 2>&1
PREFIX="$WORK/prefix"
SRC="$WORK/src"
JOBS=${JOBS:-4}
export SOURCE_DATE_EPOCH=1789528738
export CC=x86_64-w64-mingw32-gcc-posix
export CXX=x86_64-w64-mingw32-g++-posix
export AR=x86_64-w64-mingw32-ar
export RANLIB=x86_64-w64-mingw32-ranlib
export PKG_CONFIG_LIBDIR="$PREFIX/lib/pkgconfig:$PREFIX/share/pkgconfig"
export PKG_CONFIG_PATH="$PKG_CONFIG_LIBDIR"
export CFLAGS="-O2 -march=x86-64 -mtune=generic -I$PREFIX/include"
export CXXFLAGS="$CFLAGS"
export LDFLAGS="-L$PREFIX/lib -static-libgcc -static-libstdc++"
python3 "$REPO/scripts/native/fetch_sources.py" --work-dir "$WORK"
git -C "$REPO" rev-parse HEAD > "$WORK/provenance/application-commit.txt"
git -C "$REPO" archive --format=tar.gz -o "$WORK/provenance/mobius-build-sources.tar.gz" HEAD
dpkg-query -W > "$WORK/provenance/build-host-packages.txt"
"$CC" --version > "$WORK/provenance/compiler.txt"
python3 -m pip freeze > "$WORK/provenance/python-build-tools.txt"

cat > "$WORK/cross.ini" <<EOF
[binaries]
c = '$CC'
cpp = '$CXX'
ar = '$AR'
strip = 'x86_64-w64-mingw32-strip'
windres = 'x86_64-w64-mingw32-windres'
dlltool = 'x86_64-w64-mingw32-dlltool'
pkg-config = 'pkg-config'
[host_machine]
system = 'windows'
cpu_family = 'x86_64'
cpu = 'x86_64'
endian = 'little'
[properties]
needs_exe_wrapper = true
[built-in options]
c_args = ['-O2', '-march=x86-64', '-I$PREFIX/include']
cpp_args = ['-O2', '-march=x86-64', '-I$PREFIX/include']
c_link_args = ['-L$PREFIX/lib', '-static-libgcc', '-static-libstdc++']
cpp_link_args = ['-L$PREFIX/lib', '-static-libgcc', '-static-libstdc++']
EOF
cat > "$WORK/toolchain.cmake" <<EOF
set(CMAKE_SYSTEM_NAME Windows)
set(CMAKE_C_COMPILER $CC)
set(CMAKE_CXX_COMPILER $CXX)
set(CMAKE_RC_COMPILER x86_64-w64-mingw32-windres)
set(CMAKE_FIND_ROOT_PATH "$PREFIX" /usr/x86_64-w64-mingw32)
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_PACKAGE ONLY)
EOF
meson_build() {
    local name=$1; shift
    meson setup "$WORK/build/$name" "$SRC/$name" --cross-file "$WORK/cross.ini" \
        --prefix "$PREFIX" --libdir lib --buildtype release --default-library static \
        --wrap-mode=nofallback -Dauto_features=disabled -Dprefer_static=true "$@"
    meson compile -C "$WORK/build/$name" -j "$JOBS"
    meson install -C "$WORK/build/$name"
}
cmake_build() {
    local name=$1; shift
    cmake -S "$SRC/$name" -B "$WORK/build/$name" -G Ninja \
        -DCMAKE_TOOLCHAIN_FILE="$WORK/toolchain.cmake" -DCMAKE_INSTALL_PREFIX="$PREFIX" \
        -DCMAKE_BUILD_TYPE=Release -DBUILD_SHARED_LIBS=OFF "$@"
    cmake --build "$WORK/build/$name" --parallel "$JOBS"
    cmake --install "$WORK/build/$name"
}
cmake_build zlib -DZLIB_BUILD_EXAMPLES=OFF
mkdir -p "$WORK/build/libiconv"
(cd "$WORK/build/libiconv" && "$SRC/libiconv/configure" --host=x86_64-w64-mingw32 \
    --prefix="$PREFIX" --disable-shared --enable-static --disable-nls && make -j"$JOBS" && make install)
cmake_build freetype -DFT_DISABLE_ZLIB=FALSE -DFT_DISABLE_BZIP2=TRUE -DFT_DISABLE_PNG=TRUE \
    -DFT_DISABLE_HARFBUZZ=TRUE -DFT_DISABLE_BROTLI=TRUE
meson_build fribidi -Ddocs=false -Dbin=false -Dtests=false
meson_build harfbuzz -Dtests=disabled -Ddocs=disabled -Dutilities=disabled \
    -Draster=disabled -Dvector=disabled -Dgpu=disabled -Dsubset=disabled
(cd "$SRC/libunibreak" && NOCONFIGURE=1 ./autogen.sh)
mkdir -p "$WORK/build/libunibreak"
(cd "$WORK/build/libunibreak" && "$SRC/libunibreak/configure" --host=x86_64-w64-mingw32 \
    --prefix="$PREFIX" --disable-shared --enable-static && make -j"$JOBS" && make install)
meson_build libass -Ddirectwrite=enabled -Dasm=enabled -Dlibunibreak=enabled
meson_build lcms2 -Dtests=disabled
meson_build dav1d -Denable_tools=false -Denable_tests=false -Denable_examples=false
make -C "$SRC/nv-codec-headers" PREFIX="$PREFIX" install
meson_build libplacebo -Dopengl=enabled -Dlcms=enabled -Ddemos=false -Dtests=false \
    -Dvulkan=disabled -Dd3d11=disabled -Dshaderc=disabled -Dglslang=disabled

mkdir -p "$WORK/build/ffmpeg"
(cd "$WORK/build/ffmpeg" && "$SRC/ffmpeg/configure" --prefix="$PREFIX" \
    --target-os=mingw32 --arch=x86_64 --enable-cross-compile --cross-prefix=x86_64-w64-mingw32- \
    --cc="$CC" --cxx="$CXX" --pkg-config=pkg-config --pkg-config-flags=--static \
    --disable-autodetect --disable-gpl --disable-nonfree --disable-version3 \
    --enable-shared --disable-static --disable-doc --disable-ffplay \
    --enable-zlib --enable-iconv --enable-libdav1d --enable-ffnvcodec \
    --enable-nvdec --enable-cuvid --enable-nvenc --enable-d3d11va --enable-dxva2 \
    --enable-schannel --extra-cflags="$CFLAGS" --extra-ldflags="$LDFLAGS" \
    --extra-version=mobius-experimental \
    && make -j"$JOBS" && make install)
meson_build mpv --default-library=shared -Dgpl=false -Dlibmpv=true -Dcplayer=true \
    -Dbuild-date=false -Dgl=enabled -Dgl-win32=enabled -Dplain-gl=enabled \
    -Dgl-dxinterop=enabled -Dgl-dxinterop-d3d9=enabled -Dvector=enabled \
    -Dd3d-hwaccel=enabled -Dd3d9-hwaccel=enabled \
    -Dcuda-hwaccel=enabled -Dcuda-interop=enabled -Dwasapi=enabled \
    -Dwin32-threads=enabled -Dwin32-smtc=disabled -Diconv=enabled \
    -Dlcms2=enabled -Dzlib=enabled -Dmanpage-build=disabled
"$CC" -shared -O2 -static-libgcc -I"$PREFIX/include" "$REPO/source/hap_demux.c" \
    -L"$PREFIX/lib" -lavformat -lavcodec -lavutil -lm -o "$PREFIX/bin/hap_demux.dll"
cp "$PREFIX/bin/"*.dll "$WORK/runtime/"
cp "$PREFIX/bin/ffmpeg.exe" "$PREFIX/bin/ffprobe.exe" "$PREFIX/bin/mpv.exe" "$WORK/runtime/"
# The POSIX MinGW compiler may introduce this runtime. Preserve its matching sources below.
pthread=$("$CC" -print-file-name=libwinpthread-1.dll)
if [[ -f "$pthread" ]]; then cp "$pthread" "$WORK/runtime/"; fi
mkdir -p "$WORK/notices/toolchain"
for dir in /usr/share/doc/mingw-w64* /usr/share/doc/gcc-*-base; do
    if [[ -f "$dir/copyright" ]]; then cp "$dir/copyright" "$WORK/notices/toolchain/$(basename "$dir")-copyright"; fi
done
mingw_version=$(dpkg-query -W -f='${source:Version}' mingw-w64-common)
(cd "$WORK/toolchain-sources" && apt-get source --download-only "mingw-w64=$mingw_version")
cp "$WORK/build/ffmpeg/config.h" "$WORK/build/ffmpeg/ffbuild/config.mak" "$WORK/provenance/"
for name in libass libplacebo mpv; do
    cp "$WORK/build/$name/meson-info/intro-buildoptions.json" "$WORK/provenance/$name-options.json"
done
python3 "$REPO/scripts/native/package_candidate.py" "$WORK"
echo 'Candidate built. Windows playback/GPU tests and final distribution review are still required.'
