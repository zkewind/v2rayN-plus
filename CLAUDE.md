# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

v2rayN is a cross-platform GUI client for Windows, Linux and macOS that supports [Xray](https://github.com/XTLS/Xray-core), [sing-box](https://github.com/SagerNet/sing-box) and other proxy cores.

## Build Commands

### Prerequisites
- .NET 8.0 SDK
- Windows 10 SDK (for Windows builds)

### Build Windows (WPF version)
```bash
cd v2rayN
dotnet publish ./v2rayN/v2rayN.csproj -c Release -r win-x64 -p:SelfContained=true -p:EnableWindowsTargeting=true -o ./Release/win-x64
```

### Build Windows Desktop (Avalonia cross-platform version)
```bash
cd v2rayN
dotnet publish ./v2rayN.Desktop/v2rayN.Desktop.csproj -c Release -r win-x64 -p:SelfContained=true -o ./Release/win-desktop
```

### Build Linux
```bash
cd v2rayN
dotnet publish ./v2rayN.Desktop/v2rayN.Desktop.csproj -c Release -r linux-x64 -p:SelfContained=true -o ./Release/linux-x64
```

### Build macOS
```bash
cd v2rayN
dotnet publish ./v2rayN.Desktop/v2rayN.Desktop.csproj -c Release -r osx-x64 -p:SelfContained=true -o ./Release/osx-x64
```

### Build AmazTool
```bash
cd v2rayN
dotnet publish ./AmazTool/AmazTool.csproj -c Release -r win-x64 -p:SelfContained=true -p:PublishTrimmed=true -o ./Release
```

## Project Architecture

### Solution Structure (`v2rayN/v2rayN.sln`)

| Project | Type | Description |
|---------|------|-------------|
| `v2rayN/v2rayN` | WPF App | Windows-only WPF UI with MaterialDesign |
| `v2rayN/v2rayN.Desktop` | Avalonia App | Cross-platform Avalonia UI |
| `v2rayN/ServiceLib` | Library | Core business logic (shared) |
| `v2rayN/AmazTool` | Console App | Upgrade tool |
| `v2rayN/GlobalHotKeys` | Library | Global hotkey support |

### Key Directories

- `v2rayN/ServiceLib/Handler/` - Core handlers (connection, subscription, sysproxy, format)
- `v2rayN/ServiceLib/Manager/` - Managers (core info, profile, statistics, Clash API, PAC)
- `v2rayN/ServiceLib/Models/` - Data models
- `v2rayN/ServiceLib/ViewModels/` - ReactiveUI ViewModels
- `v2rayN/ServiceLib/Services/` - Core services (download, process, statistics)
- `v2rayN/ServiceLib/Sample/` - Embedded sample configs (routing rules, DNS, PAC)
- `v2rayN/ServiceLib/Resx/` - Localization resources (zh-Hans, zh-Hant, fa-Ir, fr, hu, ru)

### Technology Stack

- **Framework**: .NET 8.0
- **UI (Windows)**: WPF with MaterialDesign
- **UI (Cross-platform)**: Avalonia with Semi.Avalonia
- **MVVM**: ReactiveUI with ReactiveUI.Fody
- **Database**: sqlite-net-pcl
- **Logging**: NLog
- **Config Formats**: YamlDotNet, JSON
- **QR Code**: QRCoder, ZXing.Net

### Configuration Files

- `v2rayN/ServiceLib/Sample/custom_routing_white` - 白名单规则 (bypass mainland)
- `v2rayN/ServiceLib/Sample/custom_routing_black` - 黑名单规则 (GFW list)
- `v2rayN/ServiceLib/Sample/custom_routing_global` - 全局代理规则
- `v2rayN/ServiceLib/Sample/pac` - PAC 文件样本
- `v2rayN/ServiceLib/Sample/*` - Various core configuration templates

### Rule System

See `docs/mode-rules-guide.md` for details on:
- TUN mode vs PAC/系统代理
- 规则选项 (V4-绕过大陆, V4-黑名单, V4-全局)
- 组合建议

## Development Notes

- Both WPF and Avalonia projects depend on `ServiceLib` - most business logic lives there
- ReactiveUI is used for MVVM pattern with source-generated code via Fody
- Global hotkeys are handled by the `GlobalHotKeys` library
- The project uses embedded resources for sample configuration files
- Localization is handled via .resx files in ServiceLib/Resx
