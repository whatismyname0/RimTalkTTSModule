### 📋 常见错误速查表

| 错误信息 | 原因 | 解决方案 |
|---------|------|---------|
| `ModuleNotFoundError: No module named 'fishaudio'` | 未安装 fish-audio-sdk | `pip install fish-audio-sdk` |
| `Server failed to start within 15 seconds` | Python 启动慢或依赖缺失 | 运行 `Source/Service/FishAudioService/check_dependencies.py` |
| `Python process exited during startup` | Python 环境问题 | 查看游戏日志获取详细错误 |
| `Invalid API key` | API 密钥错误 | 检查 Mod 设置中的 API 密钥 |
| `Reference voice ID not found` | 声音 ID 不存在 | 在 Fish Audio 官网验证声音 ID |
| `Connection error` | 网络问题 | 检查防火墙/代理设置 |
| `Unauthorized` (401) | 本地服务 token 不匹配（常见于旧的 Python 服务未退出） | 重启游戏；仍失败则退出游戏后确认没有残留的 python 进程 |
| `Request body too large` (413) / `Content-Type must be application/json` (415) | 本机其他程序尝试调用本地服务 | 无需处理；模组自身的请求不会触发 |
| `Too many concurrent requests` (429) | 同时进行的语音请求超过 4 个 | 等待片刻重试；反复出现请反馈 |
| `No auth token received on stdin` | 启动握手失败（Python 版本/环境异常） | 确认游戏进程正常启动；反复出现请反馈日志 |

## 请查看游戏日志文件：

- 位置：`C:\Users\<用户名>\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`
- 搜索关键词：`FishAudio TTS`

查找类似以下的错误信息：
- `ModuleNotFoundError`: 缺少 Python 包
- `ImportError`: Python 导入错误
- `Python process exited during startup`: Python 进程启动时崩溃
- `Server failed to start within 15 seconds`: 启动超时
- `Server ready on 127.0.0.1:<端口>`: 正常启动（端口是随机分配的）

将错误信息反馈给开发者以获得进一步帮助。

## 网络连接

确保您的网络可以访问：
- https://api.fish.audio

可以在浏览器中测试此 URL 是否可访问。如果无法访问，请检查：
- 防火墙设置
- 代理设置
- 网络连接

## 技术细节

Fish Audio TTS 使用本地 HTTP 服务器来处理 TTS 请求（安全模式）：
1. C# 代码生成一次性 32 字节 token，通过 **stdin** 传给 Python 进程（不经过命令行参数，避免被同机进程读取）
2. Python 监听 `127.0.0.1` 的**随机端口**，并把实际端口通过 stdout 的 ready 行告知 C#
3. C# 之后所有请求都必须带 `X-TTS-Token`，否则返回 401；请求体限制 1 MB、只接受 `application/json`、并发上限 4
4. Python 服务器调用 Fish Audio SDK 生成语音
5. 返回音频数据给 C# 代码

退出清理：C# 会保持 stdin 管道打开；游戏进程消失（包括 Alt+F4、崩溃、任务管理器结束）时 Python 通过管道 EOF 立即退出，父进程 PID 监控作为兜底。如果退出游戏后任务管理器里仍看到 `python.exe`，等 1–2 秒后应自行消失；常驻不退出请反馈日志。

如果启动失败，通常是因为：
- Python 环境配置问题
- 缺少必需的 Python 包
- 系统资源不足
- 安全模式握手失败（见上表）
