# 网址批量打开工具（WinForms EXE）

这是一个无需 Python 运行环境的 Windows 桌面工具，已直接编译为 `URLLauncher.exe`。

## 界面与体验

- 主题为上白下浅蓝渐变
- 字体改为更平滑圆润的 `Segoe UI`
- 按钮为浅色扁平风格，文字上下居中显示

## 功能说明

- 主界面维护链接列表，显示 `序号 / 标题 / 网址`
- 支持逐条 `上移 / 下移` 调整顺序，也支持整体升序/降序
- 添加网址时弹出标题备注窗口（支持跳过）
- 输入内容中若 `http/https` 前有文字，会自动提取为标题
- 自动处理最外层一对括号（如 `（...）`、`《...》`、`【...】` 等）
- 支持按标题搜索过滤
- 支持 `一键按顺序打开`（约 0.1 秒间隔）
- 支持 `选择部分打开`，并按用户选择顺序打开
- 支持右键某条记录后 `编辑标题`
- 关闭程序时自动检测未保存改动，并弹窗选择：
  - 保存后退出
  - 不保存直接退出
  - 取消并返回继续编辑
- 有未保存改动时，窗口标题会显示 `*`

## 数据存储（JSON）

默认预设文件为 `saved_urls.json`，格式如下：

```json
{
  "version": 1,
  "links": [
    {
      "order": 1,
      "title": "示例标题",
      "url": "https://example.com"
    }
  ]
}
```

说明：
- `order`：打开顺序
- `title`：备注标题
- `url`：标准化后的网址
- 预设文件可通过“选择预设文件”加载，保存时统一写入 `.json` 格式

## 运行方式

直接双击：

- `URLLauncher.exe`

## 开发与重新编译

项目中已提供构建脚本，推荐在 `url_launcher_app` 目录下运行：

```powershell
.\build.ps1
```

该脚本会使用本机 .NET Framework 的 `csc.exe` 编译 `Program.cs`，并将 `app.ico` 嵌入到生成的 `URLLauncher.exe` 中。

如果你希望保存源码后自动重新编译，可启动监视脚本：

```powershell
.\watch.ps1
```

`watch.ps1` 会监视当前目录下的 `*.cs` 和 `app.ico` 文件变化，并在变更后自动触发构建。

如果你需要直接调用编译命令，也可以使用：

```powershell
& "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" /target:winexe /platform:anycpu /optimize+ /win32icon:app.ico /r:System.Web.Extensions.dll /out:URLLauncher.exe Program.cs
```

## 更新日志

- 见 `CHANGELOG.md`
