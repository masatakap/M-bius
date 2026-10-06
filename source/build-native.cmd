@echo off
setlocal
if "%~1"=="" (
  echo Usage: build-native.cmd FFmpeg-shared-SDK-folder
  exit /b 1
)
cl /nologo /LD /O2 /MT /I "%~1\include" "%~dp0hap_demux.c" /Fo"%~dp0hap_demux.obj" /link /LIBPATH:"%~1\lib" avformat.lib avcodec.lib avutil.lib /OUT:"%~dp0..\app\hap_demux.dll" /IMPLIB:"%~dp0hap_demux.lib"
