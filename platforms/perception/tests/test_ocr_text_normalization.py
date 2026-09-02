"""P-OCR: ocr-text-normalization spec scenarios (perception-ocr-en-v4-normalization).

Each scenario in specs/perception/ocr-text-normalization/spec.md maps to a
test here:
  * concatenated-token-recovery (glued split + already-correct preserved)
  * trailing-punctuation-strip (period stripped, `&` preserved)
  * style-normalization (digit/letter + connector variants collapse)
  * unknown-cases-fail-closed (never fabricates)
"""
from __future__ import annotations

import unittest

from uniclaw_perception.ocr.normalize import normalize_ocr_token


class NormalizeSpecScenarios(unittest.TestCase):
    """Direct mapping to the ocr-text-normalization spec."""

    # ── concatenated-token-recovery ──
    def test_glued_token_is_split(self):
        self.assertEqual(
            normalize_ocr_token("Disableadbauthorizationtimeout"),
            "Disable adb authorization timeout")

    def test_already_correct_token_preserved(self):
        self.assertEqual(
            normalize_ocr_token("Enable Bluetooth stack log"),
            "Enable Bluetooth stack log")

    def test_observed_glued_enable_bluetooth(self):
        self.assertEqual(
            normalize_ocr_token("EnableBluetoothstacklog"),
            "Enable Bluetooth stack log")

    def test_hci_acronym_l_misread_fixed(self):
        self.assertEqual(
            normalize_ocr_token("Enable Bluetooth HCl snoop log"),
            "Enable Bluetooth HCI snoop log")
        self.assertEqual(
            normalize_ocr_token("Bluetooth HCl snoop log filtering"),
            "Bluetooth HCI snoop log filtering")

    # ── trailing-punctuation-strip ──
    def test_trailing_period_stripped(self):
        self.assertEqual(
            normalize_ocr_token("Developer options."),
            "Developer options")

    def test_semantic_ampersand_preserved(self):
        self.assertEqual(
            normalize_ocr_token("Network & internet"),
            "Network & internet")

    def test_use_developer_options_trailing_period(self):
        self.assertEqual(
            normalize_ocr_token("Use developer options."),
            "Use developer options")

    # ── style-normalization ──
    def test_digit_letter_confusion_identifier(self):
        self.assertEqual(
            normalize_ocr_token("SCROLL_O2 DUPLICATE TITLES"),
            "SCROLL_02 DUPLICATE TITLES")

    def test_connector_whitespace_variants_collapse(self):
        for variant in ("NAV_03 - Page B", "NAV_03- Page B", "NAV_03  Page B"):
            self.assertEqual(
                normalize_ocr_token(variant),
                "NAV_03 - Page B",
                f"variant {variant!r} did not collapse")

    # ── unknown-cases-fail-closed ──
    def test_unsupported_residual_preserved_no_invention(self):
        # 'Delayed popupe' (hallucinated trailing e) is NOT guessed away.
        self.assertEqual(normalize_ocr_token("Delayed popupe"), "Delayed popupe")

    def test_time_string_preserved(self):
        self.assertEqual(normalize_ocr_token("2:48"), "2:48")

    def test_plain_word_unchanged(self):
        self.assertEqual(normalize_ocr_token("Disabled"), "Disabled")


if __name__ == "__main__":
    unittest.main()