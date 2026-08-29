from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[2]
SKILLS = [
    ROOT / ".ai/skills/evidence-driven-debugging/SKILL.md",
    ROOT / ".ai/skills/runtime-behavior-debugging/SKILL.md",
    ROOT / ".ai/skills/uniagent-evolution-loop/SKILL.md",
]


class SkillSemanticsTests(unittest.TestCase):
    def test_each_skill_requires_ui_first_falsifiable_hypothesis(self):
        for path in SKILLS:
            text = path.read_text(encoding="utf-8")
            self.assertRegex(text, r"用户可见目标|user's visible goal")
            self.assertRegex(text, r"当前界面|current visible interface|current visible UI")
            self.assertRegex(text, r"最短.*路径|shortest human-feasible operation path")
            self.assertRegex(text, r"可证伪|falsifiable")

    def test_each_skill_rejects_turning_ui_accidents_into_authority(self):
        for path in SKILLS:
            text = path.read_text(encoding="utf-8")
            self.assertRegex(text, r"坐标|coordinates")
            self.assertRegex(text, r"固定点击序列|fixed click sequences")
            self.assertRegex(text, r"scenario knowledge")
        loop = (ROOT / ".ai/skills/uniagent-evolution-loop/SKILL.md").read_text(encoding="utf-8")
        self.assertIn("Runtime 权威", loop)

    def test_each_skill_enters_code_from_first_divergence_not_long_chain(self):
        for path in SKILLS:
            text = path.read_text(encoding="utf-8")
            self.assertIn("First Divergence", text, path.name)
            self.assertRegex(text, r"漫长调用链|long code call chain|long call-chain trace")
            self.assertRegex(text, r"最小必要代码|minimum owning")


if __name__ == "__main__":
    unittest.main()
