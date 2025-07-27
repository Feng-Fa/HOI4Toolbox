@echo off
setlocal enabledelayedexpansion

:: 获取脚本所在目录
set "scriptDir=%~dp0"
set "scriptDir=%scriptDir:~0,-1%"

:: 设置目标目录
set "targetDir=%USERPROFILE%\Documents\Paradox Interactive\Hearts of Iron IV\mod"

:: 创建目标目录（如果不存在）
if not exist "%targetDir%" (
    mkdir "%targetDir%"
    echo Created target directory: %targetDir%
)

:: 遍历脚本目录下的子文件夹
for /D %%d in ("%scriptDir%\*") do (
    :: 获取文件夹名称（用于重命名文件）
    for %%a in ("%%d\.") do set "folderName=%%~nxa"
    
    :: 在子文件夹中查找.mod文件
    for %%f in ("%%d\*.mod") do (
        set "filePath=%%~dpf"
        set "originalName=%%~nxf"
        
        :: 处理路径变量 - 替换反斜杠为正斜杠并移除尾部反斜杠
        set "path1=!filePath:\=/!"
        set "path1=!path1:~0,-1!"
        
        :: 设置新文件名（使用文件夹名称）
        set "newName=!folderName!.mod"
        
        :: 复制文件到目标目录并重命名
        copy /Y "%%f" "%targetDir%\!newName!" >nul
        
        :: 在文件末尾添加路径配置
        echo.>> "%targetDir%\!newName!"
        echo path="!path1!">> "%targetDir%\!newName!"
        
        echo Processed: "!originalName!" renamed to "!newName!" - Added path: !path1!
    )
)

echo.
echo Operation completed! All .mod files processed and renamed to: %targetDir%
endlocal