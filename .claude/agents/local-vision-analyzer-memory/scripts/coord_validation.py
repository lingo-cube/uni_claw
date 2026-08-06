#!/usr/bin/env python3
"""coord_validation.py — click 坐标 vs analysis frame 验证 (local-vision-analyzer)

用法: python3 coord_validation.py <run_dir>

对每个 safety.click:
  1. 定位 click 前最后一帧 (pre-click 页面) 中被点击的 item (归一化文本匹配)
  2. 报告该 item 的坐标 (x,y) 与类型
  3. 定位 click 后第一帧 (post-click 页面) — 验证是否真的导航到对应页面
"""
import json
import sys
from pathlib import Path

def norm(t):
    if not t: return ""
    out=[]; pend=False
    for ch in t:
        if ch in '，、,': ch=' '
        if ch.isspace(): pend=True; continue
        if pend: out.append(' '); pend=False
        out.append(ch)
    return ''.join(out).strip().lower()

def main():
    run = Path(sys.argv[1])
    asset_dir = run / 'assets'
    ad = next(asset_dir.iterdir())
    frames = []
    for line in (ad / 'analysis.jsonl').open():
        frames.append(json.loads(line))
    trace_dir = run / 'trace'
    td = next(trace_dir.iterdir())
    clicks = []
    for line in (td / 'trace.jsonl').open():
        r = json.loads(line)
        if r.get('record_type')=='execution' and r.get('action')=='safety.click':
            clicks.append(r)
    clicks.sort(key=lambda r: r['timestamp'])
    print(f"frames={len(frames)} clicks={len(clicks)}")
    for c in clicks:
        ts = c['timestamp']
        step = c['context']['stepNumber']
        target = c.get('targetValue','')
        # pre-click frame: last frame with analyzedAt <= ts
        pre = None
        for i, f in enumerate(frames):
            if f['analyzedAt'] <= ts:
                pre = (i, f)
            else:
                break
        # post-click frame: first frame with analyzedAt > ts
        post = None
        for i, f in enumerate(frames):
            if f['analyzedAt'] > ts:
                post = (i, f)
                break
        hit = None
        if pre:
            t = norm(target)
            for it in pre[1].get('items', []):
                if norm(it.get('name','')) == t:
                    hit = it; break
        postnames = [i.get('name','') for i in (post[1].get('items',[]) if post else [])][:6]
        post_has_target = any(norm(pn)==norm(target) for pn in postnames) if post else False
        if hit:
            print(f"step {step}: click '{target}' -> item name={hit.get('name')!r} type={hit.get('type')} x={hit.get('x'):.3f} y={hit.get('y'):.4f} (frame {pre[0]+1}) | post frame {post[0]+1} names={postnames} same_page={post_has_target}")
        else:
            print(f"step {step}: click '{target}' -> NO ITEM MATCH in pre frame {pre[0]+1 if pre else '?'} | post frame {post[0]+1 if post else '?'} names={postnames}")

if __name__ == '__main__':
    main()
