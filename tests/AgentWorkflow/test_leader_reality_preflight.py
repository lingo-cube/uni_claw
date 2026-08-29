import json
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]


class LeaderRealityPreflightTests(unittest.TestCase):
    def test_shared_leader_profile_requires_bounded_reality_preflight(self):
        registry = json.loads(
            (REPO_ROOT / ".ai/profiles/roles.json").read_text(encoding="utf-8"))
        leader = next(profile for profile in registry["profiles"]
                      if profile["id"] == "coding-leader")
        self.assertIn(
            "bounded_reality_preflight_before_semantic_or_code_depth",
            leader["responsibilities"])
        self.assertTrue(
            leader["constraints"]["observable_reality_precedes_semantic_attribution"])
        self.assertTrue(
            leader["constraints"]["unknown_visible_state_must_not_be_invented"])
        self.assertTrue(
            leader["constraints"]["stop_evidence_expansion_at_minimum_owning_seam"])

    def test_workflow_places_preflight_before_profile_and_skill_routing(self):
        workflow = (REPO_ROOT / ".ai/workflows/uniflow-coding-workflow.md").read_text(
            encoding="utf-8")
        preflight = workflow.index("完成 Reality Preflight")
        module = workflow.index("识别一个主要 ModuleProfile")
        skills = workflow.index("选择最小 required_skills")
        self.assertLess(preflight, module)
        self.assertLess(preflight, skills)
        self.assertIn("界面证据不可得时明确写“未知”", workflow)
        self.assertIn("不得成为 Fact、Contract、Runtime belief", workflow)


if __name__ == "__main__":
    unittest.main()
