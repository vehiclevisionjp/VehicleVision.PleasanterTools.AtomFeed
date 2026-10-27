#!/usr/bin/env bash
# 編集元の規約ブロックを AGENTS.md へ複写する。
set -euo pipefail
cd "$(dirname "$0")/.."

build() {
    cat <<'HEADER'
# エージェント向けリポジトリ指示

このファイルは `AGENTS.md` 標準（<https://agents.md/>）を読むコーディングエージェント向けの指示です。
GitHub 上のエージェントは `.github/copilot-instructions.md`、Claude Code は `CLAUDE.md` を読みます。

**規約の全文は [`.github/copilot-instructions.md`](.github/copilot-instructions.md) にあります。
以下は同ファイルの「必ず守る規約」の写しです。作業前に参照元を必ず開いてください。**

<!-- この節は .github/copilot-instructions.md から生成される。ここを直接編集しないこと。
     編集後は bash scripts/sync-agents.sh を実行すること。 -->
HEADER
    awk '
        /^<!-- AGENT-RULES:BEGIN/ { header = 1; next }
        header && /-->/ { header = 0; body = 1; next }
        header { next }
        /^<!-- AGENT-RULES:END/ { body = 0; next }
        body { print }
    ' .github/copilot-instructions.md |
        sed -E \
            -e 's#\]\(([^)/:#][^):]*)\)#](.github/\1)#g' \
            -e 's#\]\(\.github/\.\./#](#g' \
            -e '${/^$/d;}'
}

case "${1:-}" in
    "")
        build > AGENTS.md
        echo 'Generated AGENTS.md.'
        ;;
    --check)
        if ! diff -u AGENTS.md <(build); then
            echo 'AGENTS.md is out of sync. Run bash scripts/sync-agents.sh.' >&2
            exit 1
        fi
        echo 'Agent rules check passed.'
        ;;
    *)
        echo 'Usage: sync-agents.sh [--check]' >&2
        exit 1
        ;;
esac
