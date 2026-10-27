# 喵喵小工具

> **下载**：[Windows 便携版与源码包](https://github.com/HappyAny/meow-tools/releases/latest) · **许可**：GPL-3.0-only；第三方组件保留各自许可。

原生 Windows 工具启动器。打开 EXE 后自动读取它旁边的 `tools` 文件夹，显示工具名称、版本、图标，并提供启动按钮。

```text
喵喵小工具/
  喵喵小工具.exe
  tools/
    PILL.exe
    PCM-A10.exe
    Edifier.exe
    其他工具/
      其他工具.exe
```

支持 EXE 和 Windows 快捷方式（.lnk），包括普通子文件夹。带多个文件的工具应把完整目录一起放入 `tools`。程序列出其中所有 EXE / 快捷方式，不会猜测哪个辅助程序应该隐藏。也可只把已有工具的快捷方式放进来。

启动器启动、重新获得焦点或点击“刷新”时扫描；扫描不会启动工具。关闭窗口后没有后台扫描进程。目录链接、符号链接不递归进入，避免意外读取其他目录；单次最多检查 10000 项，达到上限会提示。Windows 快捷方式可以保留原有目标与参数。

点击“启动”、双击工具卡片，或选中卡片按 Enter。EXE 的工作目录设为该 EXE 所在目录，因此相对配置、资源文件正常解析。关闭启动器不会关闭已启动工具。界面的“已启动”只表示 Windows 接受了启动请求，不代表工具的蓝牙或硬件功能已经验证。

- 搜索：匹配工具名称或相对文件名，Ctrl+F 聚焦搜索框。
- F5：刷新列表。新增、移走或重命名工具后也可返回启动器自动刷新。
- “工具文件夹”：打开旁边的 `tools`；不存在时创建。
- 右上角菜单：浅色 / 深色 / 跟随系统主题、关于与许可证。
- 主题偏好：`%LOCALAPPDATA%\MeowTools\theme.txt`。

Windows x64 单文件 EXE，自带 .NET 10.0.14 运行时。无需安装 .NET。首次启动可能解压原生 WPF 组件到当前用户临时目录。项目采用 GPL-3.0-only，被启动工具保留自己的许可。

## 构建与检查

需要 .NET 10 SDK、Python 3、Pillow 和 pefile。

```powershell
dotnet run --project tests/MeowTools.Tests.csproj -c Release
python tools/build.py
python tools/verify.py --tools-dir "%USER_TOOL_DIRECTORY%\tools"
```

如需离线构建，可给构建脚本添加 `--offline-feed <已缓存的 NuGet 包目录>`；该缓存不包含在源码包内。

诊断参数 `--verify-native` 仅用于离线界面自检。在此模式下可以通过 `MEOW_VERIFY_TOOLS` 指定待展示目录、`MEOW_VERIFY_OUTPUT` 指定截图和报告目录；普通启动始终使用同目录的 `tools`。自检不会启动真实工具。
