package com.uniclaw.fixture;

/** Scenario identifiers shared by launcher, deep links and hosts. */
public final class Scenarios {
    private Scenarios() {}

    public static final String SCROLL_01 = "SCROLL_01";
    public static final String SCROLL_02 = "SCROLL_02";
    public static final String SCROLL_03 = "SCROLL_03";
    public static final String SCROLL_04 = "SCROLL_04";

    public static final String POPUP_01 = "POPUP_01";
    public static final String POPUP_02 = "POPUP_02";
    public static final String POPUP_03 = "POPUP_03";
    public static final String POPUP_04 = "POPUP_04";
    public static final String POPUP_05 = "POPUP_05";
    public static final String POPUP_06 = "POPUP_06";
    public static final String POPUP_07 = "POPUP_07";
    public static final String POPUP_08 = "POPUP_08";
    public static final String POPUP_09 = "POPUP_09";
    public static final String POPUP_10 = "POPUP_10";

    public static final String NAV_01 = "NAV_01";
    public static final String NAV_02 = "NAV_02";
    public static final String NAV_03 = "NAV_03";
    public static final String NAV_04 = "NAV_04";

    public static final String COMPOSE_01 = "COMPOSE_01";
    public static final String COMPOSE_02 = "COMPOSE_02";
    public static final String COMPOSE_03 = "COMPOSE_03";
    public static final String COMPOSE_04 = "COMPOSE_04";
    public static final String COMPOSE_05 = "COMPOSE_05";

    public static final String EXTRA_SCENARIO = "scenario";

    /** Which host activity owns a scenario. */
    public static String hostFor(String scenario) {
        if (scenario == null) return null;
        if (scenario.startsWith("SCROLL")) return "com.uniclaw.fixture.ScrollActivity";
        if (scenario.startsWith("POPUP")) return "com.uniclaw.fixture.PopupActivity";
        if (scenario.startsWith("NAV")) return "com.uniclaw.fixture.NavActivity";
        if (scenario.startsWith("COMPOSE")) return "com.uniclaw.fixture.ComposeActivity";
        return null;
    }
}
