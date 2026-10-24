using System;
using HarmonyLib;
using RimTalk.TTS.Bridge;
using RimTalk.TTS.Data;
using Verse;

namespace RimTalk.TTS.Bootstrap
{
    /// <summary>
    /// Entry point: resolve the RimTalk hook surface once, register patches through
    /// <see cref="PatchRegistry"/>, report the capability table, then create the runtime.
    ///
    /// The runtime is always created (finally block), so a partially failed patch batch can never
    /// leave the module half-started (the old order made an exception in PatchAll skip Initialize
    /// entirely, which looked like "the mod does nothing").
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TtsBootstrap
    {
        private static bool _initialized;

        static TtsBootstrap()
        {
            try
            {
                TTSLog.Message("[RimTalk.TTS] Initializing TTS Module...");

                RimTalkHooks.Resolve();

                var harmony = new Harmony("jlibrary.rimtalk.tts");
                PatchRegistry.ApplyAll(harmony);

                RimTalkHooks.LogReport();
                PatchRegistry.LogReport();

                // Register application quit handler for proper cleanup
                UnityEngine.Application.quitting += OnApplicationQuitting;
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] Failed to initialize patches: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                InitializeRuntime();
            }

            if (_initialized)
                TTSLog.Message("[RimTalk.TTS] TTS Module initialized successfully");
        }

        /// <summary>Create the runtime even when some patches could not be applied.</summary>
        private static void InitializeRuntime()
        {
            try
            {
                var mod = LoadedModManager.GetMod(typeof(TTSMod)) as TTSMod;
                if (mod == null)
                {
                    TTSLog.Error("[RimTalk.TTS] TTSMod instance not found - falling back to default settings");
                    TtsRuntime.Create(new TTSSettings());
                }
                else
                {
                    TtsRuntime.Create(mod.GetSettings<TTSSettings>());
                }

                _initialized = true;
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] Failed to initialize TTS module: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void OnApplicationQuitting()
        {
            try
            {
                TTSLog.Message("[RimTalk.TTS] Application quitting, performing cleanup...");
                TtsRuntime.Current?.OnGameExit();
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] Error during application quit: {ex.Message}");
            }
        }
    }
}
