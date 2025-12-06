# RimTalk TTS - 语音合成模组

这是RimTalk的语音合成扩展，作为独立模组运行。

### 独立模组设计
RimTalk TTS是一个**Rimtalk的子模组**，为Rimtalk添加了语音生成功能

```

## 安装要求

### 前置模组 (Required)
1. **Harmony** - brrainz.harmony
2. **RimTalk** - cj.rimtalk

### 加载顺序
```
Harmony
  ↓
RimTalk
  ↓
RimTalk TTS  ← 此模组
```

在游戏内选项中配置：
- Fish Audio API密钥
- 默认语音模型
- 处理输入文本的LLM API
- TTS参数(temperature, top_p等)