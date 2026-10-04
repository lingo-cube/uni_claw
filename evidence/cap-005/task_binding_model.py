"""CAP-005 design model only; no production API or device evidence."""
from dataclasses import dataclass, field
import unittest


@dataclass(frozen=True)
class Context:
    run: str
    session: str
    language: str


@dataclass
class Binding:
    context: Context
    provider: object
    closed: bool = False
    releases: int = 0
    results: list = field(default_factory=list)
    diagnostics: list = field(default_factory=list)

    def deliver(self, context, value):
        if context != self.context:
            self.diagnostics.append("correlation-mismatch")
            return False
        if self.closed:
            self.diagnostics.append("late")
            return False
        self.results.append(value)
        return True

    def close(self):
        if not self.closed:
            self.closed = True
            self.releases += 1


class HostModel:
    def __init__(self):
        self.catalog = ("language-inspector:1.0",)
        self.provider = object()  # Borrowed connection, never disposed by binding.
        self.bindings = {}

    def bind(self, context, fail_initialization=False):
        if not context.run or not context.session:
            raise ValueError("Runtime identity missing")
        existing = self.bindings.get(context.run)
        if existing:
            if existing.closed or existing.context != context:
                raise ValueError("Closed or conflicting binding")
            return existing
        binding = Binding(context, self.provider)
        if fail_initialization:
            binding.close()
            return None, binding  # Retained for rollback assertions only.
        self.bindings[context.run] = binding
        return binding


class BindingDesignTests(unittest.TestCase):
    def setUp(self):
        self.host = HostModel()
        self.a = Context("model-run-A", "model-session-A", "zh-CN")
        self.b = Context("model-run-B", "model-session-B", "en-US")

    def test_tasks_interleave_without_mutating_catalog(self):
        catalog = self.host.catalog
        a, b = self.host.bind(self.a), self.host.bind(self.b)
        self.assertIsNot(a, b)
        a.deliver(self.a, "A-first")
        b.deliver(self.b, "B-first")
        a.deliver(self.a, "A-second")
        self.assertEqual(a.results, ["A-first", "A-second"])
        self.assertEqual(b.results, ["B-first"])
        self.assertIs(self.host.catalog, catalog)
        self.assertNotEqual(a.context.language, b.context.language)

    def test_cancel_A_keeps_B_and_provider_alive(self):
        a, b = self.host.bind(self.a), self.host.bind(self.b)
        a.close()
        a.close()
        self.assertEqual(a.releases, 1)
        self.assertFalse(b.closed)
        self.assertTrue(b.deliver(self.b, "B-after-A-close"))
        self.assertIs(b.provider, self.host.provider)

    def test_duplicate_bind_idempotent_config_change_rejected(self):
        a = self.host.bind(self.a)
        self.assertIs(a, self.host.bind(self.a))
        with self.assertRaises(ValueError):
            self.host.bind(Context(self.a.run, self.a.session, "en-US"))

    def test_closed_binding_cannot_be_resurrected(self):
        a = self.host.bind(self.a)
        a.close()
        with self.assertRaises(ValueError):
            self.host.bind(self.a)
        self.assertEqual(len(self.host.bindings), 1)

    def test_wrong_correlation_does_not_cross_tasks(self):
        a, b = self.host.bind(self.a), self.host.bind(self.b)
        self.assertFalse(a.deliver(self.b, "wrong"))
        self.assertEqual(a.diagnostics, ["correlation-mismatch"])
        self.assertEqual(a.results, [])
        self.assertEqual(b.results, [])

    def test_initialization_failure_rolls_back(self):
        result, partial = self.host.bind(self.a, fail_initialization=True)
        self.assertIsNone(result)
        self.assertTrue(partial.closed)
        self.assertEqual(partial.releases, 1)
        self.assertEqual(self.host.bindings, {})

    def test_late_result_diagnostic_only(self):
        a = self.host.bind(self.a)
        a.deliver(self.a, "before-close")
        a.close()
        self.assertFalse(a.deliver(self.a, "after-close"))
        self.assertEqual(a.results, ["before-close"])
        self.assertEqual(a.diagnostics, ["late"])

    def test_missing_identity_rejected_before_allocation(self):
        with self.assertRaises(ValueError):
            self.host.bind(Context("", self.a.session, "zh-CN"))
        self.assertEqual(self.host.bindings, {})

    def test_singleton_counterexample_leaks_context(self):
        shared = {"context": self.a}
        task_a = shared
        task_b = shared
        task_b["context"] = self.b
        self.assertIs(task_a, task_b)
        self.assertNotEqual(task_a["context"], self.a)


if __name__ == "__main__":
    unittest.main(verbosity=2)
