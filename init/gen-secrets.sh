#!/usr/bin/env bash
#
# gen-secrets.sh — 从当前机器提取真实配置值, 生成"自应用"配置脚本
#
# 用法（在旧机器上执行）:
#   bash init/gen-secrets.sh
#
# 产物: ~/dsh-secrets-apply.py
#   · 含真实密钥 + 内嵌全部配置模板（自包含, 单文件）
#   · chmod 700, 仅本机可读
#   · 用安全通道同步到新机器（建议 scp/加密压缩/加密U盘, 勿走明文聊天工具）
#   · 新机器上执行一次: 自动创建缺失配置文件并填充真实值; 已有文件不覆盖, 仅替换残留占位符
#
# 安全约定:
#   · 本脚本本身不含任何密钥, 可提交仓库
#   · 产物含密钥, 永不提交、用完即删（新机器执行成功后 rm ~/dsh-secrets-apply.py）
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEMPLATE_DIR="$SCRIPT_DIR/templates"
OUT="$HOME/dsh-secrets-apply.py"

# ---- 1. 提取变量（值仅经环境变量传递, 不经 shell 解析, 防特殊字符）----

# ~/.zshrc 的 export VAR="value"
zshrc_val() { # zshrc_val VAR
  grep -E "^export $1=" "$HOME/.zshrc" 2>/dev/null | head -1 \
    | sed -E "s/^export $1=\"?([^\"]*)\"?$/\1/" || true
}
# ~/.claude/settings.json 的 env.VAR
claude_val() { # claude_val VAR
  python3 - "$1" <<'PY'
import json, os, sys
try:
    d = json.load(open(os.path.expanduser("~/.claude/settings.json")))
    print(d.get("env", {}).get(sys.argv[1], ""))
except Exception:
    pass
PY
}
# ~/.codex/config.toml 指定 provider 段的 experimental_bearer_token
codex_token() { # codex_token PROVIDER_SECTION
  awk -v sec="$1" '
    $0 == "[" sec "]" { f=1; next }
    f && /^\[/ { f=0 }
    f && /experimental_bearer_token[[:space:]]*=/ {
      sub(/^.*=[[:space:]]*"?/, ""); sub(/"?[[:space:]]*$/, ""); print; exit
    }' "$HOME/.codex/config.toml" 2>/dev/null || true
}
# ~/.litellm/secrets.json 的 KEY
litellm_val() { # litellm_val KEY
  python3 - "$1" <<'PY'
import json, os, sys
try:
    d = json.load(open(os.path.expanduser("~/.litellm/secrets.json")))
    print(d.get(sys.argv[1], ""))
except Exception:
    pass
PY
}
# ~/.gitconfig 的 section.key
git_val() { # git_val SECTION.KEY
  git config --file "$HOME/.gitconfig" --get "$1" 2>/dev/null || true
}

DEEPSEEK_API_KEY="$(zshrc_val DEEPSEEK_API_KEY)"
[ -z "$DEEPSEEK_API_KEY" ] && DEEPSEEK_API_KEY="$(litellm_val DEEPSEEK_API_KEY)"
ANTHROPIC_AUTH_TOKEN="$(claude_val ANTHROPIC_AUTH_TOKEN)"
[ -z "$ANTHROPIC_AUTH_TOKEN" ] && ANTHROPIC_AUTH_TOKEN="$(litellm_val ANTHROPIC_AUTH_TOKEN)"
MIMO_API_KEY="$(claude_val MIMO_API_KEY)"
QWEN_API_KEY="$(litellm_val QWEN_API_KEY)"
SENSENOVA_TOKEN="$(codex_token model_providers.sensenova)"
GIT_NAME="$(git_val user.name)"
GIT_EMAIL="$(git_val user.email)"
GIT_PROXY="$(git_val http.proxy)"

# ---- 2. 汇总缺失项（bash 3.2 兼容: 不用关联数组, 用并行数组）----
VARS="DEEPSEEK_API_KEY ANTHROPIC_AUTH_TOKEN MIMO_API_KEY QWEN_API_KEY SENSENOVA_TOKEN GIT_NAME GIT_EMAIL GIT_PROXY"
echo "== 提取结果（只显示是否提取到, 不显示值）=="
for k in $VARS; do
  v="$(eval "printf '%s' \"\${$k}\"")"
  if [ -n "$v" ]; then
    printf '  [ ✓ ] %-22s (len=%s)\n' "$k" "${#v}"
  else
    printf '  [ ✗ ] %-22s (未提取到!)\n' "$k"
  fi
done
[ -n "$DEEPSEEK_API_KEY" ] || echo "  提示: DEEPSEEK_API_KEY 未提取到, 产物中该值为空, 需手动补"

# ---- 3. 生成自包含脚本 ----
export DEEPSEEK_API_KEY ANTHROPIC_AUTH_TOKEN MIMO_API_KEY QWEN_API_KEY SENSENOVA_TOKEN GIT_NAME GIT_EMAIL GIT_PROXY
python3 - "$OUT" "$TEMPLATE_DIR" <<'PY'
import json, os, pathlib, sys

out, tdir = sys.argv[1], pathlib.Path(sys.argv[2])

vars_ = {"__" + k + "__": os.environ.get(k, "") for k in [
    "DEEPSEEK_API_KEY", "ANTHROPIC_AUTH_TOKEN", "MIMO_API_KEY",
    "QWEN_API_KEY", "SENSENOVA_TOKEN", "GIT_NAME", "GIT_EMAIL", "GIT_PROXY",
]}

# 模板文件 → 目标文件相对路径
templates = [
    ("zshrc.template",                 "~/.zshrc"),
    ("gitconfig.template",             "~/.gitconfig"),
    ("claude-settings.json.template",  "~/.claude/settings.json"),
    ("codex-config.toml.template",     "~/.codex/config.toml"),
    ("dsh-settings.yaml.template",     "~/.dsh/settings.yaml"),
]
template_data = {src: (tdir / src).read_text() for src, _ in templates}

apply = r'''#!/usr/bin/env python3
# dsh-secrets-apply.py — 自应用配置脚本（由 gen-secrets.sh 在旧机器生成, 含真实密钥）
# 用法: python3 ~/dsh-secrets-apply.py
# 行为: 目标文件缺失 → 用内嵌模板创建并填充真实值
#       目标文件已存在但含 __XXX__ 占位符 → 仅替换占位符, 不覆盖其他内容
#       目标文件已存在且无占位符 → 跳过（不动）
# 安全: 执行成功后请删除本文件（含密钥）
import json, os, pathlib, sys

V = json.loads(__VARS_JSON__)
TEMPLATES = json.loads(__TEMPLATES_JSON__)
TARGETS = json.loads(__TARGETS_JSON__)

def expand(p):
    return pathlib.Path(os.path.expanduser(p))

def fill(text, v):
    for key, val in v.items():
        text = text.replace(key, val)
    return text

report = []
for src, dst in zip(TEMPLATES.keys(), TARGETS):
    target = expand(dst)
    if not target.exists():
        target.parent.mkdir(parents=True, exist_ok=True)
        text = fill(TEMPLATES[src], V)
        # GIT_PROXY 为空 → 删除 proxy 行（模板约定: __GIT_PROXY__ 为空则整段删除）
        if not V.get("__GIT_PROXY__"):
            text = "\n".join(l for l in text.splitlines() if "__GIT_PROXY__" not in l)
        target.write_text(text)
        report.append(f"创建+填充: {dst}")
    else:
        text = target.read_text()
        if any(p in text for p in V):
            text = fill(text, V)
            if not V.get("__GIT_PROXY__"):
                text = "\n".join(l for l in text.splitlines() if "__GIT_PROXY__" not in l)
            target.write_text(text)
            report.append(f"替换占位符: {dst}")
        else:
            report.append(f"跳过(无占位符): {dst}")

# QWEN_API_KEY 通过环境变量被 codex 引用（env_key）, 必须注入 ~/.zshrc
if V.get("__QWEN_API_KEY__"):
    zshrc = expand("~/.zshrc")
    if zshrc.exists() and "QWEN_API_KEY" not in zshrc.read_text():
        with zshrc.open("a") as f:
            f.write(f'\nexport QWEN_API_KEY="{V["__QWEN_API_KEY__"]}"\n')
        report.append("追加 QWEN_API_KEY → ~/.zshrc")
    elif not zshrc.exists():
        report.append("!! ~/.zshrc 不存在, QWEN_API_KEY 未注入（先跑 quick-init 或配置模板）")

print("== dsh-secrets-apply 执行结果 ==")
for line in report:
    print("  " + line)

# 校验: 不应有残留占位符
left = [(dst, p) for src, dst in zip(TEMPLATES.keys(), TARGETS)
        for p in V if expand(dst).exists() and p in expand(dst).read_text()]
if left:
    print("!! 残留占位符（需手动处理）:")
    for dst, p in left:
        print(f"   {dst}: {p}")
    sys.exit(1)
print("OK: 全部配置文件无残留占位符")
'''

# 嵌入 JSON（双重 dumps: 外层是合法 Python 字符串字面量, 内层是 JSON 文本）
script = apply.replace("__VARS_JSON__", json.dumps(json.dumps(vars_))) \
               .replace("__TEMPLATES_JSON__", json.dumps(json.dumps(template_data))) \
               .replace("__TARGETS_JSON__", json.dumps(json.dumps([t[1] for t in templates])))

pathlib.Path(out).write_text(script)
os.chmod(out, 0o700)
print(f"\n已生成: {out} (chmod 700, 含真实密钥)")
print("同步到新机器后执行: python3 ~/dsh-secrets-apply.py")
PY
