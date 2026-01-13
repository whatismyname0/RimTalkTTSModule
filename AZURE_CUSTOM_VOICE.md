# Azure Custom Neural Voice Support

## Overview
RimTalkTTS now supports Azure Custom Neural Voice (Professional Voice), allowing you to use your own trained custom voices alongside standard Azure TTS voices.

## Features

### Standard Azure Voices
- Use any of Azure's 400+ neural voices
- Support for 140+ languages and variants
- Speaking styles (cheerful, sad, angry, excited, etc.)
- SSML enhancements (prosody, breaks, emphasis, say-as)

### Custom Neural Voice
- Use your own trained custom voice models
- Requires Azure Speech Service with Professional Voice enabled
- Deployed custom voice models with deployment ID

## Configuration

### 1. Standard Voices (Default)
1. Go to Mod Settings → RimTalkTTS → TTS Settings
2. Select **Azure TTS** as TTS Supplier
3. Enter your **Azure Subscription Key** (API Key)
4. Select or enter your **Azure Region** (e.g., eastus, westeurope)
5. Leave **Deployment ID** empty
6. Configure voice models with standard voice names like:
   - `en-US-JennyNeural`
   - `zh-CN-XiaoxiaoNeural`
   - `ja-JP-NanamiNeural`

### 2. Custom Neural Voice
1. **Train your custom voice** in Azure Speech Studio:
   - Visit: https://speech.microsoft.com/
   - Follow Professional Voice training guide
   - Deploy your trained model to an endpoint

2. **Get your Deployment ID**:
   - In Azure Speech Studio, go to your deployed model
   - Copy the Deployment ID (GUID format)
   - Or use the REST API to list deployments

3. **Configure in RimTalkTTS**:
   - Select **Azure TTS** as TTS Supplier
   - Enter your **Azure Subscription Key**
   - Select the **same region** where you deployed the model
   - Enter your **Deployment ID** in the text field
   - Add voice model using your custom voice name

## API Endpoints

### Standard Voices
```
https://{region}.tts.speech.microsoft.com/cognitiveservices/v1
```

### Custom Voices
```
https://{region}.voice.speech.microsoft.com/cognitiveservices/v1?deploymentId={deploymentId}
```

## How It Works

1. **Without Deployment ID**: Uses standard Azure TTS endpoint with prebuilt neural voices
2. **With Deployment ID**: Uses custom voice endpoint with your trained model

The implementation automatically switches between endpoints based on whether a deployment ID is provided:

```csharp
if (!string.IsNullOrWhiteSpace(deploymentId))
{
    // Custom Neural Voice endpoint
    url = $"https://{region}.voice.speech.microsoft.com/cognitiveservices/v1?deploymentId={deploymentId}";
}
else
{
    // Standard voice endpoint
    url = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1";
}
```

## Voice Configuration

### Standard Voice Example
- Model Name: `Jenny (US English)`
- Model ID: `en-US-JennyNeural`
- Deployment ID: (leave empty)

### Custom Voice Example
- Model Name: `My Character Voice`
- Model ID: `MyCustomVoiceName` (as specified in Azure)
- Deployment ID: `12345678-1234-1234-1234-123456789abc`

## SSML Support

Both standard and custom voices support SSML features:
- **Speaking Styles**: cheerful, sad, angry, excited, friendly, etc.
- **Prosody**: rate, pitch, volume
- **Breaks**: `[break]`, `[long-break]`, `[break:2s]`
- **Emphasis**: `[emphasis]word[/emphasis]`
- **Say-As**: `[number]`, `[date]`, `[time]`, `[telephone]`

## Supported Regions

Custom Neural Voice training and deployment is available in:
- **East US** (eastus)
- **West US** (westus)
- **West US 2** (westus2)
- **East US 2** (eastus2)
- **West Europe** (westeurope)
- **North Europe** (northeurope)
- **Southeast Asia** (southeastasia)
- **East Asia** (eastasia)
- **Australia East** (australiaeast)
- **Japan East** (japaneast)
- **Canada Central** (canadacentral)
- **UK South** (uksouth)
- **Korea Central** (koreacentral)
- **India Central** (centralindia)

## Documentation References

- [Azure Custom Neural Voice Overview](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/how-to-custom-voice)
- [Train Professional Voice](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/professional-voice-train-voice)
- [Deploy Custom Voice](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/professional-voice-deploy-endpoint)
- [REST API Reference](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/rest-text-to-speech)

## Troubleshooting

### Error: 401 Unauthorized
- Check your subscription key is correct
- Verify the key matches the region you selected

### Error: 404 Not Found
- For custom voices: verify deployment ID is correct
- Check that the custom voice is deployed in the selected region
- Ensure the voice model name matches the name in Azure

### No sound output
- Check volume settings in mod configuration
- Verify voice model name is correct (case-sensitive)
- For custom voices: ensure model supports the selected language

### Custom voice not working
1. Verify deployment is "Succeeded" status in Azure
2. Check deployment ID format (should be a GUID)
3. Ensure region matches deployment region
4. Test with standard voice first to verify API key works

## Cost Considerations

- **Standard Voices**: Charged per character processed
- **Custom Neural Voice**: Requires Professional Voice subscription
  - Training costs apply
  - Real-time synthesis charged per character
  - See [Azure Speech Pricing](https://azure.microsoft.com/pricing/details/cognitive-services/speech-services/)

## Example Configuration

```
TTS Supplier: Azure TTS
API Key: your-32-character-subscription-key
Region: eastus
Deployment ID: a1b2c3d4-5678-90ab-cdef-1234567890ab

Voice Models:
1. Standard Jenny
   - Name: Jenny (Standard)
   - ID: en-US-JennyNeural
   
2. Custom Character Voice
   - Name: My Game Character
   - ID: CustomCharacterVoice
```

With this setup:
- Jenny will use standard Azure TTS endpoint
- Custom Character will use your deployed custom voice via the deployment ID
