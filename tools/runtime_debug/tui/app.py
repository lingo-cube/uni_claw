"""runtime-debug TUI — thin textual shell over the Query Core.

Rendering and input collection only: every visible datum comes from
runtime_debug.query via runtime_debug.tui.view_models. No correlation,
pruning, or analysis logic lives here. The textual import is deferred so the
module still compiles (and view_models stay testable) without the framework.

Run: uv run --with textual python -m runtime_debug.tui.app <bundle-dir>
"""

from __future__ import annotations

import os
import sys

from .. import query
from ..sources import bundle as bundle_source
from .view_models import diagnosis_view, filter_state, open_run

APP_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
if APP_ROOT not in sys.path:
    sys.path.insert(0, APP_ROOT)


def main(argv: list[str] | None = None) -> int:
    args = list(argv if argv is not None else sys.argv[1:])
    if not args:
        print("usage: runtime-debug-tui <bundle-dir>", file=sys.stderr)
        return 2
    bundle_dir = args[0]
    try:
        from textual.app import App
        from textual.binding import Binding
        from textual.containers import Vertical
        from textual.widgets import Footer, Header, Input, Static, Tree
    except ImportError as exc:  # pragma: no cover - environment guard
        print(f"TUI requires textual: uv run --with textual python -m runtime_debug.tui.app <bundle> ({exc})",
              file=sys.stderr)
        return 3

    try:
        run = open_run(bundle_dir)
    except Exception as exc:
        print(f"cannot open bundle: {exc}", file=sys.stderr)
        return 3

    return _launch(App, Binding, Vertical, Header, Footer, Static, Input, Tree, bundle_dir, run)


def _launch(TextualApp, Binding, Vertical, Header, Footer, Static, Input, Tree,
            bundle_dir: str, run: dict) -> int:
    class DebugConsoleApp(TextualApp):
        """One bundle; panels fed only by Query Core projections."""

        TITLE = f"runtime-debug — {run['bundleId']}"

        BINDINGS = [
            Binding("t", "show_tree('EXECUTION')", "execution tree"),
            Binding("c", "show_tree('CAUSAL')", "causal tree"),
            Binding("e", "show_errors()", "errors only"),
            Binding("a", "toggle_panel('assets')", "assets"),
            Binding("d", "show_diagnosis()", "diagnosis"),
            Binding("q", "quit", "quit"),
        ]

        def compose(self):
            with Vertical():
                yield Header()
                self.tree = Tree("trace")
                self.tree.show_root = False
                yield self.tree
                self.assets = Static("", id="assets", classes="hidden")
                yield self.assets
                self.diagnosis = Static("", id="diagnosis", classes="hidden")
                yield self.diagnosis
                yield Footer()

        def on_mount(self):
            self.bundle_dir = bundle_dir
            self.only_errors = False
            self.tree_kind = "EXECUTION"
            self.render_tree()

        def render_tree(self):
            self.tree.clear()
            bundle = bundle_source.read_bundle(self.bundle_dir)
            state = filter_state(only_errors=self.only_errors)
            root = self.tree.root
            if self.tree_kind == "EXECUTION":
                result = query.execution_tree(
                    bundle, hide_layers=state["hideLayers"],
                    hide_components=state["hideComponents"],
                    hide_names=state["hideNames"], only_errors=state["onlyErrors"])
                self.render_execution(result, root)
            else:
                self.render_causal(root)

        def render_execution(self, result, root):
            def add(nodes, parent):
                for node in nodes:
                    label = node["name"] or node["spanId"]
                    bracket = f" [{node['outcome']}]" if node.get("outcome") else ""
                    child = parent.add(f"{label}{bracket}")
                    add(node.get("children") or [], child)
            add(result.get("roots") or [], root)

        def render_causal(self, root):
            # The causal/evidence tree lives on the packet layer; a raw bundle
            # carries no semantic chain — show the honest absence.
            for stage in ("raw", "normalized", "fused", "canonical",
                          "semanticAdmission", "affordance", "runtimeState"):
                root.add_leaf(f"{stage} (not in bundle trace)")

        def action_show_tree(self, kind: str):
            self.tree_kind = kind
            self.render_tree()

        def action_show_errors(self):
            self.only_errors = not self.only_errors
            self.render_tree()

        def action_toggle_panel(self, target: str):
            self.query_one(f"#{target}", Static).toggle_class("hidden")

        def action_show_diagnosis(self):
            view = diagnosis_view(bundle_dir=self.bundle_dir)
            lines = [f"terminal: {view['terminal']}"]
            lines += [f"FAILED {f['name']} [{f['outcome']}]" for f in view["failedSpans"]]
            self.diagnosis.update("\n".join(lines) if len(lines) > 1 else "no failed spans")

    app = DebugConsoleApp()
    return app.run()  # pragma: no cover - interactive


if __name__ == "__main__":
    sys.exit(main())