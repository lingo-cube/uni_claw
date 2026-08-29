# Display V1 Occurrence Provenance (frozen 2026-08-29)

## Key Unknown Occurrences (blocking completeness)

| Occ | Text | Type | Y-Range | X-Range | Provenance |
|-----|------|------|---------|---------|------------|
| occ_23 | 'Appearance' | text_block | [0.703,0.721] | [0.061,0.307] | OCR-to-box misattribution: 'Appearance' section header text assigned to 'Dark theme' row box |
| occ_24 | 'Dark theme' | menu_item | [0.703,0.721] | [0.061,0.307] | Correct: Dark theme row |
| occ_31 | 'Color' | text_block | [0.861,0.872] | [0.060,0.140] | Correct: Color section header |
| occ_33 | 'Color' | text_block | [0.909,0.927] | [0.061,0.200] | OCR-to-box misattribution: 'Color' text assigned to 'Colors' row box |
| occ_34 | 'Colors' | menu_item | [0.909,0.927] | [0.061,0.200] | Correct: Colors row |

## Original 'Appearance' positions (for R-VLM-1 rule)
| Occ | Text | Y-Range | Relation |
|-----|------|---------|----------|
| occ_21 | 'Appearance' | [0.653,0.669] | Original section header (correct) |
| occ_22 | 'Appearance' | [0.653,0.669] | menu_item duplicate of occ_21 (same position, normal) |
| occ_23 | 'Appearance' | [0.703,0.721] | MISATTRIBUTED to Dark theme row position |

## Rule Evidence
- occ_23 and occ_24: same bounds, DIFFERENT text → misattribution pattern
- occ_21 and occ_23: same text, DIFFERENT bounds → original + misattributed copy
- occ_33 and occ_34: same bounds, DIFFERENT text ('Color' vs 'Colors') → misattribution
- occ_31 and occ_33: same text, DIFFERENT bounds → original + misattributed copy
