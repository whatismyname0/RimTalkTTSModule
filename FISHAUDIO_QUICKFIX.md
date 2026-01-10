### 📋 常见错误速查表

| 错误信息 | 原因 | 解决方案 |
|---------|------|---------|
| `ModuleNotFoundError: No module named 'fishaudio'` | 未安装 fish-audio-sdk | `pip install fish-audio-sdk` |
| `Server failed to start within 15 seconds` | Python 启动慢或依赖缺失 | 运行 `Source/Service/FishAudioService/check_dependencies.py` |
| `Python process exited during startup` | Python 环境问题 | 查看游戏日志获取详细错误 |
| `Invalid API key` | API 密钥错误 | 检查 Mod 设置中的 API 密钥 |
| `Reference voice ID not found` | 声音 ID 不存在 | 在 Fish Audio 官网验证声音 ID |
| `Connection error` | 网络问题 | 检查防火墙/代理设置 |

## 请查看游戏日志文件：

- 位置：`C:\Users\<用户名>\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`
- 搜索关键词：`FishAudio TTS`

查找类似以下的错误信息：
- `ModuleNotFoundError`: 缺少 Python 包
- `ImportError`: Python 导入错误
- `Python process exited during startup`: Python 进程启动时崩溃
- `Server failed to start within 15 seconds`: 启动超时

将错误信息反馈给开发者以获得进一步帮助。

## 网络连接

确保您的网络可以访问：
- https://api.fish.audio

可以在浏览器中测试此 URL 是否可访问。如果无法访问，请检查：
- 防火墙设置
- 代理设置
- 网络连接

## 技术细节

Fish Audio TTS 使用本地 HTTP 服务器（127.0.0.1:5678）来处理 TTS 请求：
1. C# 代码启动 Python 服务器进程
2. Python 服务器监听本地端口 5678
3. C# 通过 HTTP 发送 TTS 请求到 Python 服务器
4. Python 服务器调用 Fish Audio SDK 生成语音
5. 返回音频数据给 C# 代码

如果启动失败，通常是因为：
- Python 环境配置问题
- 缺少必需的 Python 包
- 系统资源不足
- 端口 5678 被占用
