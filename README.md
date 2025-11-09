# Studio Brightness Control

> 🌐 [English](./README.en.md)

Windows 实用工具，用于控制 Apple Studio Display 的亮度。

## 功能特性

- 🎛️ 15 级亮度调节
- ⌨️ 全局热键支持 (LShift + LWin + 左右方向键)
- 🖱️ 系统托盘操作
- 🎚️ 滑块实时调节
- 🔄 实时预览与保存

## 系统要求

- Windows 10 11
- .NET 6.0 Runtime (如果使用框架依赖版本)
- Apple Studio Display

## 下载

前往 [Releases](https://github.com/dnhy/studio-brightness-control/releases) 页面下载最新版本。

## 使用方法

1. 下载并运行 `BrightnessAppInstaller.exe`进行安装
2. 运行`StudioBrightnessApp.exe`,程序会在系统托盘中运行
3. 使用方法：
   - 双击托盘图标 打开亮度设置
   - 右键托盘图标 显示菜单
   - 热键 `LShift + LWin + ←→` 调节亮度

## 构建

```bash
# 克隆仓库
git clone git@github.com:dnhy/studio-brightness-control.git

# 发布单个可执行文件
dotnet publish -c Release -r win-x64 -pPublishSingleFile=true --self-contained true
```

## 许可证
本项目采用 MIT 许可证 - 查看 [LICENSE](https://github.com/dnhy/studio-brightness-control/blob/main/LICENSE) 文件了解详情。
