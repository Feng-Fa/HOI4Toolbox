@echo off
setlocal enabledelayedexpansion

:: 参数处理（使用%~1移除外部引号）
set "URL=%~1"
set "OUTPUT_DIR=%~2"
set "ARIA_PATH=%~3"
set "APP_DIR=%~4"

:: 验证参数
if "%URL%"=="" (
    echo [ERROR] 缺少URL参数
    exit /b 1
)

if "%OUTPUT_DIR%"=="" (
    echo [ERROR] 缺少输出目录参数
    exit /b 1
)

if "%ARIA_PATH%"=="" (
    echo [ERROR] 缺少aria2路径参数
    exit /b 1
)

if "%APP_DIR%"=="" (
    echo [ERROR] 缺少应用目录参数
    exit /b 1
)

:: 检查文件是否存在
if not exist "%ARIA_PATH%" (
    echo [ERROR] aria2不存在: %ARIA_PATH%
    exit /b 1
)

:: 创建控制台窗口
mode con: cols=100 lines=30
title HOI4游戏下载

:: 开始下载
echo [INFO] 开始下载游戏文件...
echo [DEBUG] URL: %URL%
echo [DEBUG] 输出目录: %OUTPUT_DIR%

"%ARIA_PATH%" ^
  "%URL%" ^
  --dir="%OUTPUT_DIR%" ^
  --split=16 ^
  --max-connection-per-server=16 ^
  --continue=true ^
  --max-tries=5 ^
  --retry-wait=10 ^
  --auto-file-renaming=false ^
  --allow-overwrite=true

if %errorlevel% neq 0 (
    echo [ERROR] 下载失败，错误代码: %errorlevel%
    pause
    exit /b %errorlevel%
)

:: 创建完成标记
echo > "%APP_DIR%\win.txt" Download completed at %date% %time%

echo [INFO] 下载完成!
pause
exit /b 0