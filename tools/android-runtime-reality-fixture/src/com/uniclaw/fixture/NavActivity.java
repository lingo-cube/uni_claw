package com.uniclaw.fixture;

import android.app.Activity;
import android.os.Bundle;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;

/** NAV-01..04 — deterministic navigation.
 *  NAV-01: A -> B -> Return; Back B->A.
 *  NAV-02: A -> B -> C -> D (multi-level).
 *  NAV-03: Parent with Child A/B/C; each child Returns to the SAME parent.
 *  NAV-04: "Open Child" on A deterministically lands on C (Expected=B, Actual=C). */
public final class NavActivity extends Activity {

    private static final String[] PAGE_LABEL = {"Page A", "Page B", "Page C", "Page D"};

    private String scenario;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        scenario = getIntent().getStringExtra(Scenarios.EXTRA_SCENARIO);
        if (scenario == null) scenario = Scenarios.NAV_01;
        int depth = getIntent().getIntExtra("depth", 0);
        renderPage(depth);
    }

    /** Renders the page at depth; pushes deeper pages as new activity instances
     *  so the activity back stack provides deterministic parent return. */
    private void renderPage(final int depth) {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(48, 48, 48, 48);

        TextView title = new TextView(this);
        title.setId(R.id.scenario_title);
        title.setText(scenario + " — " + PAGE_LABEL[depth]);
        title.setTextSize(20f);
        root.addView(title, lp());

        if (Scenarios.NAV_03.equals(scenario) && depth == 0) {
            // Sibling parent: three children, all return to this same parent.
            String[] children = {"Child A", "Child B", "Child C"};
            for (int i = 0; i < children.length; i++) {
                final int child = i + 1;
                Button b = new Button(this);
        b.setAllCaps(false);
                b.setText(children[i]);
                b.setContentDescription(children[i]);
                b.setOnClickListener(v -> openChild(child, 1));
                root.addView(b, lp());
            }
        } else if (Scenarios.NAV_02.equals(scenario) && depth < 3) {
            Button next = new Button(this);
        next.setAllCaps(false);
            next.setText("Open " + PAGE_LABEL[depth + 1]);
            next.setId(R.id.open_child);
            next.setOnClickListener(v -> openChild(depth + 1, depth + 1));
            root.addView(next, lp());
        } else if (Scenarios.NAV_04.equals(scenario) && depth == 0) {
            Button next = new Button(this);
        next.setAllCaps(false);
            next.setText("Open Child");
            next.setId(R.id.open_child);
            // Deterministic unexpected destination: expected B, actual C.
            next.setOnClickListener(v -> openChild(2, 1));
            root.addView(next, lp());
        } else if (depth < 3 && !Scenarios.NAV_03.equals(scenario)) {
            Button next = new Button(this);
        next.setAllCaps(false);
            next.setText(Scenarios.NAV_01.equals(scenario) ? "Open Child" : "Open " + PAGE_LABEL[depth + 1]);
            next.setId(R.id.open_child);
            next.setOnClickListener(v -> openChild(depth + 1, depth + 1));
            root.addView(next, lp());
        }

        if (depth > 0) {
            Button ret = new Button(this);
        ret.setAllCaps(false);
            ret.setId(R.id.return_button);
            ret.setText("Return");
            ret.setOnClickListener(v -> finish());
            root.addView(ret, lp());
        }

        Button reset = new Button(this);
        reset.setAllCaps(false);
        reset.setId(R.id.reset_button);
        reset.setText("RESET SCENARIO");
        reset.setOnClickListener(v -> {
            getIntent().removeExtra("depth");
            finish();
        });
        root.addView(reset, lp());

        setContentView(root);
    }

    private void openChild(int childDepth, int expectedDepth) {
        android.content.Intent i = new android.content.Intent(this, NavActivity.class);
        i.putExtra(Scenarios.EXTRA_SCENARIO, scenario);
        i.putExtra("depth", childDepth);
        i.putExtra("expected", expectedDepth);
        startActivity(i);
    }

    private LinearLayout.LayoutParams lp() {
        return new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
    }
}
