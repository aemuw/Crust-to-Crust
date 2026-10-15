"""Stage an audited, behavior-preserving Godot source release (stdlib only).

Run from any directory: python Tools/PrepareRelease.py
Output is ignored <repository>/artifacts/release-stage; main is never pruned.
"""
from pathlib import Path
import hashlib
import json
import re
import shutil
import sys

PROJECT = Path(__file__).resolve().parents[1]
REPO = PROJECT.parent
OUTPUT = REPO / "artifacts" / "release-stage"
REPORT = REPO / "artifacts" / "release-audit.json"
TEXT = {".cs", ".tscn", ".tres", ".gdshader", ".gd", ".godot"}


def dependencies(project):
    """Include every compiled runtime script/scene, plus their resource closure.

    Dynamic families are expanded conservatively, including catalog Root+file,
    CaveArt.Crop names and interpolated frame numbers. Unknown expressions fail
    rather than silently dropping a potentially required dependency.
    """
    keep = {"project.godot", "Crust-to-Crust.csproj", "Crust-to-Crust.sln"}
    keep.update(p.relative_to(project).as_posix() for p in (project / "Scripts").rglob("*.cs"))
    keep.update(p.relative_to(project).as_posix() for p in (project / "Scenes").rglob("*.tscn"))
    pending, scanned, missing = list(keep), set(), []

    def include(name, owner):
        if "{" in name:
            matches = list(project.glob(re.sub(r"\{[^}]+\}", "*", name)))
            if not matches:
                missing.append(f"{owner}: unresolved dynamic path {name}")
            for match in matches:
                include(match.relative_to(project).as_posix(), owner)
            return
        target = project / name
        if target.is_dir():
            # Directory prefixes are handled by explicit expressions below.
            return
        if not target.is_file():
            missing.append(f"{owner}: {name}")
            return
        if name not in keep:
            keep.add(name)
            pending.append(name)

    while pending:
        name = pending.pop()
        if name in scanned:
            continue
        scanned.add(name)
        path = project / name
        if path.suffix not in TEXT:
            continue
        text = path.read_text(encoding="utf-8-sig")
        for resource in re.findall(r'res://([^"\s<>]+)', text):
            if resource.endswith("/"):
                if resource not in {"Textures/PixelSkins/", "Textures/ArtRefresh/"}:
                    missing.append(f"{name}: unhandled dynamic directory {resource}")
                continue
            include(resource, name)
        root = re.search(r'const string Root\s*=\s*"res://([^"]+)"', text)
        if root:
            for filename in re.findall(r'Root\s*\+\s*"([^"]+)"', text):
                include(root[1] + filename, name)
        if name == "Scripts/CaveArt.cs":
            for filename in re.findall(r'Crop\("([^"]+)"', text):
                include("Textures/ArtRefresh/" + filename + ".png", name)
    if missing:
        raise RuntimeError("Missing dependencies:\n" + "\n".join(sorted(set(missing))))
    for name in list(keep):
        for suffix in (".import", ".uid"):
            if (project / (name + suffix)).is_file():
                keep.add(name + suffix)
    # Licenses are distribution requirements, even though the engine never loads them.
    keep.update(p.relative_to(project).as_posix() for p in (project / "Fonts").glob("*-OFL.txt"))
    return keep


def write_report(keep):
    all_assets = [p for folder in ("Textures", "Audio", "Fonts", "Shaders")
                  for p in (PROJECT / folder).rglob("*") if p.is_file()]
    excluded = [p for p in all_assets if p.relative_to(PROJECT).as_posix() not in keep]
    report = {
        "project_files": len(keep),
        "retained_bytes": sum((PROJECT / name).stat().st_size for name in keep),
        "excluded_asset_bytes": sum(p.stat().st_size for p in excluded),
        "excluded_assets": sorted(p.relative_to(PROJECT).as_posix() for p in excluded),
        "sha256": {name: hashlib.sha256((PROJECT / name).read_bytes()).hexdigest() for name in sorted(keep)},
    }
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k not in ("sha256", "excluded_assets")}, indent=2))
    print(f"Audit: {REPORT}\nStaging: {OUTPUT}")


def main():
    keep = dependencies(PROJECT)
    if sys.argv[1:] == ["--refresh-audit"]:
        write_report(keep)
        return
    if len(sys.argv) == 3 and sys.argv[1] == "--verify":
        staged = Path(sys.argv[2]).resolve() / "crust-to-crust"
        dependencies(staged)
        changed = [name for name in keep if not (staged / name).is_file()
                   or (staged / name).read_bytes() != (PROJECT / name).read_bytes()]
        if changed:
            raise RuntimeError("Release differs from developer runtime: " + ", ".join(changed))
        print(f"Verified {len(keep)} runtime files byte-for-byte; all dependencies exist.")
        return
    OUTPUT.mkdir(parents=True, exist_ok=True)
    # Refuse a dirty staging destination; never recursively delete user files.
    if any(OUTPUT.iterdir()):
        raise RuntimeError(f"Staging directory must be empty: {OUTPUT}")
    for name in sorted(keep):
        target = OUTPUT / "crust-to-crust" / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(PROJECT / name, target)
    (OUTPUT / ".gitignore").write_text(
        ".godot/\nbin/\nobj/\nartifacts/\n*.log\n.vs/\n.vscode/\n.idea/\n"
        "export_credentials.cfg\n*.csproj.user\n*.userprefs\n", encoding="utf-8")
    (OUTPUT / "README.md").write_text(
        "# Crust to Crust\n\n"
        "Clean source release of the 2D cave runner.\n\n"
        "## Run\n\n"
        "1. Install Godot **4.7.2 .NET** and the .NET 8 SDK (a newer SDK capable of targeting .NET 8 also works).\n"
        "2. Import `crust-to-crust/project.godot`, wait for resource import, then build C# and press F5.\n\n"
        "CLI build: `dotnet build crust-to-crust/Crust-to-Crust.csproj -c Release`.\n\n"
        "A / D or arrows: move. Mouse: aim. Left click: toggle lantern. Esc: pause. Gamepad is supported.\n\n"
        "This branch contains the runtime source and required assets. Developer notes, asset generators "
        "and smoke tests are retained on `main`. This archive is source code, not a prebuilt executable.\n\n"
        "Font licenses are included in `crust-to-crust/Fonts/`.\n", encoding="utf-8")
    write_report(keep)


if __name__ == "__main__":
    main()
