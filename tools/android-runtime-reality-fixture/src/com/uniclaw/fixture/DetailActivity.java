package com.uniclaw.fixture;

import android.app.Activity;
import android.os.Bundle;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;

/** Deterministic detail page opened by navigation rows. Back returns to parent. */
public final class DetailActivity extends Activity {
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        String text = getIntent().getStringExtra("text");
        int identity = getIntent().getIntExtra("identity", -1);

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(48, 48, 48, 48);

        TextView tv = new TextView(this);
        tv.setId(R.id.row_title);
        tv.setText(text == null ? "Detail" : text + "  (id=" + identity + ")");
        tv.setTextSize(22f);
        root.addView(tv, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        Button ret = new Button(this);
        ret.setAllCaps(false);
        ret.setId(R.id.return_button);
        ret.setText("Return");
        ret.setOnClickListener(v -> finish());
        root.addView(ret, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        setContentView(root);
    }
}
