@echo off
echo Building Studio Brightness Control...

REM Clean previous builds
dotnet clean

REM Build for different platforms
echo Building for win-x64...
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true -o "publish/win-x64"

echo Building for win-x86...
dotnet publish -c Release -r win-x86 -p:PublishSingleFile=true --self-contained true -o "publish/win-x86"

echo.
echo Build completed!
echo Files are in the 'publish' folder.
echo.
pause