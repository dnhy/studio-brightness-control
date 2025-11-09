# Studio Brightness Control

> 🌐 [简体中文](./README.md)

A Windows utility for controlling brightness of Apple Studio Display.

## Features

- 🎛️ 15-level brightness adjustment
- ⌨️ Global hotkey support (LShift + LWin + ←→)
- 🖱️ System tray integration
- 🎚️ Real-time slider control
- 🔄 Live preview and persistent saving

## System Requirements

- Windows 10 / 11
- .NET 6.0 Runtime (if using framework-dependent build)
- Apple Studio Display

## Download

Visit the [Releases](https://github.com/dnhy/studio-brightness-control/releases) page to download the latest version.

## Usage

1. Download and run `BrightnessAppInstaller.exe` to install  
2. Launch `StudioBrightnessApp.exe`, it will run in the system tray  
3. Controls:
   - Double-click tray icon to open brightness settings
   - Right-click tray icon to open menu
   - Use hotkeys `LShift + LWin + ←→` to adjust brightness

## Build

```bash
git clone git@github.com:dnhy/studio-brightness-control.git
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true
```

## License
This project is licensed under the MIT License – see the LICENSE file for details.