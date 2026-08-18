package com.uniclaw.fixture;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

/** Deterministic scenario launcher. Every entry is a plain Button with the
 *  scenario id as its text and a stable contentDescription; tapping opens the
 *  owning host activity with the scenario extra. */
public final class MainActivity extends Activity {

    private static final String[][] ENTRIES = {
        {Scenarios.SCROLL_01, "Long List"},
        {Scenarios.SCROLL_02, "Duplicate Titles"},
        {Scenarios.SCROLL_03, "Mixed Controls"},
        {Scenarios.SCROLL_04, "Dynamic Mutation"},
        {Scenarios.POPUP_01, "Immediate Dialog"},
        {Scenarios.POPUP_02, "Delayed Dialog"},
        {Scenarios.POPUP_03, "Delayed Action"},
        {Scenarios.POPUP_04, "Return Top Left"},
        {Scenarios.POPUP_05, "Return Top Right"},
        {Scenarios.POPUP_06, "Return Bottom Left"},
        {Scenarios.POPUP_07, "Return Bottom Right"},
        {Scenarios.POPUP_08, "System Back Dismiss"},
        {Scenarios.POPUP_09, "Back Triggers Dialog"},
        {Scenarios.POPUP_10, "PopupWindow Overlay"},
        {Scenarios.NAV_01, "Single Child"},
        {Scenarios.NAV_02, "Multi Level"},
        {Scenarios.NAV_03, "Sibling Return"},
        {Scenarios.NAV_04, "Unexpected Destination"},
        {Scenarios.COMPOSE_01, "Scroll Then Popup"},
        {Scenarios.COMPOSE_02, "Navigate Then Popup"},
        {Scenarios.COMPOSE_03, "Popup Then Return"},
        {Scenarios.COMPOSE_04, "Scroll + Duplicate + Popup"},
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        TextView title = new TextView(this);
        title.setText("UniClaw Reality Fixture — scenario launcher");
        title.setTextSize(18f);
        title.setPadding(24, 24, 24, 8);

        LinearLayout col = new LinearLayout(this);
        col.setOrientation(LinearLayout.VERTICAL);
        col.addView(title, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        for (String[] e : ENTRIES) {
            final String scenario = e[0];
            Button b = new Button(this);
            b.setText(scenario + "  " + e[1]);
            b.setContentDescription(scenario);
            b.setOnClickListener(v -> launch(scenario));
            col.addView(b, new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        }

        ScrollView scroll = new ScrollView(this);
        scroll.setId(R.id.scenario_list);
        scroll.addView(col);
        setContentView(scroll);
    }

    private void launch(String scenario) {
        String host = Scenarios.hostFor(scenario);
        if (host == null) return;
        Intent i = new Intent();
        i.setClassName(this, host);
        i.putExtra(Scenarios.EXTRA_SCENARIO, scenario);
        startActivity(i);
    }
}
