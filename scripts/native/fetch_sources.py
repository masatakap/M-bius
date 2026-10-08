"""Fetch and verify the experimental runtime's locked sources (Python 3.12+)."""
import argparse
import copy
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import tarfile
import urllib.request


def prepare(work: Path):
    lock_path = Path(__file__).with_name("sources.lock.json")
    lock = json.loads(lock_path.read_text(encoding="utf-8"))
    archives, sources, notices = (work / p for p in ("archives", "src", "notices"))
    for directory in (archives, sources, notices):
        directory.mkdir(parents=True, exist_ok=True)
    inventory = []
    for entry in lock["sources"]:
        name = PurePosixPath(entry["name"])
        if name.is_absolute() or ".." in name.parts:
            raise ValueError(f"Invalid source name: {name}")
        archive = archives / entry["archive"]
        if archive.name != entry["archive"]:
            raise ValueError("Archive must be a basename")
        if not archive.exists():
            partial = archive.with_suffix(".partial")
            with urllib.request.urlopen(entry["url"], timeout=120) as response, partial.open("wb") as target:
                shutil.copyfileobj(response, target)
            if hashlib.sha256(partial.read_bytes()).hexdigest() != entry["sha256"]:
                raise ValueError(f"Download hash mismatch: {name}")
            partial.replace(archive)
        if hashlib.sha256(archive.read_bytes()).hexdigest() != entry["sha256"]:
            raise ValueError(f"Cached archive hash mismatch: {name}")
        destination = sources.joinpath(*name.parts)
        marker = destination / ".mobius-source-sha256"
        if marker.exists():
            if marker.read_text().strip() != entry["sha256"]:
                raise ValueError(f"Existing source has a different version: {name}")
        else:
            # GitHub archives omit gitlinks; these are separate locked entries.
            # Reject a populated directory instead of overwriting a checkout.
            if destination.exists() and any(destination.iterdir()):
                raise ValueError(f"Source directory is not empty: {destination}")
            destination.mkdir(parents=True, exist_ok=True)
            with tarfile.open(archive) as package:
                members = []
                roots = set()
                for original in package.getmembers():
                    path = PurePosixPath(original.name)
                    if path.is_absolute() or ".." in path.parts:
                        raise ValueError(f"Unsafe archive member: {original.name}")
                    roots.add(path.parts[0])
                    if len(path.parts) == 1:
                        continue
                    member = copy.copy(original)
                    member.name = str(PurePosixPath(*path.parts[1:]))
                    if member.islnk():
                        link = PurePosixPath(member.linkname)
                        if not link.parts or link.parts[0] != path.parts[0]:
                            raise ValueError("Hardlink outside archive root")
                        member.linkname = str(PurePosixPath(*link.parts[1:]))
                    members.append(member)
                if len(roots) != 1:
                    raise ValueError(f"Expected one archive root: {name}")
                package.extractall(destination, members=members, filter="data")
            marker.write_text(entry["sha256"] + "\n", encoding="utf-8")
        files = []
        for path in destination.rglob("*"):
            if not path.is_file() or path.is_symlink():
                continue
            lower = path.name.lower()
            if lower.startswith(("license", "copying", "copyright", "notice")) or lower == "ftl.txt":
                relative = path.relative_to(sources)
                target = notices / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(path, target)
                files.append(str(relative).replace("\\", "/"))
        inventory.append({**entry, "notice_files": files})
        print(f"Verified {entry['name']} @ {entry['revision']}", flush=True)
    shutil.copy2(lock_path, work / lock_path.name)
    (work / "source-inventory.json").write_text(json.dumps({
        "status": "experimental-source-inputs; final distribution review pending",
        "sources": inventory,
    }, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work-dir", type=Path, required=True)
    args = parser.parse_args()
    prepare(args.work_dir.resolve())
