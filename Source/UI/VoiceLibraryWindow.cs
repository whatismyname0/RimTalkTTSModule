using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimTalk.TTS.Data;
using RimWorld;

namespace RimTalk.TTS.UI
{
    /// <summary>
    /// Window displaying available voices for AzureTTS/EdgeTTS
    /// </summary>
    public class VoiceLibraryWindow : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private readonly TTSSettings.TTSSupplier supplier;
        private string searchText = "";
        private string selectedLanguage = "All";

        // Voice data structure
        private class VoiceEntry
        {
            public string Name;
            public string Gender;
            public string Language;
            public string LanguageDisplay;
            public string Personality;
            public string Category;
        }

        private static readonly List<VoiceEntry> allVoices = InitializeVoices();

        public VoiceLibraryWindow(TTSSettings.TTSSupplier supplier)
        {
            this.supplier = supplier;
            this.doCloseX = true;
            this.forcePause = false;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(1200f, 800f);

        private static List<VoiceEntry> InitializeVoices()
        {
            var voices = new List<VoiceEntry>();

            // 中文语音
            voices.Add(new VoiceEntry { Name = "zh-CN-XiaoxiaoNeural", Gender = "女", Language = "zh-CN", LanguageDisplay = "中文(普通话)", Personality = "温暖", Category = "新闻, 小说" });
            voices.Add(new VoiceEntry { Name = "zh-CN-XiaoyiNeural", Gender = "女", Language = "zh-CN", LanguageDisplay = "中文(普通话)", Personality = "活泼", Category = "卡通, 小说" });
            voices.Add(new VoiceEntry { Name = "zh-CN-YunjianNeural", Gender = "男", Language = "zh-CN", LanguageDisplay = "中文(普通话)", Personality = "激情", Category = "体育, 小说" });
            voices.Add(new VoiceEntry { Name = "zh-CN-YunxiNeural", Gender = "男", Language = "zh-CN", LanguageDisplay = "中文(普通话)", Personality = "活泼, 阳光", Category = "小说" });
            voices.Add(new VoiceEntry { Name = "zh-CN-YunxiaNeural", Gender = "男", Language = "zh-CN", LanguageDisplay = "中文(普通话)", Personality = "可爱", Category = "卡通, 小说" });
            voices.Add(new VoiceEntry { Name = "zh-CN-YunyangNeural", Gender = "男", Language = "zh-CN", LanguageDisplay = "中文(普通话)", Personality = "专业, 可靠", Category = "新闻" });
            voices.Add(new VoiceEntry { Name = "zh-CN-liaoning-XiaobeiNeural", Gender = "女", Language = "zh-CN", LanguageDisplay = "中文(辽宁方言)", Personality = "幽默", Category = "方言" });
            voices.Add(new VoiceEntry { Name = "zh-CN-shaanxi-XiaoniNeural", Gender = "女", Language = "zh-CN", LanguageDisplay = "中文(陕西方言)", Personality = "明亮", Category = "方言" });
            voices.Add(new VoiceEntry { Name = "zh-HK-HiuGaaiNeural", Gender = "女", Language = "zh-HK", LanguageDisplay = "中文(粵語)", Personality = "友善、積極", Category = "通用" });
            voices.Add(new VoiceEntry { Name = "zh-HK-HiuMaanNeural", Gender = "女", Language = "zh-HK", LanguageDisplay = "中文(粵語)", Personality = "友善、積極", Category = "通用" });
            voices.Add(new VoiceEntry { Name = "zh-HK-WanLungNeural", Gender = "男", Language = "zh-HK", LanguageDisplay = "中文(粵語)", Personality = "友善、積極", Category = "通用" });
            voices.Add(new VoiceEntry { Name = "zh-TW-HsiaoChenNeural", Gender = "女", Language = "zh-TW", LanguageDisplay = "中文(台灣)", Personality = "友善、積極", Category = "通用" });
            voices.Add(new VoiceEntry { Name = "zh-TW-HsiaoYuNeural", Gender = "女", Language = "zh-TW", LanguageDisplay = "中文(台灣)", Personality = "友善、積極", Category = "通用" });
            voices.Add(new VoiceEntry { Name = "zh-TW-YunJheNeural", Gender = "男", Language = "zh-TW", LanguageDisplay = "中文(台灣)", Personality = "友善、積極", Category = "通用" });

            // 英语语音 - 美国
            voices.Add(new VoiceEntry { Name = "en-US-JennyNeural", Gender = "Female", Language = "en-US", LanguageDisplay = "English (US)", Personality = "Friendly, Considerate", Category = "General" });
            voices.Add(new VoiceEntry { Name = "en-US-AriaNeural", Gender = "Female", Language = "en-US", LanguageDisplay = "English (US)", Personality = "Positive, Confident", Category = "News, Novel" });
            voices.Add(new VoiceEntry { Name = "en-US-GuyNeural", Gender = "Male", Language = "en-US", LanguageDisplay = "English (US)", Personality = "Passion", Category = "News, Novel" });
            voices.Add(new VoiceEntry { Name = "en-US-AnaNeural", Gender = "Female", Language = "en-US", LanguageDisplay = "English (US)", Personality = "Cute", Category = "Cartoon, Conversation" });
            voices.Add(new VoiceEntry { Name = "en-US-AndrewNeural", Gender = "Male", Language = "en-US", LanguageDisplay = "English (US)", Personality = "Warm, Confident, Honest", Category = "Conversation, Copilot" });
            voices.Add(new VoiceEntry { Name = "en-US-EmmaNeural", Gender = "Female", Language = "en-US", LanguageDisplay = "English (US)", Personality = "Cheerful, Clear", Category = "Conversation, Copilot" });
            voices.Add(new VoiceEntry { Name = "en-US-BrianNeural", Gender = "Male", Language = "en-US", LanguageDisplay = "English (US)", Personality = "Approachable, Casual", Category = "Conversation, Copilot" });
            voices.Add(new VoiceEntry { Name = "en-US-AvaNeural", Gender = "Female", Language = "en-US", LanguageDisplay = "English (US)", Personality = "Expressive, Caring", Category = "Conversation, Copilot" });
            voices.Add(new VoiceEntry { Name = "en-US-ChristopherNeural", Gender = "Male", Language = "en-US", LanguageDisplay = "English (US)", Personality = "Reliable, Authority", Category = "News, Novel" });
            voices.Add(new VoiceEntry { Name = "en-US-EricNeural", Gender = "Male", Language = "en-US", LanguageDisplay = "English (US)", Personality = "Rational", Category = "News, Novel" });
            voices.Add(new VoiceEntry { Name = "en-US-MichelleNeural", Gender = "Female", Language = "en-US", LanguageDisplay = "English (US)", Personality = "Friendly, Pleasant", Category = "News, Novel" });
            voices.Add(new VoiceEntry { Name = "en-US-RogerNeural", Gender = "Male", Language = "en-US", LanguageDisplay = "English (US)", Personality = "Lively", Category = "News, Novel" });
            voices.Add(new VoiceEntry { Name = "en-US-SteffanNeural", Gender = "Male", Language = "en-US", LanguageDisplay = "English (US)", Personality = "Rational", Category = "News, Novel" });

            // 英语语音 - 英国
            voices.Add(new VoiceEntry { Name = "en-GB-LibbyNeural", Gender = "Female", Language = "en-GB", LanguageDisplay = "English (UK)", Personality = "Friendly, Positive", Category = "General" });
            voices.Add(new VoiceEntry { Name = "en-GB-MaisieNeural", Gender = "Female", Language = "en-GB", LanguageDisplay = "English (UK)", Personality = "Friendly, Positive", Category = "General" });
            voices.Add(new VoiceEntry { Name = "en-GB-RyanNeural", Gender = "Male", Language = "en-GB", LanguageDisplay = "English (UK)", Personality = "Friendly, Positive", Category = "General" });
            voices.Add(new VoiceEntry { Name = "en-GB-SoniaNeural", Gender = "Female", Language = "en-GB", LanguageDisplay = "English (UK)", Personality = "Friendly, Positive", Category = "General" });
            voices.Add(new VoiceEntry { Name = "en-GB-ThomasNeural", Gender = "Male", Language = "en-GB", LanguageDisplay = "English (UK)", Personality = "Friendly, Positive", Category = "General" });

            // 英语语音 - 澳大利亚
            voices.Add(new VoiceEntry { Name = "en-AU-NatashaNeural", Gender = "Female", Language = "en-AU", LanguageDisplay = "English (Australia)", Personality = "Friendly, Positive", Category = "General" });
            voices.Add(new VoiceEntry { Name = "en-AU-WilliamMultilingualNeural", Gender = "Male", Language = "en-AU", LanguageDisplay = "English (Australia)", Personality = "Friendly, Positive", Category = "General" });

            // 英语语音 - 其他地区
            voices.Add(new VoiceEntry { Name = "en-CA-ClaraNeural", Gender = "Female", Language = "en-CA", LanguageDisplay = "English (Canada)", Personality = "Friendly, Positive", Category = "General" });
            voices.Add(new VoiceEntry { Name = "en-CA-LiamNeural", Gender = "Male", Language = "en-CA", LanguageDisplay = "English (Canada)", Personality = "Friendly, Positive", Category = "General" });
            voices.Add(new VoiceEntry { Name = "en-IN-NeerjaNeural", Gender = "Female", Language = "en-IN", LanguageDisplay = "English (India)", Personality = "Friendly, Positive", Category = "General" });
            voices.Add(new VoiceEntry { Name = "en-IN-PrabhatNeural", Gender = "Male", Language = "en-IN", LanguageDisplay = "English (India)", Personality = "Friendly, Positive", Category = "General" });

            // 日语
            voices.Add(new VoiceEntry { Name = "ja-JP-NanamiNeural", Gender = "女性", Language = "ja-JP", LanguageDisplay = "日本語", Personality = "フレンドリー, ポジティブ", Category = "一般" });
            voices.Add(new VoiceEntry { Name = "ja-JP-KeitaNeural", Gender = "男性", Language = "ja-JP", LanguageDisplay = "日本語", Personality = "フレンドリー, ポジティブ", Category = "一般" });

            // 韩语
            voices.Add(new VoiceEntry { Name = "ko-KR-SunHiNeural", Gender = "여성", Language = "ko-KR", LanguageDisplay = "한국어", Personality = "친근함, 긍정적", Category = "일반" });
            voices.Add(new VoiceEntry { Name = "ko-KR-InJoonNeural", Gender = "남성", Language = "ko-KR", LanguageDisplay = "한국어", Personality = "친근함, 긍정적", Category = "일반" });
            voices.Add(new VoiceEntry { Name = "ko-KR-HyunsuMultilingualNeural", Gender = "남성", Language = "ko-KR", LanguageDisplay = "한국어", Personality = "친근함, 긍정적", Category = "일반" });

            // 法语
            voices.Add(new VoiceEntry { Name = "fr-FR-DeniseNeural", Gender = "Femme", Language = "fr-FR", LanguageDisplay = "Français (France)", Personality = "Amical, Positif", Category = "Général" });
            voices.Add(new VoiceEntry { Name = "fr-FR-HenriNeural", Gender = "Homme", Language = "fr-FR", LanguageDisplay = "Français (France)", Personality = "Amical, Positif", Category = "Général" });
            voices.Add(new VoiceEntry { Name = "fr-CA-SylvieNeural", Gender = "Femme", Language = "fr-CA", LanguageDisplay = "Français (Canada)", Personality = "Amical, Positif", Category = "Général" });
            voices.Add(new VoiceEntry { Name = "fr-CA-JeanNeural", Gender = "Homme", Language = "fr-CA", LanguageDisplay = "Français (Canada)", Personality = "Amical, Positif", Category = "Général" });

            // 德语
            voices.Add(new VoiceEntry { Name = "de-DE-KatjaNeural", Gender = "Weiblich", Language = "de-DE", LanguageDisplay = "Deutsch (Deutschland)", Personality = "Freundlich, Positiv", Category = "Allgemein" });
            voices.Add(new VoiceEntry { Name = "de-DE-ConradNeural", Gender = "Männlich", Language = "de-DE", LanguageDisplay = "Deutsch (Deutschland)", Personality = "Freundlich, Positiv", Category = "Allgemein" });
            voices.Add(new VoiceEntry { Name = "de-AT-IngridNeural", Gender = "Weiblich", Language = "de-AT", LanguageDisplay = "Deutsch (Österreich)", Personality = "Freundlich, Positiv", Category = "Allgemein" });
            voices.Add(new VoiceEntry { Name = "de-AT-JonasNeural", Gender = "Männlich", Language = "de-AT", LanguageDisplay = "Deutsch (Österreich)", Personality = "Freundlich, Positiv", Category = "Allgemein" });

            // 西班牙语
            voices.Add(new VoiceEntry { Name = "es-ES-ElviraNeural", Gender = "Mujer", Language = "es-ES", LanguageDisplay = "Español (España)", Personality = "Amigable, Positivo", Category = "General" });
            voices.Add(new VoiceEntry { Name = "es-ES-AlvaroNeural", Gender = "Hombre", Language = "es-ES", LanguageDisplay = "Español (España)", Personality = "Amigable, Positivo", Category = "General" });
            voices.Add(new VoiceEntry { Name = "es-MX-DaliaNeural", Gender = "Mujer", Language = "es-MX", LanguageDisplay = "Español (México)", Personality = "Amigable, Positivo", Category = "General" });
            voices.Add(new VoiceEntry { Name = "es-MX-JorgeNeural", Gender = "Hombre", Language = "es-MX", LanguageDisplay = "Español (México)", Personality = "Amigable, Positivo", Category = "General" });
            voices.Add(new VoiceEntry { Name = "es-US-PalomaNeural", Gender = "Mujer", Language = "es-US", LanguageDisplay = "Español (US)", Personality = "Amigable, Positivo", Category = "General" });
            voices.Add(new VoiceEntry { Name = "es-US-AlonsoNeural", Gender = "Hombre", Language = "es-US", LanguageDisplay = "Español (US)", Personality = "Amigable, Positivo", Category = "General" });

            // 俄语
            voices.Add(new VoiceEntry { Name = "ru-RU-SvetlanaNeural", Gender = "Женский", Language = "ru-RU", LanguageDisplay = "Русский", Personality = "Дружелюбный, Позитивный", Category = "Общий" });
            voices.Add(new VoiceEntry { Name = "ru-RU-DmitryNeural", Gender = "Мужской", Language = "ru-RU", LanguageDisplay = "Русский", Personality = "Дружелюбный, Позитивный", Category = "Общий" });

            // 意大利语
            voices.Add(new VoiceEntry { Name = "it-IT-IsabellaNeural", Gender = "Donna", Language = "it-IT", LanguageDisplay = "Italiano", Personality = "Amichevole, Positivo", Category = "Generale" });
            voices.Add(new VoiceEntry { Name = "it-IT-DiegoNeural", Gender = "Uomo", Language = "it-IT", LanguageDisplay = "Italiano", Personality = "Amichevole, Positivo", Category = "Generale" });

            // 葡萄牙语
            voices.Add(new VoiceEntry { Name = "pt-BR-FranciscaNeural", Gender = "Mulher", Language = "pt-BR", LanguageDisplay = "Português (Brasil)", Personality = "Amigável, Positivo", Category = "Geral" });
            voices.Add(new VoiceEntry { Name = "pt-BR-AntonioNeural", Gender = "Homem", Language = "pt-BR", LanguageDisplay = "Português (Brasil)", Personality = "Amigável, Positivo", Category = "Geral" });
            voices.Add(new VoiceEntry { Name = "pt-PT-RaquelNeural", Gender = "Mulher", Language = "pt-PT", LanguageDisplay = "Português (Portugal)", Personality = "Amigável, Positivo", Category = "Geral" });
            voices.Add(new VoiceEntry { Name = "pt-PT-DuarteNeural", Gender = "Homem", Language = "pt-PT", LanguageDisplay = "Português (Portugal)", Personality = "Amigável, Positivo", Category = "Geral" });

            // 阿拉伯语
            voices.Add(new VoiceEntry { Name = "ar-SA-ZariyahNeural", Gender = "أنثى", Language = "ar-SA", LanguageDisplay = "العربية (السعودية)", Personality = "ودود، إيجابي", Category = "عام" });
            voices.Add(new VoiceEntry { Name = "ar-SA-HamedNeural", Gender = "ذكر", Language = "ar-SA", LanguageDisplay = "العربية (السعودية)", Personality = "ودود، إيجابي", Category = "عام" });
            voices.Add(new VoiceEntry { Name = "ar-EG-SalmaNeural", Gender = "أنثى", Language = "ar-EG", LanguageDisplay = "العربية (مصر)", Personality = "ودود، إيجابي", Category = "عام" });
            voices.Add(new VoiceEntry { Name = "ar-EG-ShakirNeural", Gender = "ذكر", Language = "ar-EG", LanguageDisplay = "العربية (مصر)", Personality = "ودود، إيجابي", Category = "عام" });

            // 印地语
            voices.Add(new VoiceEntry { Name = "hi-IN-SwaraNeural", Gender = "महिला", Language = "hi-IN", LanguageDisplay = "हिन्दी", Personality = "मित्रवत, सकारात्मक", Category = "सामान्य" });
            voices.Add(new VoiceEntry { Name = "hi-IN-MadhurNeural", Gender = "पुरुष", Language = "hi-IN", LanguageDisplay = "हिन्दी", Personality = "मित्रवत, सकारात्मक", Category = "सामान्य" });

            // 泰语
            voices.Add(new VoiceEntry { Name = "th-TH-PremwadeeNeural", Gender = "หญิง", Language = "th-TH", LanguageDisplay = "ไทย", Personality = "เป็นมิตร, เชิงบวก", Category = "ทั่วไป" });
            voices.Add(new VoiceEntry { Name = "th-TH-NiwatNeural", Gender = "ชาย", Language = "th-TH", LanguageDisplay = "ไทย", Personality = "เป็นมิตร, เชิงบวก", Category = "ทั่วไป" });

            // 越南语
            voices.Add(new VoiceEntry { Name = "vi-VN-HoaiMyNeural", Gender = "Nữ", Language = "vi-VN", LanguageDisplay = "Tiếng Việt", Personality = "Thân thiện, Tích cực", Category = "Tổng quát" });
            voices.Add(new VoiceEntry { Name = "vi-VN-NamMinhNeural", Gender = "Nam", Language = "vi-VN", LanguageDisplay = "Tiếng Việt", Personality = "Thân thiện, Tích cực", Category = "Tổng quát" });

            // 印尼语
            voices.Add(new VoiceEntry { Name = "id-ID-GadisNeural", Gender = "Perempuan", Language = "id-ID", LanguageDisplay = "Bahasa Indonesia", Personality = "Ramah, Positif", Category = "Umum" });
            voices.Add(new VoiceEntry { Name = "id-ID-ArdiNeural", Gender = "Laki-laki", Language = "id-ID", LanguageDisplay = "Bahasa Indonesia", Personality = "Ramah, Positif", Category = "Umum" });

            // 土耳其语
            voices.Add(new VoiceEntry { Name = "tr-TR-EmelNeural", Gender = "Kadın", Language = "tr-TR", LanguageDisplay = "Türkçe", Personality = "Arkadaş canlısı, Olumlu", Category = "Genel" });
            voices.Add(new VoiceEntry { Name = "tr-TR-AhmetNeural", Gender = "Erkek", Language = "tr-TR", LanguageDisplay = "Türkçe", Personality = "Arkadaş canlısı, Olumlu", Category = "Genel" });

            // 波兰语
            voices.Add(new VoiceEntry { Name = "pl-PL-ZofiaNeural", Gender = "Kobieta", Language = "pl-PL", LanguageDisplay = "Polski", Personality = "Przyjazny, Pozytywny", Category = "Ogólny" });
            voices.Add(new VoiceEntry { Name = "pl-PL-MarekNeural", Gender = "Mężczyzna", Language = "pl-PL", LanguageDisplay = "Polski", Personality = "Przyjazny, Pozytywny", Category = "Ogólny" });

            // 荷兰语
            voices.Add(new VoiceEntry { Name = "nl-NL-ColetteNeural", Gender = "Vrouw", Language = "nl-NL", LanguageDisplay = "Nederlands", Personality = "Vriendelijk, Positief", Category = "Algemeen" });
            voices.Add(new VoiceEntry { Name = "nl-NL-MaartenNeural", Gender = "Man", Language = "nl-NL", LanguageDisplay = "Nederlands", Personality = "Vriendelijk, Positief", Category = "Algemeen" });

            // 瑞典语
            voices.Add(new VoiceEntry { Name = "sv-SE-SofieNeural", Gender = "Kvinna", Language = "sv-SE", LanguageDisplay = "Svenska", Personality = "Vänlig, Positiv", Category = "Allmän" });
            voices.Add(new VoiceEntry { Name = "sv-SE-MattiasNeural", Gender = "Man", Language = "sv-SE", LanguageDisplay = "Svenska", Personality = "Vänlig, Positiv", Category = "Allmän" });

            // 丹麦语
            voices.Add(new VoiceEntry { Name = "da-DK-ChristelNeural", Gender = "Kvinde", Language = "da-DK", LanguageDisplay = "Dansk", Personality = "Venlig, Positiv", Category = "Generel" });
            voices.Add(new VoiceEntry { Name = "da-DK-JeppeNeural", Gender = "Mand", Language = "da-DK", LanguageDisplay = "Dansk", Personality = "Venlig, Positiv", Category = "Generel" });

            // 挪威语
            voices.Add(new VoiceEntry { Name = "nb-NO-PernilleNeural", Gender = "Kvinne", Language = "nb-NO", LanguageDisplay = "Norsk", Personality = "Vennlig, Positiv", Category = "Generell" });
            voices.Add(new VoiceEntry { Name = "nb-NO-FinnNeural", Gender = "Mann", Language = "nb-NO", LanguageDisplay = "Norsk", Personality = "Vennlig, Positiv", Category = "Generell" });

            // 芬兰语
            voices.Add(new VoiceEntry { Name = "fi-FI-NooraNeural", Gender = "Nainen", Language = "fi-FI", LanguageDisplay = "Suomi", Personality = "Ystävällinen, Positiivinen", Category = "Yleinen" });
            voices.Add(new VoiceEntry { Name = "fi-FI-HarriNeural", Gender = "Mies", Language = "fi-FI", LanguageDisplay = "Suomi", Personality = "Ystävällinen, Positiivinen", Category = "Yleinen" });

            return voices;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            
            // Title
            Rect titleRect = new Rect(0f, 0f, inRect.width, 40f);
            Text.Font = GameFont.Medium;
            string title = supplier == TTSSettings.TTSSupplier.AzureTTS ? "AzureTTS Voice Library" : "EdgeTTS Voice Library";
            Widgets.Label(titleRect, title);
            Text.Font = GameFont.Small;

            // Search and filter section
            Rect searchRect = new Rect(0f, 45f, inRect.width * 0.6f, 30f);
            Widgets.Label(new Rect(searchRect.x, searchRect.y, 80f, 30f), "Search:");
            searchText = Widgets.TextField(new Rect(searchRect.x + 85f, searchRect.y, searchRect.width - 85f, 30f), searchText);

            // Language filter dropdown
            Rect filterRect = new Rect(inRect.width * 0.65f, 45f, inRect.width * 0.35f - 10f, 30f);
            if (Widgets.ButtonText(new Rect(filterRect.x, filterRect.y, 100f, 30f), "Language:"))
            {
                var languages = allVoices.Select(v => v.LanguageDisplay).Distinct().OrderBy(l => l).ToList();
                languages.Insert(0, "All");
                var options = new List<FloatMenuOption>();
                foreach (var lang in languages)
                {
                    options.Add(new FloatMenuOption(lang, delegate { selectedLanguage = lang; }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            Widgets.Label(new Rect(filterRect.x + 105f, filterRect.y, filterRect.width - 105f, 30f), selectedLanguage);

            // Table header
            Rect tableRect = new Rect(0f, 85f, inRect.width, inRect.height - 95f);
            Rect headerRect = new Rect(tableRect.x, tableRect.y, tableRect.width - 16f, 30f);
            
            float col1Width = 300f; // Voice Name
            float col2Width = 80f;  // Gender
            float col3Width = 200f; // Language
            float col4Width = 200f; // Personality
            float col5Width = headerRect.width - col1Width - col2Width - col3Width - col4Width; // Category

            GUI.color = new Color(0.3f, 0.3f, 0.3f);
            Widgets.DrawBoxSolid(headerRect, GUI.color);
            GUI.color = Color.white;

            Widgets.Label(new Rect(headerRect.x + 5f, headerRect.y + 5f, col1Width, 25f), "Voice Name");
            Widgets.Label(new Rect(headerRect.x + col1Width + 5f, headerRect.y + 5f, col2Width, 25f), "Gender");
            Widgets.Label(new Rect(headerRect.x + col1Width + col2Width + 5f, headerRect.y + 5f, col3Width, 25f), "Language");
            Widgets.Label(new Rect(headerRect.x + col1Width + col2Width + col3Width + 5f, headerRect.y + 5f, col4Width, 25f), "Personality");
            Widgets.Label(new Rect(headerRect.x + col1Width + col2Width + col3Width + col4Width + 5f, headerRect.y + 5f, col5Width, 25f), "Category");

            // Filter voices
            var filteredVoices = allVoices.AsEnumerable();
            if (!string.IsNullOrEmpty(searchText))
            {
                filteredVoices = filteredVoices.Where(v => 
                    v.Name.ToLower().Contains(searchText.ToLower()) ||
                    v.LanguageDisplay.ToLower().Contains(searchText.ToLower()) ||
                    v.Personality.ToLower().Contains(searchText.ToLower()));
            }
            if (selectedLanguage != "All")
            {
                filteredVoices = filteredVoices.Where(v => v.LanguageDisplay == selectedLanguage);
            }

            // Group by language for organized display
            var groupedVoices = filteredVoices.GroupBy(v => v.LanguageDisplay).OrderBy(g => g.Key);

            // Scrollable table content
            Rect contentRect = new Rect(tableRect.x, tableRect.y + 35f, tableRect.width - 16f, tableRect.height - 35f);
            float totalHeight = 0f;
            foreach (var group in groupedVoices)
            {
                totalHeight += 35f; // Language header
                totalHeight += group.Count() * 30f; // Voice rows
            }

            Rect viewRect = new Rect(0f, 0f, contentRect.width - 20f, totalHeight);
            Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);

            float yPos = 0f;
            bool alternate = false;

            foreach (var group in groupedVoices)
            {
                // Language group header
                Rect groupHeaderRect = new Rect(0f, yPos, viewRect.width, 30f);
                GUI.color = new Color(0.2f, 0.4f, 0.6f);
                Widgets.DrawBoxSolid(groupHeaderRect, GUI.color);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(groupHeaderRect.x + 5f, groupHeaderRect.y + 5f, groupHeaderRect.width, 25f), 
                    $"{group.Key} ({group.Count()} voices)");
                yPos += 35f;

                // Voice rows
                foreach (var voice in group.OrderBy(v => v.Name))
                {
                    Rect rowRect = new Rect(0f, yPos, viewRect.width, 30f);
                    
                    if (alternate)
                    {
                        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                        Widgets.DrawBoxSolid(rowRect, GUI.color);
                        GUI.color = Color.white;
                    }

                    // Make row clickable to copy voice name
                    if (Widgets.ButtonInvisible(rowRect))
                    {
                        GUIUtility.systemCopyBuffer = voice.Name;
                        Messages.Message($"Copied to clipboard: {voice.Name}", MessageTypeDefOf.PositiveEvent, false);
                    }

                    if (Mouse.IsOver(rowRect))
                    {
                        GUI.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
                        Widgets.DrawBoxSolid(rowRect, GUI.color);
                        GUI.color = Color.white;
                    }

                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(rowRect.x + 5f, rowRect.y + 5f, col1Width - 10f, 25f), voice.Name);
                    Widgets.Label(new Rect(rowRect.x + col1Width + 5f, rowRect.y + 5f, col2Width - 10f, 25f), voice.Gender);
                    Widgets.Label(new Rect(rowRect.x + col1Width + col2Width + 5f, rowRect.y + 5f, col3Width - 10f, 25f), voice.LanguageDisplay);
                    Widgets.Label(new Rect(rowRect.x + col1Width + col2Width + col3Width + 5f, rowRect.y + 5f, col4Width - 10f, 25f), voice.Personality);
                    Widgets.Label(new Rect(rowRect.x + col1Width + col2Width + col3Width + col4Width + 5f, rowRect.y + 5f, col5Width - 10f, 25f), voice.Category);
                    Text.Font = GameFont.Small;

                    yPos += 30f;
                    alternate = !alternate;
                }
            }

            Widgets.EndScrollView();

            // Footer info
            Rect footerRect = new Rect(0f, inRect.height - 25f, inRect.width, 25f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(footerRect, $"Total: {filteredVoices.Count()} voices | Click any row to copy voice name to clipboard");
            Text.Font = GameFont.Small;
        }
    }
}
