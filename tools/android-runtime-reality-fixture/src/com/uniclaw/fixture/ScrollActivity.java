package com.uniclaw.fixture;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.Switch;
import android.widget.TextView;

import java.util.ArrayList;
import java.util.List;

/** SCROLL-01..04 — deterministic long lists. Classic ListView (viewport-only
 *  materialization), 40 rows for SCROLL-01. Row height ~140dp so a 1080x1920
 *  emulator shows ~8-12 rows per viewport. */
public final class ScrollActivity extends Activity {

    private static final int ROW_HEIGHT_DP = 140;

    private String scenario;
    private ListView list;
    private RowAdapter adapter;
    private final List<Row> rows = new ArrayList<>();
    private TextView stateLine;

    /** One logical row. identity != visible text (SCROLL-02 duplicates). */
    private static final class Row {
        final int identity;
        final String title;
        final int type; // 0=nav 1=switch 2=button 3=checkbox 4=ambiguous
        boolean checked;
        Row(int identity, String title, int type) {
            this.identity = identity;
            this.title = title;
            this.type = type;
        }
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        scenario = getIntent().getStringExtra(Scenarios.EXTRA_SCENARIO);
        if (scenario == null) scenario = Scenarios.SCROLL_01;

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);

        TextView title = new TextView(this);
        title.setId(R.id.scenario_title);
        title.setText(scenario + " — " + titleFor(scenario));
        title.setTextSize(16f);
        title.setPadding(24, 24, 24, 8);
        root.addView(title, lp());

        stateLine = new TextView(this);
        stateLine.setId(R.id.state_text);
        stateLine.setText("");
        stateLine.setPadding(24, 4, 24, 4);
        root.addView(stateLine, lp());

        list = new ListView(this);
        list.setId(R.id.scenario_list);
        root.addView(list, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));

        buildRows(scenario);

        if (Scenarios.SCROLL_04.equals(scenario)) {
            LinearLayout bar = new LinearLayout(this);
            Button ins = new Button(this);
        ins.setAllCaps(false);
            ins.setId(R.id.insert_row);
            ins.setText("Insert Row");
            ins.setOnClickListener(v -> insertRow());
            Button rem = new Button(this);
        rem.setAllCaps(false);
            rem.setId(R.id.remove_row);
            rem.setText("Remove Row");
            rem.setOnClickListener(v -> removeRow());
            bar.addView(ins, lpW());
            bar.addView(rem, lpW());
            root.addView(bar, lp());
        }

        Button reset = new Button(this);
        reset.setAllCaps(false);
        reset.setId(R.id.reset_button);
        reset.setText("RESET SCENARIO");
        reset.setOnClickListener(v -> resetScenario());
        root.addView(reset, lp());

        adapter = new RowAdapter();
        list.setAdapter(adapter);
        setContentView(root);
    }

    private LinearLayout.LayoutParams lp() {
        return new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
    }

    private LinearLayout.LayoutParams lpW() {
        return new LinearLayout.LayoutParams(0,
                ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
    }

    private void buildRows(String s) {
        rows.clear();
        switch (s) {
            case Scenarios.SCROLL_01:
                for (int i = 1; i <= 40; i++) {
                    rows.add(new Row(i, String.format("Item %02d", i), 0));
                }
                break;
            case Scenarios.SCROLL_02:
                // Distinct logical rows with duplicate visible text.
                String[] seq = {"Item A", "Shared", "Item B", "Shared",
                        "Item C", "Item D", "Shared", "Item E", "Shared", "Item F"};
                for (int i = 0; i < seq.length; i++) {
                    rows.add(new Row(100 + i, seq[i], 0));
                }
                break;
            case Scenarios.SCROLL_03:
                rows.add(new Row(200, "Navigation Row 1", 0));
                rows.add(new Row(201, "Local Switch 1", 1));
                rows.add(new Row(202, "Local Button", 2));
                rows.add(new Row(203, "Navigation Row 2", 0));
                rows.add(new Row(204, "Local Checkbox", 3));
                rows.add(new Row(205, "Navigation Row 3", 0));
                rows.add(new Row(206, "Ambiguous Row", 4));
                rows.add(new Row(207, "Local Switch 2", 1));
                rows.add(new Row(208, "Navigation Row 4", 0));
                break;
            case Scenarios.SCROLL_04:
                rows.add(new Row(300, "A", 0));
                rows.add(new Row(301, "B", 0));
                rows.add(new Row(302, "C", 0));
                rows.add(new Row(303, "D", 0));
                break;
        }
    }

    private void insertRow() {
        rows.add(2, new Row(399, "X", 0)); // deterministic: A B X C D
        adapter.notifyDataSetChanged();
        stateLine.setText("STATE: X inserted");
    }

    private void removeRow() {
        for (int i = 0; i < rows.size(); i++) {
            if (rows.get(i).identity == 399) {
                rows.remove(i);
                break;
            }
        }
        adapter.notifyDataSetChanged();
        stateLine.setText("STATE: X removed");
    }

    private void resetScenario() {
        for (Row r : rows) r.checked = false;
        buildRows(scenario);
        adapter.notifyDataSetChanged();
        list.setSelection(0);
        stateLine.setText("STATE: RESET");
    }

    private static String titleFor(String s) {
        switch (s) {
            case Scenarios.SCROLL_01: return "Long List";
            case Scenarios.SCROLL_02: return "Duplicate Titles";
            case Scenarios.SCROLL_03: return "Mixed Controls";
            case Scenarios.SCROLL_04: return "Dynamic Mutation";
        }
        return s;
    }

    private void openDetail(int identity, String text) {
        Intent i = new Intent(this, DetailActivity.class);
        i.putExtra("identity", identity);
        i.putExtra("text", text);
        startActivity(i);
    }

    private final class RowAdapter extends BaseAdapter {
        @Override public int getCount() { return rows.size(); }
        @Override public Object getItem(int p) { return rows.get(p); }
        @Override public long getItemId(int p) { return rows.get(p).identity; }
        @Override public int getViewTypeCount() { return 5; }
        @Override public int getItemViewType(int p) { return rows.get(p).type; }

        @Override
        public View getView(int position, View convertView, ViewGroup parent) {
            final Row r = rows.get(position);
            LinearLayout row = new LinearLayout(ScrollActivity.this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setPadding(24, 16, 24, 16);
            int h = (int) (ROW_HEIGHT_DP * getResources().getDisplayMetrics().density);
            LinearLayout.LayoutParams rlp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, h);

            TextView tv = new TextView(ScrollActivity.this);
            tv.setId(R.id.row_title);
            tv.setText(r.title);
            tv.setTextSize(16f);
            tv.setLayoutParams(new LinearLayout.LayoutParams(0,
                    ViewGroup.LayoutParams.MATCH_PARENT, 1f));

            switch (r.type) {
                case 0: // navigation row
                    row.setClickable(true);
                    row.setFocusable(true);
                    row.addView(tv);
                    row.setOnClickListener(v -> openDetail(r.identity, "Detail " + r.title));
                    break;
                case 1: { // local switch — mutates current-page state only
                    final Switch sw = new Switch(ScrollActivity.this);
                    sw.setId(R.id.local_switch);
                    sw.setChecked(r.checked);
                    sw.setOnCheckedChangeListener((b, checked) -> {
                        r.checked = checked;
                        stateLine.setText("STATE: " + r.title + "=" + (checked ? "ON" : "OFF"));
                    });
                    row.addView(tv);
                    row.addView(sw);
                    break;
                }
                case 2: { // local button — mutates current-page state only
                    Button btn = new Button(ScrollActivity.this);
        btn.setAllCaps(false);
                    btn.setText("Press");
                    btn.setOnClickListener(v ->
                            stateLine.setText("STATE: " + r.title + " pressed"));
                    row.addView(tv);
                    row.addView(btn);
                    break;
                }
                case 3: { // local checkbox
                    final CheckBox cb = new CheckBox(ScrollActivity.this);
                    cb.setId(R.id.local_checkbox);
                    cb.setChecked(r.checked);
                    cb.setOnCheckedChangeListener((b, checked) -> {
                        r.checked = checked;
                        stateLine.setText("STATE: " + r.title + "=" + (checked ? "checked" : "unchecked"));
                    });
                    row.addView(tv);
                    row.addView(cb);
                    break;
                }
                default: { // ambiguous clickable row — navigates (per SCROLL-03 spec)
                    row.setClickable(true);
                    row.setFocusable(true);
                    row.addView(tv);
                    row.setOnClickListener(v -> openDetail(r.identity, "Detail " + r.title));
                    break;
                }
            }
            row.setLayoutParams(rlp);
            return row;
        }
    }
}
