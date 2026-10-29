@echo off
chcp 65001 >nul
cd /d "%~dp0"

where node >nul 2>&1
if errorlevel 1 (
  echo.
  echo 还没有安装 Node.js，暂时无法启动。
  echo 请先安装 Node.js 18 或更高版本：https://nodejs.org/
  echo 安装时勾选 "Add to PATH"，装完后重新双击本文件。
  echo.
  pause
  exit /b 1
)

node scripts\setup-and-run.cjs
echo.
pause
