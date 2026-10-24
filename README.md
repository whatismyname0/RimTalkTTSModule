# RimTalk TTS - 语音合成模组

当前版本：**2.0**

RimTalk 的语音合成扩展，作为独立模组运行：捕获 RimTalk 生成的对话并转成语音播放。

## 安装要求

### 前置模组（必需）

- **Harmony** - `brrainz.harmony`
- **RimTalk** - `cj.rimtalk`

### 加载顺序

```
Harmony → RimTalk → RimTalk TTS（本模组）
```

### Python 环境

- Fish Audio 与 EdgeTTS 需要 **Python 3.9+**
- Fish Audio 需要 `fish-audio-sdk`
- EdgeTTS 需要 `edge-tts`

## 支持的供应商

- Fish Audio（本地 Python helper，安全模式：随机端口 + 一次性 token）
- CosyVoice2 / IndexTTS2（硅基流动）
- Azure TTS、Gemini TTS、EdgeTTS（免费）
- 自定义 OpenAI 兼容 TTS 供应商（可在设置里增删改）

TTS 前的文本处理/翻译使用 OpenAI 兼容的 LLM API（DeepSeek / OpenAI / 自定义 / 跟随 RimTalk 配置）。

## 行为说明

- **首句优先**：一段对话的第一句会跳过供应商生成冷却队列，让对话尽快开始出声；后续句子按冷却排队。
- **显示与播放同步**：音频未就绪时对话行会保持在队首、每 0.5 秒重试，音频准备好后才显示并播放；不打断正在播放的上一段。
- **暂停时也生成**：在暂停界面生成对话时，TTS 会从生成完成时就开始准备，而不是等取消暂停（取消暂停后正常显示并播放）。
- **单句超时**：默认 120 秒，范围 0–600 秒，0 = 关闭。计时从请求发给供应商开始，不含文本预处理与冷却；超时的句子会被丢弃并以 Warning 记录，对话照常显示。
- **忽略同步**：RimTalk 忽略某句时取消尚未开始的语音生成；已经开始的播放不会被掐断。
- **手动控制**：Overlay 提供“清空 / 生成一轮 / 忽略全部 / 显示下一条”四个按钮；“显示下一条”会临时绕过 RimTalk 的回复间隔。
- **开关**：设置页的 Enable TTS 与游戏内按钮都可随时开关；关闭时清理待办语音与播放状态。

## Fish Audio 本地服务

Fish Audio 由一个本地 Python helper 提供，默认安全模式：监听 `127.0.0.1` 随机端口，通过 stdin 接收一次性 token，之后所有请求都要带 `X-TTS-Token`；请求体限制 1 MB、只接受 `application/json`、并发上限 4。

- 退出清理：C# 保持 stdin 管道打开；游戏进程结束（包括 Alt+F4、崩溃、任务管理器）时 Python 通过 EOF 立即退出；父 PID 监控与创建时间校验作为兜底。
- 更多 Fish Audio 排障见 [FISHAUDIO_QUICKFIX.md](FISHAUDIO_QUICKFIX.md)。

注意：设置文件与日志会包含明文 API 密钥，请勿分享。

## 链接

- GitHub: https://github.com/whatismyname0/RimTalkTTSModule
- 贴吧发布页及安装教程: https://tieba.baidu.com/p/10278651916
- B 站演示视频: https://www.bilibili.com/video/BV1PB2fBsEtQ

## 作者

Nitori_Tachyon

代码参考：三哒锅酱（EdgeTTS C# 客户端代码）

使用的 AI：Claude Sonnet 4.5、Gemini 3 Pro、GPT-5.1-Codex、GPT-5 Mini、Deepseek-v4-flash-0731、Deepseek-v4.1-flash

建议与咨询：Deepseek v3.2
