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
Python 3.9+
fish-audio-sdk
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
- 暂时只支持Fish Audio作为TTS供应商
- Fish Audio API密钥(https://fish.audio/)
- 语音模型(可在fish audio网站上点击模型的"分享"按钮, 然后删除剪贴板网址中的"https://fish.audio/m/",剩下部分即为模型ID)
- 处理输入文本的LLM API(暂时只适配了Deepseek与ChatGPT, 如用其他模型可尝试在自定义选项内输入模型url,模型名称与APIkey)
- TTS参数(temperature, top_p等)