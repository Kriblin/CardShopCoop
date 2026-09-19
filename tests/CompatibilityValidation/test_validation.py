"""Stdlib regression tests for validation failure handling and candidate contents."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("validation", Path(__file__).resolve().parents[2] / "tools/validate_compatibility.py")
validation = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validation)


class ValidationTests(unittest.TestCase):
    def setUp(self):
        self.workspace = tempfile.TemporaryDirectory()
        self.addCleanup(self.workspace.cleanup)
        self.root = Path(self.workspace.name)
        self.dll = self.root / "src/CardShopCoop/bin/Release/CardShopCoop.dll"
        self.dll.parent.mkdir(parents=True)
        self.dll.write_bytes(b"fixture plugin")
        for name in ("README.md", "LICENSE"):
            (self.root / name).write_text(name)
        self.notes = "## 1.3.1\nCurrent changes.\n\n**Both players must update.**\n"
        (self.root / "CHANGELOG.md").write_text(self.notes + "\n## 1.3.0\nOlder changes.\n")
        self.report = {"status": "passed", "commit": "fixture", "source_modified": False, "built_plugin_sha256": validation.sha256(self.dll)}

    def test_notes_come_from_exact_version_section(self):
        self.assertEqual(validation.release_section((self.root / "CHANGELOG.md").read_text(), "1.3.1"), self.notes)
        with self.assertRaises(ValueError):
            validation.release_section(self.notes, "1.3.2")

    def test_candidate_contains_only_allowlisted_files_and_pending_status(self):
        (self.dll.parent / "Assembly-CSharp.dll").write_bytes(b"must not ship")
        candidate = validation.package_candidate(self.root, "1.3.1", self.report, self.notes)
        self.assertEqual(candidate.parent, self.root / "dist/release")
        with zipfile.ZipFile(candidate) as archive:
            self.assertEqual(set(archive.namelist()), {"BepInEx/plugins/CardShopCoop.dll", "README.md", "LICENSE", "CHANGELOG.md", "VALIDATION.txt"})
            self.assertEqual(archive.read("CHANGELOG.md").decode(), self.notes)
            self.assertIn("runtime validation is pending", archive.read("VALIDATION.txt").decode())
            self.assertEqual(archive.read("BepInEx/plugins/CardShopCoop.dll"), b"fixture plugin")

    def test_rejects_failed_validation_or_changed_binary(self):
        self.report["status"] = "failed"
        with self.assertRaises(ValueError):
            validation.package_candidate(self.root, "1.3.1", self.report, self.notes)
        self.report["status"] = "passed"
        self.dll.write_bytes(b"another build")
        with self.assertRaises(ValueError):
            validation.package_candidate(self.root, "1.3.1", self.report, self.notes)
        self.assertFalse((self.root / "dist").exists())

    def test_failed_formatting_stops_before_build_and_packaging(self):
        game = self.root / "game"
        assembly = game / "Card Shop Simulator_Data/Managed/Assembly-CSharp.dll"
        assembly.parent.mkdir(parents=True)
        assembly.write_bytes(b"fixture game")
        commands = []

        def capture(command, **kwargs):
            if "msbuild" in command:
                return json.dumps({"Properties": {"GamePath": str(game), "CardShopCoopVersion": "1.3.1"}})
            if "status" in command:
                return ""
            return "fixture"

        def run(command, **kwargs):
            commands.append(command)
            return subprocess.CompletedProcess(command, 1 if "format" in command else 0)

        with patch.object(validation, "ROOT", self.root), patch.object(sys, "argv", ["validate", "--package"]), \
             patch.object(validation.subprocess, "check_output", side_effect=capture), \
             patch.object(validation.subprocess, "run", side_effect=run):
            self.assertEqual(validation.main(), 1)
        self.assertEqual([command[1] for command in commands], ["-B", "restore", "format"])
        self.assertIn("-p:Deploy=false", commands[1])
        self.assertFalse((self.root / "dist").exists())
        report = json.loads(next((self.root / "diag").rglob("result.json")).read_text())
        self.assertEqual(report["status"], "failed")
        self.assertEqual(report["two_player_runtime"], "pending")


if __name__ == "__main__":
    unittest.main()
