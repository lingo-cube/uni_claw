package com.uniclaw.fixture;

import android.app.Activity;
import android.app.AlertDialog;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;

import java.util.ArrayList;
import java.util.List;

/** COMPOSE-01..04 — composed deterministic scenarios.
 *  COMPOSE-01: long list, popup trigger below fold (scroll -> tap -> popup).
 *  COMPOSE-02: page A -> B; B auto-shows popup after 1000ms.
 *  COMPOSE-03: page A -> B -> immediate popup; dismiss then Back -> A.
 *  COMPOSE-04: long list with duplicate titles + popup trigger below fold. */
public final class ComposeActivity extends Activity {

    private final Handler handler = new Handler(Looper.getMainLooper());
    private String scenario;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        scenario = getIntent().getStringExtra(Scenarios.EXTRA_SCENARIO);
        if (scenario == null && "com.uniclaw.fixture.action.CAPSTONE".equals(getIntent().getAction())) {
            scenario = Scenarios.COMPOSE_05;
        }
        if (scenario == null) scenario = Scenarios.COMPOSE_01;

        int depth = getIntent().getIntExtra("depth", 0);
        switch (scenario) {
            case Scenarios.COMPOSE_01:
            case Scenarios.COMPOSE_04:
                renderScrollThenPopup();
                break;
            case Scenarios.COMPOSE_02:
                renderNavThenDelayedPopup(depth);
                break;
            case Scenarios.COMPOSE_03:
                renderNavPopupReturn(depth);
                break;
            case Scenarios.COMPOSE_05:
                renderCapstoneRoot();
                break;
        }
    }

    private LinearLayout.LayoutParams lp() {
        return new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
    }

    // ── COMPOSE-01 / COMPOSE-04 ─────────────────────────────────────────────

    private void renderScrollThenPopup() {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        TextView title = new TextView(this);
        title.setId(R.id.scenario_title);
        title.setText(scenario);
        title.setTextSize(16f);
        title.setPadding(24, 24, 24, 8);
        root.addView(title, lp());

        final List<String> rows = new ArrayList<>();
        if (Scenarios.COMPOSE_04.equals(scenario)) {
            String[] seq = {"Item A", "Shared", "Item B", "Shared", "Item C", "Shared",
                    "Item D", "Shared", "Item E", "Shared", "Item F", "Shared",
                    "Item G", "Shared", "Item H", "Shared", "Item I", "Shared", "Item J"};
            for (String s : seq) rows.add(s);
        } else {
            for (int i = 1; i <= 30; i++) rows.add(String.format("Row %02d", i));
        }

        ListView list = new ListView(this);
        list.setId(R.id.scenario_list);
        list.setAdapter(new BaseAdapter() {
            @Override public int getCount() { return rows.size() + 1; }
            @Override public Object getItem(int p) { return p; }
            @Override public long getItemId(int p) { return p; }

            @Override
            public android.view.View getView(int position, android.view.View cv, ViewGroup parent) {
                if (position == rows.size()) {
                    Button trig = new Button(ComposeActivity.this);
        trig.setAllCaps(false);
                    trig.setId(R.id.open_popup);
                    trig.setText("Open Popup (below fold)");
                    trig.setOnClickListener(v -> new AlertDialog.Builder(ComposeActivity.this)
                            .setTitle(scenario)
                            .setMessage("Compose popup")
                            .setNegativeButton("Return", (d, w) -> d.dismiss())
                            .show());
                    return trig;
                }
                LinearLayout row = new LinearLayout(ComposeActivity.this);
                row.setPadding(24, 16, 24, 16);
                TextView tv = new TextView(ComposeActivity.this);
                tv.setId(R.id.row_title);
                tv.setText(rows.get(position));
                tv.setTextSize(16f);
                row.addView(tv);
                return row;
            }
        });
        root.addView(list, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));

        Button reset = new Button(this);
        reset.setAllCaps(false);
        reset.setId(R.id.reset_button);
        reset.setText("RESET SCENARIO");
        reset.setOnClickListener(v -> {
            handler.removeCallbacksAndMessages(null);
            recreate();
        });
        root.addView(reset, lp());
        setContentView(root);
    }

    // ── COMPOSE-05 CAPSTONE ────────────────────────────────────────────────

    private void renderCapstoneRoot() {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);

        TextView title = new TextView(this);
        title.setId(R.id.scenario_title);
        title.setText("Fixture Root");
        title.setTextSize(24f);
        title.setPadding(24, 180, 24, 8);
        root.addView(title, lp());

        final TextView state = new TextView(this);
        state.setId(R.id.state_text);
        state.setText(SharedState.stateLine());
        state.setTextSize(24f);
        state.setPadding(24, 4, 24, 4);
        root.addView(state, lp());

        // STATIC rows in a ScrollView (no ListView recycling): every row keeps
        // its "Child XX" title TextView with android:id/title at ALL scroll
        // positions, so the structured evidence always carries the row title.
        // The title TextView FILLS the whole row (MATCH_PARENT height): when a
        // row is partially scrolled above the fold, uiautomator clips the row
        // to its visible sliver and DROPS child views whose own visible rect is
        // empty. A small top-anchored TextView would vanish entirely; a
        // fill-height TextView always keeps a non-empty visible portion, so the
        // dump always carries the row's title text.
        // No RESET button on this page (reset is out-of-band: force-stop + the
        // CAPSTONE launch intent; SharedState is process-local and resets on
        // force-stop).
        final String[] children = {"Child 01", "Child 02", "Child 03", "Child 04",
                "Child 05", "Child 06", "Child 07", "Child 08"};
        LinearLayout list = new LinearLayout(this);
        list.setOrientation(LinearLayout.VERTICAL);
        final int rowH = (int) (160 * getResources().getDisplayMetrics().density);
        for (int i = 0; i < children.length; i++) {
            final int idx = i + 1;
            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setGravity(android.view.Gravity.CENTER_VERTICAL);
            row.setPadding(24, 0, 24, 0);
            row.setClickable(true);
            row.setFocusable(true);
            TextView tv = new TextView(this);
            tv.setId(android.R.id.title);
            tv.setText(children[i]);
            tv.setTextSize(28f);
            tv.setGravity(android.view.Gravity.CENTER_VERTICAL);
            row.addView(tv, new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT,
                    ViewGroup.LayoutParams.MATCH_PARENT));
            row.setLayoutParams(new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, rowH));
            row.setOnClickListener(v -> openCapstoneChild(idx));
            list.addView(row);
        }
        android.widget.ScrollView scroll = new android.widget.ScrollView(this);
        scroll.setId(R.id.scenario_list);
        scroll.addView(list);
        root.addView(scroll, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));

        setContentView(root);
    }

    private void openCapstoneChild(int index) {
        Class<?> target = index == 6 ? CapstonePopupChildActivity.class : CapstoneChildActivity.class;
        startActivity(new android.content.Intent(this, target).putExtra("index", index));
    }

    @Override
    protected void onResume() {
        super.onResume();
        TextView state = findViewById(R.id.state_text);
        if (state != null && Scenarios.COMPOSE_05.equals(scenario)) {
            state.setText(SharedState.stateLine());
        }
    }

    private void openB() {
        startActivity(new android.content.Intent(this, ComposeActivity.class)
                .putExtra(Scenarios.EXTRA_SCENARIO, scenario)
                .putExtra("depth", 1));
    }

    // ── COMPOSE-02 / COMPOSE-03 ─────────────────────────────────────────────

    private void renderNavThenDelayedPopup(final int depth) {
        final LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(48, 48, 48, 48);
        TextView title = new TextView(this);
        title.setId(R.id.scenario_title);
        title.setText(scenario + " — Page " + (depth == 0 ? "A" : "B"));
        title.setTextSize(20f);
        root.addView(title, lp());

        if (depth == 0) {
            Button next = new Button(this);
        next.setAllCaps(false);
            next.setId(R.id.open_child);
            next.setText("Open B");
            next.setOnClickListener(v -> openB());
            root.addView(next, lp());
        } else {
            TextView state = new TextView(this);
            state.setId(R.id.state_text);
            state.setText("STATE: WAITING");
            root.addView(state, lp());
            handler.postDelayed(() -> {
                new AlertDialog.Builder(this)
                        .setTitle(scenario)
                        .setMessage("Delayed popup on B")
                        .setNegativeButton("Return", (d, w) -> d.dismiss())
                        .show();
                state.setText("STATE: READY");
            }, 1000);
        }
        Button reset = new Button(this);
        reset.setAllCaps(false);
        reset.setId(R.id.reset_button);
        reset.setText("RESET SCENARIO");
        reset.setOnClickListener(v -> {
            handler.removeCallbacksAndMessages(null);
            recreate();
        });
        root.addView(reset, lp());
        setContentView(root);
    }

    private void renderNavPopupReturn(final int depth) {
        final LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(48, 48, 48, 48);
        TextView title = new TextView(this);
        title.setId(R.id.scenario_title);
        title.setText(scenario + " — Page " + (depth == 0 ? "A" : "B"));
        title.setTextSize(20f);
        root.addView(title, lp());

        if (depth == 0) {
            Button next = new Button(this);
        next.setAllCaps(false);
            next.setId(R.id.open_child);
            next.setText("Open B");
            next.setOnClickListener(v -> openB());
            root.addView(next, lp());
        } else {
            Button show = new Button(this);
        show.setAllCaps(false);
            show.setId(R.id.open_popup);
            show.setText("Open Popup");
            show.setOnClickListener(v -> new AlertDialog.Builder(this)
                    .setTitle(scenario)
                    .setMessage("Popup on B")
                    .setNegativeButton("Return", (d, w) -> d.dismiss())
                    .show());
            root.addView(show, lp());
            // Back returns to page A (activity stack).
        }
        Button reset = new Button(this);
        reset.setAllCaps(false);
        reset.setId(R.id.reset_button);
        reset.setText("RESET SCENARIO");
        reset.setOnClickListener(v -> recreate());
        root.addView(reset, lp());
        setContentView(root);
    }
}
