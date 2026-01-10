import asyncio
import json
import base64
import sys
import os
import time
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler
from threading import Thread
from fishaudio import AsyncFishAudio
from fishaudio.types import TTSConfig, Prosody


class TTSRequestHandler(BaseHTTPRequestHandler):
    """HTTP request handler for TTS conversion"""
    
    async def handle_tts_request(self, request_data):
        """
        Convert text to speech asynchronously with timeout protection
        
        Args:
            request_data: Dictionary with api_key, text, reference_id, etc.
        
        Returns:
            Dictionary with success status and audio data or error
        """
        try:
            api_key = request_data.get("api_key")
            text = request_data.get("text")
            reference_id = request_data.get("reference_id")
            model = request_data.get("model", "s1")
            latency = request_data.get("latency", "normal")
            speed = request_data.get("speed", 1.0)
            normalize = request_data.get("normalize", False)
            temperature = request_data.get("temperature", 0.9)
            top_p = request_data.get("top_p", 0.9)
            
            if not api_key or not text or not reference_id:
                return {
                    "success": False,
                    "error": "Missing required parameters: api_key, text, or reference_id"
                }
            
            # Create client with proper base_url to avoid SSL hostname mismatch
            client = AsyncFishAudio(
                api_key=api_key,
                base_url="https://api.fish.audio"
            )
            
            config = TTSConfig(
                prosody=Prosody(speed=float(speed), volume=0),
                reference_id=reference_id,
                format="wav",
                latency=latency,
                normalize=normalize,
                model=model,
                temperature=temperature,
                top_p=top_p
            )
            
            # Convert text to speech with timeout (45 seconds max - increased for slow networks)
            try:
                audio_data = await asyncio.wait_for(
                    client.tts.convert(text=text, config=config),
                    timeout=45.0
                )
            except asyncio.TimeoutError:
                return {
                    "success": False,
                    "error": "TTS generation timed out after 45 seconds. This may be due to slow network or Fish Audio API issues."
                }
            except Exception as api_error:
                # Catch specific API errors
                error_msg = str(api_error)
                if "401" in error_msg or "Unauthorized" in error_msg:
                    return {
                        "success": False,
                        "error": "Invalid API key. Please check your Fish Audio API key."
                    }
                elif "403" in error_msg or "Forbidden" in error_msg:
                    return {
                        "success": False,
                        "error": "Access forbidden. Your API key may not have permission to use this feature."
                    }
                elif "404" in error_msg or "Not Found" in error_msg:
                    return {
                        "success": False,
                        "error": f"Reference voice ID '{reference_id}' not found. Please check the voice ID."
                    }
                elif "429" in error_msg or "Too Many Requests" in error_msg:
                    return {
                        "success": False,
                        "error": "Rate limit exceeded. Please wait a moment and try again."
                    }
                elif "timeout" in error_msg.lower() or "timed out" in error_msg.lower():
                    return {
                        "success": False,
                        "error": f"Network timeout: {error_msg}. Check your internet connection."
                    }
                elif "connection" in error_msg.lower():
                    return {
                        "success": False,
                        "error": f"Connection error: {error_msg}. Check your internet connection and firewall."
                    }
                else:
                    raise  # Re-raise unknown errors to be caught by outer exception handler
            
            # Collect all chunks if it's a stream
            if hasattr(audio_data, 'collect'):
                audio_bytes = audio_data.collect()
            else:
                audio_bytes = audio_data
            
            # Ensure audio_bytes is bytes type
            if not isinstance(audio_bytes, bytes):
                # Try to convert to bytes if it's a different type
                if isinstance(audio_bytes, (list, tuple)):
                    audio_bytes = b''.join(audio_bytes)
                else:
                    return {
                        "success": False,
                        "error": f"Invalid audio data type: {type(audio_bytes).__name__}"
                    }
            
            # Validate audio data
            if len(audio_bytes) == 0:
                return {
                    "success": False,
                    "error": "Audio data is empty"
                }
            
            # Encode audio as base64
            audio_base64 = base64.b64encode(audio_bytes).decode('utf-8')
            
            return {
                "success": True,
                "audio": audio_base64,
                "size": len(audio_bytes)
            }
            
        except Exception as e:
            import traceback
            return {
                "success": False,
                "error": str(e),
                "traceback": traceback.format_exc()
            }
    
    def do_POST(self):
        """Handle POST requests"""
        try:
            # Read request body
            content_length = int(self.headers.get('Content-Length', 0))
            body = self.rfile.read(content_length)
            request_data = json.loads(body.decode('utf-8'))
            
            # Handle shutdown request
            if request_data.get("command") == "shutdown":
                self.send_response(200)
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({"success": True, "message": "Shutting down"}).encode())
                # Shutdown server in a separate thread to avoid blocking
                Thread(target=self.server.shutdown).start()
                return
            
            # Process TTS request asynchronously
            loop = asyncio.new_event_loop()
            asyncio.set_event_loop(loop)
            response = loop.run_until_complete(self.handle_tts_request(request_data))
            loop.close()
            
            # Send response
            try:
                self.send_response(200 if response.get("success") else 400)
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps(response).encode())
            except (ConnectionAbortedError, ConnectionResetError, BrokenPipeError) as e:
                # Client disconnected, log but don't crash
                sys.stderr.write(f"[TTS Server] Client disconnected during response send: {str(e)}\n")
                return
            
        except Exception as e:
            import traceback
            error_response = {
                "success": False,
                "error": str(e),
                "traceback": traceback.format_exc()
            }
            
            try:
                self.send_response(500)
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps(error_response).encode())
            except (ConnectionAbortedError, ConnectionResetError, BrokenPipeError):
                # Client disconnected, log but don't crash
                sys.stderr.write(f"[TTS Server] Client disconnected\n")
    
    def log_message(self, format, *args):
        """Suppress HTTP access logs (200 OK responses are too verbose)"""
        # Silently ignore all HTTP request logs
        pass

def monitor_parent_process(parent_pid, server):
    """Monitor parent process and shutdown server if parent exits"""
    if parent_pid is None:
        return
    
    sys.stderr.write(f"[TTS Server] Monitoring parent process PID {parent_pid}\n")
    sys.stderr.flush()
    
    while True:
        try:
            # Check if parent process is still running
            if os.name == 'nt':  # Windows
                # On Windows, try to open the process
                import ctypes
                kernel32 = ctypes.windll.kernel32
                PROCESS_QUERY_INFORMATION = 0x0400
                handle = kernel32.OpenProcess(PROCESS_QUERY_INFORMATION, False, parent_pid)
                if handle:
                    # Process exists, check exit code
                    exit_code = ctypes.c_ulong()
                    if kernel32.GetExitCodeProcess(handle, ctypes.byref(exit_code)):
                        kernel32.CloseHandle(handle)
                        if exit_code.value != 259:  # 259 = STILL_ACTIVE
                            sys.stderr.write(f"[TTS Server] Parent process {parent_pid} has exited, shutting down\n")
                            sys.stderr.flush()
                            server.shutdown()
                            break
                    else:
                        kernel32.CloseHandle(handle)
                else:
                    # Failed to open process, assume it's dead
                    sys.stderr.write(f"[TTS Server] Parent process {parent_pid} is not accessible, shutting down\n")
                    sys.stderr.flush()
                    server.shutdown()
                    break
            else:  # Unix/Linux
                # On Unix, use os.kill with signal 0
                os.kill(parent_pid, 0)
        except (OSError, AttributeError) as e:
            # Process doesn't exist or we don't have permission
            sys.stderr.write(f"[TTS Server] Parent process {parent_pid} is gone, shutting down\n")
            sys.stderr.flush()
            server.shutdown()
            break
        except Exception as e:
            sys.stderr.write(f"[TTS Server] Error monitoring parent process: {e}\n")
            sys.stderr.flush()
        
        # Check every 5 seconds
        time.sleep(5)

def run_server(port=5678, parent_pid=None):
    """
    Start the TTS HTTP server with threading support for concurrent requests
    
    Args:
        port: Port number to listen on (default: 5678)
        parent_pid: Parent process ID to monitor (optional)
    """
    server_address = ('127.0.0.1', port)
    httpd = ThreadingHTTPServer(server_address, TTSRequestHandler)
    
    # Start parent process monitor thread
    if parent_pid is not None:
        monitor_thread = Thread(target=monitor_parent_process, args=(parent_pid, httpd), daemon=True)
        monitor_thread.start()
    
    print(json.dumps({
        "status": "ready",
        "port": port,
        "message": "Fish Audio TTS Server started (threading mode)"
    }), flush=True)
    
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        sys.stderr.write("[TTS Server] Keyboard interrupt received\n")
        sys.stderr.flush()
    except Exception as e:
        sys.stderr.write(f"[TTS Server] Server error: {e}\n")
        sys.stderr.flush()
    finally:
        sys.stderr.write("[TTS Server] Cleaning up...\n")
        sys.stderr.flush()
        httpd.server_close()
        print(json.dumps({
            "status": "stopped",
            "message": "Fish Audio TTS Server stopped"
        }), file=sys.stderr, flush=True)


if __name__ == "__main__":
    # Get port and parent PID from command line arguments
    port = 5678
    parent_pid = None
    
    # Log startup information
    sys.stderr.write("[TTS Server] Starting Fish Audio TTS Server...\n")
    sys.stderr.write(f"[TTS Server] Python version: {sys.version}\n")
    sys.stderr.write(f"[TTS Server] Python executable: {sys.executable}\n")
    sys.stderr.flush()
    
    if len(sys.argv) > 1:
        try:
            port = int(sys.argv[1])
            sys.stderr.write(f"[TTS Server] Using port: {port}\n")
            sys.stderr.flush()
        except ValueError:
            print(json.dumps({
                "status": "error",
                "error": f"Invalid port number: {sys.argv[1]}"
            }), file=sys.stderr)
            sys.exit(1)
    
    if len(sys.argv) > 2:
        try:
            parent_pid = int(sys.argv[2])
            sys.stderr.write(f"[TTS Server] Parent process PID: {parent_pid}\n")
            sys.stderr.flush()
        except ValueError:
            sys.stderr.write(f"[TTS Server] Warning: Invalid parent PID: {sys.argv[2]}\n")
            sys.stderr.flush()
    
    # Verify fishaudio package is available
    try:
        import fishaudio
        sys.stderr.write(f"[TTS Server] fishaudio package version: {getattr(fishaudio, '__version__', 'unknown')}\n")
        sys.stderr.flush()
    except ImportError as e:
        print(json.dumps({
            "status": "error",
            "error": "fishaudio package not found. Please install: pip install fish-audio-sdk"
        }), file=sys.stderr)
        sys.exit(1)
    
    sys.stderr.write(f"[TTS Server] Starting HTTP server on 127.0.0.1:{port}\n")
    sys.stderr.flush()
    
    run_server(port, parent_pid)
