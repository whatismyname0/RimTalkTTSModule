#!/usr/bin/env python3
"""
Fish Audio TTS Standalone Test
This script tests the Fish Audio TTS functionality independently
"""

import sys
import asyncio
import json

def test_imports():
    """Test if required packages can be imported"""
    print("=" * 60)
    print("1. Testing Python imports...")
    print("=" * 60)
    
    try:
        import fishaudio
        print(f"✓ fishaudio package found (version: {getattr(fishaudio, '__version__', 'unknown')})")
    except ImportError as e:
        print(f"✗ fishaudio package not found: {e}")
        print("  Fix: pip install fish-audio-sdk")
        return False
    
    try:
        from fishaudio import AsyncFishAudio
        print("✓ AsyncFishAudio class available")
    except ImportError as e:
        print(f"✗ Cannot import AsyncFishAudio: {e}")
        return False
    
    try:
        from fishaudio.types import TTSConfig, Prosody
        print("✓ TTSConfig and Prosody types available")
    except ImportError as e:
        print(f"✗ Cannot import fishaudio types: {e}")
        return False
    
    print("\n✓ All imports successful!\n")
    return True

async def test_api_connection(api_key, reference_id):
    """Test connection to Fish Audio API"""
    print("=" * 60)
    print("2. Testing API connection...")
    print("=" * 60)
    
    if not api_key:
        print("✗ No API key provided")
        print("  Usage: python test_fishaudio.py <API_KEY> <REFERENCE_ID>")
        return False
    
    if not reference_id:
        print("✗ No reference ID provided")
        print("  Usage: python test_fishaudio.py <API_KEY> <REFERENCE_ID>")
        return False
    
    try:
        from fishaudio import AsyncFishAudio
        from fishaudio.types import TTSConfig, Prosody
        
        print(f"Creating client for API: https://api.fish.audio")
        client = AsyncFishAudio(
            api_key=api_key,
            base_url="https://api.fish.audio"
        )
        
        config = TTSConfig(
            prosody=Prosody(speed=1.0, volume=0),
            reference_id=reference_id,
            format="wav",
            latency="normal",
            normalize=False,
            model="s1",
            temperature=0.9,
            top_p=0.9
        )
        
        test_text = "Hello, this is a test."
        print(f"Sending test request: '{test_text}'")
        print("This may take a few seconds...")
        
        audio_data = await asyncio.wait_for(
            client.tts.convert(text=test_text, config=config),
            timeout=30.0
        )
        
        # Collect audio data
        if hasattr(audio_data, 'collect'):
            audio_bytes = audio_data.collect()
        else:
            audio_bytes = audio_data
        
        if isinstance(audio_bytes, (list, tuple)):
            audio_bytes = b''.join(audio_bytes)
        
        if len(audio_bytes) > 0:
            print(f"✓ API request successful! Received {len(audio_bytes)} bytes of audio data")
            print("\n✓ Fish Audio TTS is working correctly!\n")
            return True
        else:
            print("✗ API returned empty audio data")
            return False
            
    except asyncio.TimeoutError:
        print("✗ Request timed out after 30 seconds")
        print("  Possible causes:")
        print("  - Slow internet connection")
        print("  - Fish Audio API is experiencing issues")
        return False
    except Exception as e:
        print(f"✗ API test failed: {e}")
        error_str = str(e)
        
        if "401" in error_str or "Unauthorized" in error_str:
            print("  Cause: Invalid API key")
        elif "403" in error_str or "Forbidden" in error_str:
            print("  Cause: API key doesn't have permission")
        elif "404" in error_str or "Not Found" in error_str:
            print(f"  Cause: Reference voice ID '{reference_id}' not found")
        elif "timeout" in error_str.lower():
            print("  Cause: Network timeout - check your internet connection")
        elif "connection" in error_str.lower():
            print("  Cause: Connection error - check firewall and internet")
        
        return False

def print_system_info():
    """Print system information"""
    print("=" * 60)
    print("System Information")
    print("=" * 60)
    print(f"Python version: {sys.version}")
    print(f"Python executable: {sys.executable}")
    print()

if __name__ == "__main__":
    print("\n" + "=" * 60)
    print("Fish Audio TTS Test Script")
    print("=" * 60 + "\n")
    
    print_system_info()
    
    # Test imports
    if not test_imports():
        print("\n✗ Import test failed. Cannot proceed with API test.")
        sys.exit(1)
    
    # Test API (if credentials provided)
    if len(sys.argv) >= 3:
        api_key = sys.argv[1]
        reference_id = sys.argv[2]
        
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        success = loop.run_until_complete(test_api_connection(api_key, reference_id))
        loop.close()
        
        if success:
            print("=" * 60)
            print("All tests passed! Fish Audio TTS is ready to use.")
            print("=" * 60)
            sys.exit(0)
        else:
            print("=" * 60)
            print("API test failed. Please check the errors above.")
            print("=" * 60)
            sys.exit(1)
    else:
        print("=" * 60)
        print("Import test passed!")
        print("=" * 60)
        print("\nTo test the API connection, run:")
        print("  python test_fishaudio.py <YOUR_API_KEY> <REFERENCE_VOICE_ID>")
        print("\nExample:")
        print("  python test_fishaudio.py sk-abc123... 7f92f8afb8ec43bf81429cc1c9199cb1")
        print()
        sys.exit(0)
