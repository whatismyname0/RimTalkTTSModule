#!/usr/bin/env python3
"""
Simple Edge TTS client wrapper for RimTalk TTS
Calls edge-tts to generate speech audio

Based on https://github.com/rany2/edge-tts
"""

import sys
import asyncio
import argparse
import edge_tts


async def generate_speech(text: str, voice: str, rate: str, volume: str, pitch: str, output_file: str):
    """
    Generate speech using edge-tts
    
    Args:
        text: Text to convert to speech
        voice: Voice name (e.g., en-US-JennyNeural)
        rate: Speech rate (e.g., +0%, +50%, -25%)
        volume: Speech volume (e.g., +0%, +50%, -25%)
        pitch: Speech pitch (e.g., +0Hz, +10Hz, -5Hz)
        output_file: Output file path
    """
    try:
        # Create Communicate instance with all parameters
        communicate = edge_tts.Communicate(
            text=text,
            voice=voice,
            rate=rate,
            volume=volume,
            pitch=pitch
        )
        
        # Save audio to file (MP3 format by default)
        await communicate.save(output_file)
        
        print("SUCCESS", flush=True)
    except Exception as e:
        print(f"ERROR: {type(e).__name__}: {str(e)}", file=sys.stderr, flush=True)
        sys.exit(1)


def main():
    parser = argparse.ArgumentParser(
        description="Generate speech using Microsoft Edge TTS",
        formatter_class=argparse.RawDescriptionHelpFormatter
    )
    
    parser.add_argument("text", help="Text to convert to speech")
    parser.add_argument("voice", help="Voice name (e.g., en-US-JennyNeural)")
    parser.add_argument("--rate", default="+0%", help="Speech rate (e.g., +50%%, -25%%), default: +0%%")
    parser.add_argument("--volume", default="+0%", help="Speech volume (e.g., +50%%, -25%%), default: +0%%")
    parser.add_argument("--pitch", default="+0Hz", help="Speech pitch (e.g., +10Hz, -5Hz), default: +0Hz")
    parser.add_argument("--output", "-o", required=True, help="Output file path (MP3 format)")
    
    args = parser.parse_args()
    
    # Run async generation
    asyncio.run(generate_speech(
        text=args.text,
        voice=args.voice,
        rate=args.rate,
        volume=args.volume,
        pitch=args.pitch,
        output_file=args.output
    ))


if __name__ == "__main__":
    main()
