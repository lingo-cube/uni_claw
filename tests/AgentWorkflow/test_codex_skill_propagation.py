import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
ADAPTERS = (
    "module-worker.toml",
    "test-author.toml",
    "verifier.toml",
    "semantic-analyzer.toml",
)


class CodexSkillPropagationTests(unittest.TestCase):
    def test_all_worker_adapters_load_resolved_required_skills_before_action(self):
        for name in ADAPTERS:
            text = (REPO_ROOT / ".codex" / "agents" / name).read_text(
                encoding="utf-8")
            self.assertIn("required_skills", text, name)
            self.assertIn("context_sources.required_skills", text, name)
            self.assertIn("完整读取每个 SKILL.md", text, name)
            self.assertIn("REQUIRED_SKILL_UNAVAILABLE", text, name)
            self.assertIn("BLOCKED_FOR_SPEC", text, name)

    def test_required_skill_never_expands_worker_authority(self):
        for name in ADAPTERS:
            text = (REPO_ROOT / ".codex" / "agents" / name).read_text(
                encoding="utf-8")
            self.assertIn("Authority 始终为 NONE", text, name)
            self.assertRegex(text, r"不能.*(scope|范围|契约|Contract)", name)


if __name__ == "__main__":
    unittest.main()
