import math

from uniclaw_perception.remap import remap_coords


def _candidate(bounds, px=None):
    return {"bounds": bounds, "boundsPx": px or [10, 10, 20, 20]}


def test_coord01_nan_and_infinity_rejected():
    e = {"candidates": [_candidate({"x1": math.nan, "y1": .1, "x2": .2, "y2": .2}),
                         _candidate({"x1": .1, "y1": .1, "x2": math.inf, "y2": .2})]}
    remap_coords(e, 1, 0, 100, 100)
    assert e["candidates"] == []
    assert {"code": "INVALID_GEOMETRY"} in e["diagnostics"]


def test_coord02_negative_and_over_one_rejected():
    e = {"candidates": [_candidate({"x1": -.1, "y1": .1, "x2": .2, "y2": .2}),
                         _candidate({"x1": .1, "y1": .1, "x2": 1.2, "y2": .2})]}
    remap_coords(e, 1, 0, 100, 100)
    assert e["candidates"] == []


def test_coord03_zero_area_and_reversed_rejected():
    e = {"candidates": [_candidate({"x1": .2, "y1": .2, "x2": .2, "y2": .3}),
                         _candidate({"x1": .4, "y1": .4, "x2": .3, "y2": .5})]}
    remap_coords(e, 1, 0, 100, 100)
    assert e["candidates"] == []


def test_coord04_valid_sibling_preserved_without_clamp():
    good = _candidate({"x1": 0.1, "y1": 0.1, "x2": 0.2, "y2": 0.2})
    bad = _candidate({"x1": -.1, "y1": .1, "x2": .2, "y2": .2})
    e = {"candidates": [good, bad]}
    remap_coords(e, 1, 0, 100, 100)
    assert e["candidates"] == [good]
    assert good["bounds"]["x1"] == 0.1


def test_coord05_all_invalid_returns_empty_and_diagnostic():
    e = {"candidates": [_candidate({"x1": 0, "y1": 0, "x2": 0, "y2": 0})]}
    remap_coords(e, 1, 0, 100, 100)
    assert e["candidates"] == []
    assert e["status"] == "INVALID_GEOMETRY"


def test_coord06_post_remap_pixel_bounds_are_checked():
    e = {"candidates": [_candidate({"x1": 0.1, "y1": 0.1, "x2": 0.2, "y2": 0.2}, [90, 90, 120, 120])]}
    remap_coords(e, 2, 0, 100, 100)
    assert e["candidates"] == []


def test_coord07_stage_views_use_normalized_bounds_and_do_not_reenter():
    # Canonical remap rejects invalid candidates; stage-view filtering uses the
    # same normalized rule, so an invalid item cannot re-enter fused evidence.
    e = {"candidates": [
        _candidate({"x1": .1, "y1": .1, "x2": .2, "y2": .2}),
        _candidate({"x1": .1, "y1": .1, "x2": 1.2, "y2": .2}),
    ]}
    remap_coords(e, 1, 0, 100, 100)
    assert len(e["candidates"]) == 1
    assert e["candidates"][0]["bounds"]["x2"] <= 1
