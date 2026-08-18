package com.uniclaw.fixture;

/** CAPSTONE shared external completion state. Root increments only when a
 *  child is genuinely entered and successfully returns. */
public final class SharedState {
    private SharedState() {}

    public static final int CHILD_COUNT = 8;
    public static int visitedCount = 0;

    public static synchronized void reset() { visitedCount = 0; }

    public static synchronized void childReturned() { visitedCount++; }

    public static synchronized String stateLine() {
        return "Visited " + visitedCount + "/" + CHILD_COUNT
            + (visitedCount >= CHILD_COUNT ? "  CAPSTONE COMPLETE" : "");
    }
}
