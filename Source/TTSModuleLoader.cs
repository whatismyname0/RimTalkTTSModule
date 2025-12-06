using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace RimTalk.TTS
{
    /// <summary>
    /// Entry point for TTS module - applies Harmony patches to hook into main RimTalk
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TTSModuleLoader
    {
        static TTSModuleLoader()
        {
            try
            {
                Log.Message("[RimTalk.TTS] Initializing TTS Module...");
                
                var harmony = new Harmony("jlibrary.rimtalk.tts");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                
                TTSModule.Instance.Initialize();
                
                // Register application quit handler for proper cleanup
                UnityEngine.Application.quitting += OnApplicationQuitting;
                
                Log.Message("[RimTalk.TTS] TTS Module initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] Failed to initialize: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void OnApplicationQuitting()
        {
            try
            {
                Log.Message("[RimTalk.TTS] Application quitting, performing cleanup...");
                TTSModule.Instance.OnGameExit();
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] Error during application quit: {ex.Message}");
            }
        }
    }
}
