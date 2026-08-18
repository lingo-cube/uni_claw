package com.uniclaw.fixture;

import android.app.Activity;
import android.os.Bundle;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;

/** CAPSTONE normal child page. Unique return anchor: text == "Fixture Root".
 *  Returning (finish) counts as a genuinely visited child. */
public final class CapstoneChildActivity extends Activity {
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        int index = getIntent().getIntExtra("index", 1);

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(48, 180, 48, 48);

        TextView title = new TextView(this);
        title.setId(R.id.scenario_title);
        title.setText(String.format("Child %02d", index));
        title.setTextSize(34f);
        root.addView(title, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        Button ret = new Button(this);
        ret.setId(R.id.return_button);
        ret.setAllCaps(false);
        ret.setTextSize(26f);
        ret.setText("Fixture Root");
        ret.setOnClickListener(v -> {
            SharedState.childReturned();
            finish();
        });
        root.addView(ret, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        setContentView(root);
    }
}
