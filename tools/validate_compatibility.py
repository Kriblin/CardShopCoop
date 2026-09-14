#!/usr/bin/env python3
"""Run local compatibility checks and optionally package an unverified candidate."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PROJECT = "src/CardShopCoop/CardShopCoop.csproj"
HARNESSES = ("AvatarInitialization", "MarketCompatibility", "ShelfBoxPull", "TcgCompatibility", "GameCompatibility")


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def release_section(changelog, version):
    match = re.search(r"^## " + re.escape(version) + r"\s*\n(.*?)(?=^## |\Z)", changelog, re.M | re.S)
    if not match or not match.group(1).strip():
        raise ValueError(f"CHANGELOG.md has no release notes for {version}")
    return f"## {version}\n{match.group(1).rstrip()}\n"


def package_candidate(root, version, report, notes):
    if report.get("status") != "passed":
        raise ValueError("Cannot package a candidate before all local checks pass")
    # Explicit allowlist: never collect game assemblies, personal saves or plugins.
    files = {
        "BepInEx/plugins/CardShopCoop.dll": root / "src/CardShopCoop/bin/Release/CardShopCoop.dll",
        "README.md": root / "README.md",
        "LICENSE": root / "LICENSE",
    }
    for path in files.values():
        if not path.is_file():
            raise FileNotFoundError(path)
    if sha256(files["BepInEx/plugins/CardShopCoop.dll"]) != report["built_plugin_sha256"]:
        raise ValueError("Built plugin changed after validation")
    destination = root / "dist/release"
    destination.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    archive = destination / f"CardShopCoop-{version}-candidate-{stamp}.zip"
    with zipfile.ZipFile(archive, "x", zipfile.ZIP_DEFLATED) as output:
        for name, path in files.items():
            output.write(path, name)
        output.writestr("CHANGELOG.md", notes)
        output.writestr("VALIDATION.txt", "Local automated checks passed. Two-player runtime validation is pending.\n"
                        f"Plugin: {version}\nSource commit: {report['commit']}\n"
                        f"Source modified: {report['source_modified']}\n"
                        f"Plugin SHA-256: {report['built_plugin_sha256']}\n"
                        "This candidate has not been deployed or published by the validation tool.\n")
    return archive


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", action="store_true", help="Create a local candidate ZIP after checks pass; never deploy or publish")
    args = parser.parse_args()
    properties = ["-p:Deploy=false", "-p:UseSharedCompilation=false"]
    (ROOT / "diag/compatibility").mkdir(parents=True, exist_ok=True)
    output = Path(tempfile.mkdtemp(prefix=datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ-"), dir=ROOT / "diag/compatibility"))
    report = {"status": "running", "two_player_runtime": "pending", "steps": []}
    print(f"Local validation evidence: {output}", flush=True)

    def capture(command):
        return subprocess.check_output(command, cwd=ROOT, text=True).strip()

    def step(name, command):
        logfile = output / f"{name}.log"
        print(f"Running {name}...", flush=True)
        with logfile.open("w", encoding="utf-8") as log:
            result = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
        report["steps"].append({"name": name, "exit_code": result.returncode, "log": logfile.name})
        if result.returncode:
            raise RuntimeError(f"{name} failed ({result.returncode}); see {logfile}")
        print(f"PASS {name}", flush=True)

    try:
        report["commit"] = capture(["git", "rev-parse", "HEAD"])
        report["source_modified"] = bool(capture(["git", "status", "--porcelain"]))
        report["dotnet_sdk"] = capture(["dotnet", "--version"])
        config = json.loads(capture(["dotnet", "msbuild", PROJECT, "-nologo", "-getProperty:GamePath,CardShopCoopVersion", *properties]))["Properties"]
        game = Path(config["GamePath"]).resolve()
        version = config["CardShopCoopVersion"]
        if not re.fullmatch(r"\d+\.\d+\.\d+", version):
            raise ValueError(f"Unexpected CardShopCoopVersion: {version}")
        report["plugin_version"] = version
        report["game_path"] = str(game)
        report["game_assembly_sha256"] = sha256(game / "Card Shop Simulator_Data/Managed/Assembly-CSharp.dll")
        # On-disk binaries identify this installation, not which mods were loaded by a
        # host/guest process. Content packs and remote PCs still need manual recording.
        plugin_dir = game / "BepInEx/plugins"
        report["installed_plugin_binaries"] = [
            {"path": str(path.relative_to(plugin_dir)), "sha256": sha256(path)}
            for path in sorted(plugin_dir.rglob("*.dll"))
        ]
        manifest = game.parent.parent / "appmanifest_3070070.acf"
        if manifest.is_file():
            build = re.search(r'"buildid"\s+"(\d+)"', manifest.read_text(encoding="utf-8"))
            if build:
                report["steam_build_id"] = build.group(1)
        notes = release_section((ROOT / "CHANGELOG.md").read_text(encoding="utf-8"), version)
        step("validation-tool", [sys.executable, "-B", "-m", "unittest", "discover", "-s", "tests/CompatibilityValidation", "-v"])
        step("restore", ["dotnet", "restore", PROJECT, *properties])
        step("whitespace", ["dotnet", "format", PROJECT, "whitespace", "--verify-no-changes", "--no-restore"])
        step("release-build", ["dotnet", "build", PROJECT, "-c", "Release", "--no-restore", *properties])
        for harness in HARNESSES:
            command = ["dotnet", "run", "--project", f"tests/{harness}/{harness}.csproj", "-c", "Release", *properties]
            if harness == "GameCompatibility":
                command.extend(["--", str(ROOT), str(game)])
            step(harness, command)
        step("diff-whitespace", ["git", "diff", "--check"])
        report["built_plugin_sha256"] = sha256(ROOT / "src/CardShopCoop/bin/Release/CardShopCoop.dll")
        report["status"] = "passed"
        if args.package:
            archive = package_candidate(ROOT, version, report, notes)
            report["candidate"] = {"path": str(archive.relative_to(ROOT)), "sha256": sha256(archive)}
            print(f"Candidate: {archive}", flush=True)
    except (OSError, ValueError, RuntimeError, subprocess.CalledProcessError) as error:
        report["status"] = "failed"
        report["error"] = str(error)
        print(f"FAIL: {error}", flush=True)
    finally:
        (output / "result.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("Two-player gameplay, loaded mod parity, and release sign-off remain pending.")
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
