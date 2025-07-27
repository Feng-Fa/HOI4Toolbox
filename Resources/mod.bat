@echo off
setlocal enabledelayedexpansion

:: ��ȡ�ű�����Ŀ¼
set "scriptDir=%~dp0"
set "scriptDir=%scriptDir:~0,-1%"

:: ����Ŀ��Ŀ¼
set "targetDir=%USERPROFILE%\Documents\Paradox Interactive\Hearts of Iron IV\mod"

:: ����Ŀ��Ŀ¼����������ڣ�
if not exist "%targetDir%" (
    mkdir "%targetDir%"
    echo Created target directory: %targetDir%
)

:: �����ű�Ŀ¼�µ����ļ���
for /D %%d in ("%scriptDir%\*") do (
    :: ��ȡ�ļ������ƣ������������ļ���
    for %%a in ("%%d\.") do set "folderName=%%~nxa"
    
    :: �����ļ����в���.mod�ļ�
    for %%f in ("%%d\*.mod") do (
        set "filePath=%%~dpf"
        set "originalName=%%~nxf"
        
        :: ����·������ - �滻��б��Ϊ��б�ܲ��Ƴ�β����б��
        set "path1=!filePath:\=/!"
        set "path1=!path1:~0,-1!"
        
        :: �������ļ�����ʹ���ļ������ƣ�
        set "newName=!folderName!.mod"
        
        :: �����ļ���Ŀ��Ŀ¼��������
        copy /Y "%%f" "%targetDir%\!newName!" >nul
        
        :: ���ļ�ĩβ���·������
        echo.>> "%targetDir%\!newName!"
        echo path="!path1!">> "%targetDir%\!newName!"
        
        echo Processed: "!originalName!" renamed to "!newName!" - Added path: !path1!
    )
)

echo.
echo Operation completed! All .mod files processed and renamed to: %targetDir%
endlocal