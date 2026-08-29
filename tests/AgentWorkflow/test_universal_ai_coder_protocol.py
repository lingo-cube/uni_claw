import os
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = REPO_ROOT / ".ai" / "skills"
ADAPTER_ROOTS = (
    REPO_ROOT / ".agents" / "skills",
    REPO_ROOT / ".dsh" / "skills",
)


class UniversalAiCoderProtocolTests(unittest.TestCase):
    def source_names(self):
        return {
            entry.name
            for entry in SOURCE_ROOT.iterdir()
            if entry.is_dir() and (entry / "SKILL.md").is_file()
        }

    def test_every_host_adapter_maps_exactly_to_portable_skill_core(self):
        source_names = self.source_names()
        self.assertGreater(len(source_names), 0)
        for adapter_root in ADAPTER_ROOTS:
            entries = {entry.name: entry for entry in adapter_root.iterdir()}
            self.assertEqual(source_names, set(entries), adapter_root)
            for name, entry in entries.items():
                self.assertTrue(entry.is_symlink(), entry)
                self.assertEqual(
                    f"../../.ai/skills/{name}", os.readlink(entry), entry
                )
                self.assertEqual((SOURCE_ROOT / name).resolve(), entry.resolve())
                self.assertEqual(
                    (SOURCE_ROOT / name / "SKILL.md").read_bytes(),
                    (entry / "SKILL.md").read_bytes(),
                )

    def test_claude_project_configuration_is_retired(self):
        self.assertFalse((REPO_ROOT / ".claude").exists())
        adapter = (REPO_ROOT / "CLAUDE.md").read_text(encoding="utf-8")
        self.assertIn("AGENTS.md", adapter)
        for forbidden in (".claude/", ".ai/skills/", "model-routing", "hooks/"):
            self.assertNotIn(forbidden, adapter)

    def test_migrated_skills_are_host_neutral(self):
        migrated = (
            "openspec-propose",
            "openspec-apply-change",
            "openspec-explore",
            "openspec-archive-change",
            "perception-model-intelligence",
        )
        forbidden = (
            "AskUserQuestion tool",
            "TodoWrite tool",
            "Task tool",
            "Skill tool",
            "/opsx:",
        )
        for name in migrated:
            text = (SOURCE_ROOT / name / "SKILL.md").read_text(encoding="utf-8")
            self.assertIn("authority: NONE", text, name)
            for token in forbidden:
                self.assertNotIn(token, text, name)

    def test_current_protocol_and_bootstrap_do_not_depend_on_claude_paths(self):
        current_files = (
            "AGENTS.md",
            ".ai/agent-routing.md",
            ".ai/development-protocol.md",
            ".ai/model-routing.yaml",
            ".ai/openspec-workflow.md",
            ".ai/task-contract.md",
            ".ai/result-contract.md",
            ".ai/workflows/uniflow-coding-workflow.md",
            ".ai/skills/README.md",
            "openspec/AGENTS.md",
            ".dsh/profile-adapter/README.md",
            "tools/agent_profile_validator.py",
            "tools/csharp-mcp-README.md",
            "init/README.md",
            "init/PATH-LAYOUT.md",
            "init/gen-secrets.sh",
            "init/quick-init.sh",
        )
        for relative in current_files:
            text = (REPO_ROOT / relative).read_text(encoding="utf-8")
            self.assertNotIn(".claude/", text, relative)
        self.assertFalse(
            (REPO_ROOT / "init" / "templates" / "claude-settings.json.template").exists()
        )

    def test_csharp_mcp_guidance_is_portable(self):
        guide = REPO_ROOT / ".ai" / "tooling" / "csharp-mcp-query.md"
        self.assertTrue(guide.is_file())
        self.assertIn(
            ".ai/tooling/csharp-mcp-query.md",
            (REPO_ROOT / "AGENTS.md").read_text(encoding="utf-8"),
        )


if __name__ == "__main__":
    unittest.main()
