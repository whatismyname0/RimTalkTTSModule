# Azure TTS 供应商实现文档

## 概述

已成功将 Microsoft Azure Text-to-Speech API 添加为 RimTalkTTS 模组的新供应商选项。

## 实现的功能

### 1. 核心组件

#### AzureTTSClient.cs
- 位置：`Source/Service/AzureTTSClient.cs`
- 功能：
  - 通过 REST API 调用 Azure TTS 服务
  - 支持 SSML 格式的请求构建
  - 支持语速调节（通过 SSML prosody 标签）
  - 自动生成 SSML 文档
  - 返回 WAV 格式音频（24kHz 16-bit mono PCM）
  - 提供语音列表获取功能（GetVoicesAsync）

#### AzureTTSProvider.cs
- 位置：`Source/Provider/AzureTTSProvider.cs`
- 功能：
  - 实现 ITTSProvider 接口
  - 管理 Azure 区域配置
  - 验证 API 密钥（至少32字符）
  - 无需资源清理（纯 HTTP 客户端）

### 2. 配置系统

#### TTSSettings.cs 更新
- 添加 `AzureTTS` 枚举值到 `TTSSupplier`
- 添加 `SupplierRegion` 字典用于存储区域配置
- 添加区域访问方法：
  - `GetSupplierRegion(TTSSupplier)`
  - `SetSupplierRegion(TTSSupplier, string)`
- 在 `LoadOldSettings()` 中初始化 Azure TTS 默认配置
- 在 `GetDefaultVoiceModels()` 中添加 8 个预设语音：
  - 英语（美国）：Jenny, Guy, Aria, Davis
  - 英语（英国）：Sonia, Ryan
  - 中文（简体）：Xiaoxiao, Yunxi

### 3. UI 集成

#### SettingsUI.cs 更新
- 在供应商选择菜单中添加 "Azure TTS" 选项
- 添加区域配置界面：
  - 下拉菜单选择常用区域（11个预设区域）
  - 文本输入框支持自定义区域
  - 区域更改时自动更新 Provider

#### 支持的 Azure 区域
- eastus, westus, westus2, eastus2
- westeurope, northeurope
- southeastasia, eastasia
- australiaeast, japaneast
- canadacentral

### 4. 翻译文本

已添加中英文翻译：
- `RimTalk.Settings.TTS.TTSSupplier.AzureTTS` - "Azure TTS"
- `RimTalk.Settings.TTS.AzureRegionLabel` - "Azure Region: {0}" / "Azure 区域：{0}"
- `RimTalk.Settings.TTS.CustomRegionLabel` - 自定义区域输入提示

### 5. 服务层集成

#### TTSService.cs 更新
- `SetProvider()` 方法添加 settings 可选参数
- 添加 AzureTTS case 分支
- 在创建 Provider 时设置区域配置
- 更新所有调用点传入 settings 参数

## API 使用说明

### 认证方式
Azure TTS 使用订阅密钥（Subscription Key）认证：
```
Header: Ocp-Apim-Subscription-Key: YOUR_KEY
```

### 端点格式
```
https://{region}.tts.speech.microsoft.com/cognitiveservices/v1
```

### SSML 请求示例
```xml
<speak version="1.0" xml:lang="en-US">
  <voice name="en-US-JennyNeural">
    <prosody rate="+0%">
      Hello, this is a test.
    </prosody>
  </voice>
</speak>
```

### 音频输出格式
- 格式：RIFF WAV (riff-24khz-16bit-mono-pcm)
- 采样率：24kHz
- 位深度：16-bit
- 声道：单声道

## 配置步骤

1. **获取 Azure 订阅密钥**：
   - 在 Azure Portal 创建 Speech 服务资源
   - 复制订阅密钥

2. **在游戏中配置**：
   - 打开 Mod 设置
   - 选择 "Azure TTS" 作为供应商
   - 输入 API 密钥
   - 选择合适的区域（默认 eastus）
   - 配置语音模型（从预设中选择或手动输入）

3. **测试**：
   - 设置翻译语言
   - 为角色分配语音模型
   - 触发对话测试

## 技术特点

### 优点
✅ 官方 Microsoft 服务，稳定性高
✅ 支持多语言、多口音
✅ 语音质量优秀（Neural TTS）
✅ 全球多区域部署
✅ 支持 SSML 精细控制
✅ 无需本地服务器或 Python 环境

### 限制
⚠️ 需要 Azure 订阅和付费（有免费额度）
⚠️ 依赖网络连接
⚠️ API 密钥至少 32 字符
⚠️ 部分高级功能（如自定义语音）需额外配置

## 与其他供应商的对比

| 特性 | FishAudio | CosyVoice | IndexTTS | Azure TTS |
|------|-----------|-----------|----------|-----------|
| 实现方式 | Python Server | HTTP Client | HTTP Client | HTTP Client |
| 需要本地服务 | 是 | 否 | 否 | 否 |
| 区域配置 | 否 | 否 | 否 | 是 |
| 默认语音数 | 0 | 8 | 8 | 8 |
| 支持自定义语音 | 是 | 是 | 是 | 是（需额外配置）|
| 音频格式 | WAV | WAV | WAV | WAV |
| 速度控制 | 是 | 是 | 是 | 是（SSML）|

## 文件清单

### 新增文件
- `Source/Service/AzureTTSClient.cs` - Azure TTS HTTP 客户端
- `Source/Provider/AzureTTSProvider.cs` - Azure TTS 供应商实现

### 修改文件
- `Source/Data/TTSSettings.cs` - 添加枚举、配置字段、访问方法
- `Source/Service/TTSService.cs` - 更新 SetProvider 方法
- `Source/UI/SettingsUI.cs` - 添加 UI 选项和区域配置
- `Source/TTSModule.cs` - 更新 SetProvider 调用
- `Languages/English/Keyed/TTS_English.xml` - 添加英文翻译
- `Languages/ChineseSimplified/Keyed/TTS_ChineseSimplified.xml` - 添加中文翻译

## 扩展建议

### 未来可能的增强功能
1. **语音风格支持**：利用 Azure 的 `<mstts:express-as>` 标签
2. **批量语音列表加载**：从 Azure API 获取所有可用语音
3. **自定义语音支持**：集成 Azure Custom Voice
4. **错误处理增强**：更详细的错误信息和重试机制
5. **缓存机制**：缓存常用语音以减少 API 调用

## 参考文档

- [Azure TTS REST API 文档](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/rest-text-to-speech)
- [Azure TTS SSML 文档](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/speech-synthesis-markup)
- [Azure 区域列表](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/regions)
- [Azure 语音列表](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=tts)
