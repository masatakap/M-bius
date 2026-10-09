"""Fail on unexpected DLL imports and inventory the candidate and its build inputs."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

root = Path(sys.argv[1]).resolve()
runtime = root / "runtime"
system = set("kernel32.dll user32.dll advapi32.dll ole32.dll oleaut32.dll shell32.dll "
             "shlwapi.dll ws2_32.dll winmm.dll gdi32.dll comdlg32.dll comctl32.dll "
             "imm32.dll version.dll msvcrt.dll ucrtbase.dll ntdll.dll bcrypt.dll "
             "crypt32.dll ncrypt.dll secur32.dll normaliz.dll d3d11.dll dxgi.dll d3d9.dll "
             "opengl32.dll dwmapi.dll avrt.dll setupapi.dll mfplat.dll mfuuid.dll "
             "mf.dll propsys.dll powrprof.dll uuid.dll dwrite.dll usp10.dll "
             "winspool.drv iphlpapi.dll psapi.dll msimg32.dll ksuser.dll "
             "cfgmgr32.dll wintrust.dll rpcrt4.dll shcore.dll uxtheme.dll".split())
supplied = {p.name.lower() for p in runtime.iterdir() if p.suffix.lower() == ".dll"}
imports = {}
unresolved_imports = {}
for path in sorted(runtime.iterdir()):
    if path.suffix.lower() not in (".dll", ".exe"):
        continue
    output = subprocess.check_output(["x86_64-w64-mingw32-objdump", "-p", str(path)], text=True)
    deps = sorted(set(re.findall(r"DLL Name:\s*(\S+)", output)))
    unresolved = [d for d in deps if d.lower() not in system | supplied
                  and not d.lower().startswith(("api-ms-win-", "ext-ms-win-"))]
    if unresolved:
        unresolved_imports[path.name] = unresolved
    imports[path.name] = deps
if unresolved_imports:
    (root / "provenance" / "unresolved-imports.json").write_text(
        json.dumps(unresolved_imports, indent=2), encoding="utf-8")
    raise SystemExit(f"Unclassified runtime imports: {unresolved_imports}")
files = {}
for folder in ("runtime", "archives", "toolchain-sources", "notices", "provenance"):
    for path in sorted((root / folder).rglob("*")):
        if path.is_file():
            files[path.relative_to(root).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
(root / "candidate-manifest.json").write_text(json.dumps({
    "status": "experimental-not-validated-for-release",
    "windows_playback_verified": False,
    "imports": imports,
    "sha256": files,
}, indent=2) + "\n", encoding="utf-8")
(root / "SHA256SUMS.txt").write_text("".join(f"{v}  {k}\n" for k, v in files.items()), encoding="utf-8")
