#!/bin/sh
cd "$(dirname "$0")" || exit 1

if ! command -v node >/dev/null 2>&1; then
  echo ""
  echo "还没有安装 Node.js，暂时无法启动。"
  echo "请先安装 Node.js 18 或更高版本：https://nodejs.org/"
  echo ""
  exit 1
fi

exec node scripts/setup-and-run.cjs
