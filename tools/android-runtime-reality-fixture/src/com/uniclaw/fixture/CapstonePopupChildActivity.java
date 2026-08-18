package com.uniclaw.fixture;

import android.app.Activity;
import android.app.Dialog;
import android.os.Bundle;
import android.view.Gravity;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;

/** CAPSTONE popup child (below-fold). IMMEDIATE custom Dialog on entry with a
 *  single Button "Fixture Root" (exact text — plain Button, not AlertDialog, so
 *  the text is NOT allCaps). The dialog's button is the ONLY "Fixture Root"
 *  anchor on this page (no layout return button), so the Agent's parent-return
 *  path taps it: dismiss + finish. This is
 *  OBSTRUCTION_PRESENT_PARENT_RETURN_COMPOSITION — NOT PopupObstructionRecovery. */
public final class CapstonePopupChildActivity extends Activity {
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(48, 180, 48, 48);

        TextView title = new TextView(this);
        title.setId(R.id.scenario_title);
        title.setText("Child 06");
        title.setTextSize(34f);
        root.addView(title, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        TextView hint = new TextView(this);
        hint.setId(R.id.state_text);
        hint.setText("Obstruction pending");
        root.addView(hint, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        setContentView(root);

        // Immediate, non-cancelable custom dialog. Single "Fixture Root" button
        // doubles as the parent-return anchor: dismiss + finish.
        final Dialog dialog = new Dialog(this);
        dialog.setTitle("Obstruction on Child 06");
        dialog.setCancelable(false);
        dialog.setCanceledOnTouchOutside(false);
        FrameLayout content = new FrameLayout(this);
        content.setPadding(64, 64, 64, 64);
        TextView msg = new TextView(this);
        msg.setText("Immediate popup");
        msg.setTextSize(16f);
        content.addView(msg, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT,
                Gravity.CENTER));
        Button ret = new Button(this);
        ret.setId(R.id.return_button);
        ret.setAllCaps(false);
        ret.setTextSize(36f);
        ret.setText("Fixture Root");
        ret.setBackgroundColor(0xFF3D5AFE);
        ret.setTextColor(0xFFFFFFFF);
        ret.setOnClickListener(v -> {
            dialog.dismiss();
            SharedState.childReturned();
            finish();
        });
        content.addView(ret, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT,
                Gravity.CENTER | Gravity.BOTTOM));
        dialog.setContentView(content, new FrameLayout.LayoutParams(700, 420));
        dialog.show();
    }
}
