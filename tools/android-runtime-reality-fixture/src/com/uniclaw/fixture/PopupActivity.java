package com.uniclaw.fixture;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.Dialog;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.PopupWindow;
import android.widget.TextView;

/** POPUP-01..10 — deterministic popup/overlay obstructions.
 *  - POPUP-01 immediate AlertDialog
 *  - POPUP-02 dialog after deterministic 2000ms
 *  - POPUP-03 dialog immediate, dismiss disabled for 2000ms (countdown)
 *  - POPUP-04..07 custom dialog with Return at TOP_LEFT/TOP_RIGHT/BOTTOM_LEFT/BOTTOM_RIGHT
 *  - POPUP-08 modal dialog dismissed by system Back only
 *  - POPUP-09 Back triggers confirmation dialog (Back != parent return)
 *  - POPUP-10 PopupWindow overlay anchored offset from center
 *  Label variants cycle across scenarios: Return/Back/Close/Cancel/Dismiss. */
public final class PopupActivity extends Activity {

    private static final String[] LABELS = {"Return", "Back", "Close", "Cancel", "Dismiss"};

    private String scenario;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private Dialog activeDialog;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        scenario = getIntent().getStringExtra(Scenarios.EXTRA_SCENARIO);
        if (scenario == null) scenario = Scenarios.POPUP_01;

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(48, 48, 48, 48);

        TextView title = new TextView(this);
        title.setId(R.id.scenario_title);
        title.setText(scenario + " — " + titleFor(scenario));
        title.setTextSize(18f);
        root.addView(title, lp());

        TextView state = new TextView(this);
        state.setId(R.id.state_text);
        state.setText("STATE: READY");
        root.addView(state, lp());

        final Button trigger = new Button(this);
        trigger.setAllCaps(false);
        trigger.setId(R.id.open_popup);
        trigger.setText(triggerLabel());
        trigger.setOnClickListener(v -> openPopup(state));
        root.addView(trigger, lp());

        Button reset = new Button(this);
        reset.setAllCaps(false);
        reset.setId(R.id.reset_button);
        reset.setText("RESET SCENARIO");
        reset.setOnClickListener(v -> reset());
        root.addView(reset, lp());

        setContentView(root);
    }

    private LinearLayout.LayoutParams lp() {
        return new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
    }

    private String titleFor(String s) {
        switch (s) {
            case Scenarios.POPUP_01: return "Immediate Dialog";
            case Scenarios.POPUP_02: return "Delayed Dialog";
            case Scenarios.POPUP_03: return "Delayed Action";
            case Scenarios.POPUP_04: return "Return Top Left";
            case Scenarios.POPUP_05: return "Return Top Right";
            case Scenarios.POPUP_06: return "Return Bottom Left";
            case Scenarios.POPUP_07: return "Return Bottom Right";
            case Scenarios.POPUP_08: return "System Back Dismiss";
            case Scenarios.POPUP_09: return "Back Triggers Dialog";
            case Scenarios.POPUP_10: return "PopupWindow Overlay";
        }
        return s;
    }

    private String triggerLabel() {
        if (Scenarios.POPUP_02.equals(scenario)) return "Open Delayed Dialog";
        if (Scenarios.POPUP_10.equals(scenario)) return "Show Overlay";
        return "Open Dialog";
    }

    private String dismissLabel(int index) {
        return LABELS[index % LABELS.length];
    }

    private void openPopup(TextView state) {
        handler.removeCallbacksAndMessages(null);
        switch (scenario) {
            case Scenarios.POPUP_01: {
                activeDialog = new AlertDialog.Builder(this)
                        .setTitle(scenario)
                        .setMessage("Immediate popup")
                        .setNegativeButton(dismissLabel(0), (d, w) -> d.dismiss())
                        .setPositiveButton(dismissLabel(1), (d, w) -> d.dismiss())
                        .setCancelable(true)
                        .create();
                activeDialog.show();
                break;
            }
            case Scenarios.POPUP_02: {
                state.setText("STATE: WAITING");
                handler.postDelayed(() -> {
                    activeDialog = new AlertDialog.Builder(this)
                            .setTitle(scenario)
                            .setMessage("Delayed popup")
                            .setNegativeButton(dismissLabel(2), (d, w) -> d.dismiss())
                            .setCancelable(true)
                            .create();
                    activeDialog.show();
                    state.setText("STATE: READY");
                }, 2000);
                break;
            }
            case Scenarios.POPUP_03: {
                activeDialog = new AlertDialog.Builder(this)
                        .setTitle(scenario)
                        .setMessage("Please wait")
                        .setNegativeButton(dismissLabel(3), (d, w) -> d.dismiss())
                        .setCancelable(false)
                        .create();
                activeDialog.show();
                final Button btn = ((AlertDialog) activeDialog).getButton(AlertDialog.BUTTON_NEGATIVE);
                btn.setEnabled(false);
                state.setText("STATE: WAITING");
                handler.postDelayed(() -> {
                    btn.setEnabled(true);
                    btn.setText("Ready");
                    state.setText("STATE: READY");
                }, 2000);
                break;
            }
            case Scenarios.POPUP_04:
            case Scenarios.POPUP_05:
            case Scenarios.POPUP_06:
            case Scenarios.POPUP_07:
                showPositionedDialog();
                break;
            case Scenarios.POPUP_08: {
                activeDialog = new AlertDialog.Builder(this)
                        .setTitle(scenario)
                        .setMessage("Back dismisses me")
                        .setCancelable(true)
                        .create();
                activeDialog.setOnCancelListener(d -> state.setText("STATE: READY"));
                activeDialog.show();
                break;
            }
            case Scenarios.POPUP_09:
                // Trigger the confirmation dialog via system Back (onBackPressed).
                state.setText("STATE: READY — press Back");
                break;
            case Scenarios.POPUP_10:
                showPopupWindow();
                break;
        }
    }

    private void showPositionedDialog() {
        String label = dismissLabel((scenario.equals(Scenarios.POPUP_04)) ? 0
                : (scenario.equals(Scenarios.POPUP_05)) ? 1
                : (scenario.equals(Scenarios.POPUP_06)) ? 2 : 3);
        final Dialog d = new Dialog(this);
        d.setTitle(scenario);

        FrameLayout content = new FrameLayout(this);
        content.setPadding(64, 64, 64, 64);
        FrameLayout.LayoutParams flp = new FrameLayout.LayoutParams(600, 400);

        TextView msg = new TextView(this);
        msg.setText("Custom dialog — Return at " + positionName());
        msg.setTextSize(16f);
        content.addView(msg, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT,
                Gravity.CENTER));

        Button ret = new Button(this);
        ret.setAllCaps(false);
        ret.setId(R.id.return_button);
        ret.setText(label);
        ret.setOnClickListener(v -> d.dismiss());
        int grav = scenario.equals(Scenarios.POPUP_04) ? (Gravity.LEFT | Gravity.TOP)
                : scenario.equals(Scenarios.POPUP_05) ? (Gravity.RIGHT | Gravity.TOP)
                : scenario.equals(Scenarios.POPUP_06) ? (Gravity.LEFT | Gravity.BOTTOM)
                : (Gravity.RIGHT | Gravity.BOTTOM);
        content.addView(ret, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT, grav));

        d.setContentView(content, flp);
        activeDialog = d;
        d.show();
    }

    private String positionName() {
        switch (scenario) {
            case Scenarios.POPUP_04: return "TOP LEFT";
            case Scenarios.POPUP_05: return "TOP RIGHT";
            case Scenarios.POPUP_06: return "BOTTOM LEFT";
            default: return "BOTTOM RIGHT";
        }
    }

    private void showPopupWindow() {
        Button anchor = findViewById(R.id.open_popup);
        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(32, 32, 32, 32);
        content.setBackgroundColor(0xFFCCCCCC);
        TextView tv = new TextView(this);
        tv.setText("PopupWindow overlay");
        content.addView(tv, lp());
        Button close = new Button(this);
        close.setAllCaps(false);
        close.setId(R.id.return_button);
        close.setText(dismissLabel(4));
        close.setOnClickListener(v -> popup.dismiss());
        content.addView(close, lp());
        popup = new PopupWindow(content,
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT, true);
        popup.setOutsideTouchable(true);
        popup.showAtLocation(anchor, Gravity.TOP | Gravity.CENTER_HORIZONTAL, 0, 400);
    }

    private PopupWindow popup;

    @Override
    public void onBackPressed() {
        if (Scenarios.POPUP_09.equals(scenario)) {
            // Back does NOT leave; it creates an obstruction.
            new AlertDialog.Builder(this)
                    .setTitle(scenario)
                    .setMessage("Leave this page?")
                    .setNegativeButton("Stay", (d, w) -> d.dismiss())
                    .setPositiveButton("Return", (d, w) -> finish())
                    .setCancelable(false)
                    .show();
            return;
        }
        super.onBackPressed();
    }

    private void reset() {
        handler.removeCallbacksAndMessages(null);
        if (activeDialog != null && activeDialog.isShowing()) activeDialog.dismiss();
        if (popup != null && popup.isShowing()) popup.dismiss();
        TextView state = findViewById(R.id.state_text);
        if (state != null) state.setText("STATE: RESET");
    }
}
