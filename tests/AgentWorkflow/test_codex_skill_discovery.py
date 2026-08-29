import os
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
SKILL_NAME = "uniagent-evolution-loop"
SOURCE_SKILL = REPO_ROOT / ".ai" / "skills" / SKILL_NAME
ADAPTER_SKILL = REPO_ROOT / ".agents" / "skills" / SKILL_NAME
EXPECTED_TARGET = Path("../../.ai/skills") / SKILL_NAME


class CodexSkillDiscoveryTests(unittest.TestCase):
    def test_project_adapter_is_the_expected_relative_symlink(self):
        self.assertTrue(ADAPTER_SKILL.is_symlink())
        self.assertEqual(EXPECTED_TARGET, Path(os.readlink(ADAPTER_SKILL)))
        self.assertEqual(SOURCE_SKILL.resolve(), ADAPTER_SKILL.resolve())
        self.assertTrue((ADAPTER_SKILL / "SKILL.md").is_file())

    def test_agents_skills_contains_only_adapter_symlinks(self):
        entries = list((REPO_ROOT / ".agents" / "skills").iterdir())
        self.assertIn(ADAPTER_SKILL, entries)
        self.assertTrue(all(entry.is_symlink() for entry in entries))

    def test_skill_body_has_one_repository_source(self):
        self.assertTrue((SOURCE_SKILL / "SKILL.md").is_file())
        self.assertNotIn(
            SKILL_NAME,
            (REPO_ROOT / ".codex" / "config.toml").read_text(encoding="utf-8"),
        )

    def test_governance_documents_the_adapter_boundary(self):
        policy = (REPO_ROOT / ".ai" / "skills" / "README.md").read_text(encoding="utf-8")
        self.assertIn(".agents/skills/<name>", policy)
        self.assertIn("相对符号链接", policy)
        self.assertIn("不是第二份 Skill 真相源", policy)


if __name__ == "__main__":
    unittest.main()
