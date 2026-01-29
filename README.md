# RimTalk TTS - 语音合成模组

这是RimTalk的语音合成扩展，作为独立模组运行。

RimTalk TTS是一个**Rimtalk的子模组**，为Rimtalk添加了语音生成功能

## 安装要求

### 前置模组 (Required)
```
1. **Harmony** - brrainz.harmony
2. **RimTalk** - cj.rimtalk
```

### Python环境
```
Fish Audio与EdgeTTS需要: Python 3.9+
Fish Audio需要: fish-audio-sdk
EdgeTTS需要: edge-tts
```

### 加载顺序
```
Harmony
  ↓
RimTalk
  ↓
RimTalk TTS  ← 此模组
```

在游戏内选项中配置：
- 支持Fish Audio, CozyVoice(硅基流动), IndexTTS(硅基流动), EdgeTTS(免费)作为TTS供应商
- Fish Audio API密钥(https://fish.audio/)或硅基流动API密钥(https://cloud.siliconflow.cn/)
- 语音模型: 
  Fish Audio: 可在fish audio网站上点击模型的"分享"按钮, 然后删除剪贴板网址中的"https://fish.audio/m/",剩下部分即为模型ID.
  硅基流动: 参见游戏内模组配置界面，有八个预设模型，也可添加自定义模型（需实名认证）。
  EdgeTTS: 仅有预设模型
- 处理输入文本的LLM API(暂时只适配了Deepseek与ChatGPT, 如用其他模型可尝试在自定义选项内输入模型url,模型名称与APIkey)
- TTS参数(temperature, top_p等，仅对Fish Audio有效)

github:https://github.com/whatismyname0/RimTalkTTSModule

贴吧发布页及安装教程:https://tieba.baidu.com/p/10278651916

B站演示视频:https://www.bilibili.com/video/BV1PB2fBsEtQ

作者:
Nitori_Tachyon
代码参考:
三哒锅酱(EdgeTTS C# 客户端代码)
Claude Sonnet 4.5
Gemini 3 Pro
GPT-5.1-Codex
GPT-5 Mini
建议与咨询:
Deepseek v3.2