#!/usr/bin/env python3
"""Fish Audio TTS local bridge server.

The RimTalk TTS C# addon starts this script and talks to it over loopback HTTP.
Because any local process (and any web page) can reach a loopback port, the
server is deliberately locked down:

- binds an OS-assigned ephemeral port and reports it on stdout;
- reads a shared secret from stdin at startup and requires it in the
  ``X-TTS-Token`` header on every request;
- accepts only ``application/json`` bodies of at most 1 MiB and at most
  ``MAX_CONCURRENT_REQUESTS`` concurrent TTS jobs;
- never returns tracebacks or server/Python version banners to the client;
  full tracebacks go to stderr only.
"""

import asyncio
import base64
import hmac
import json
import os
import sys
import time
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler
from threading import BoundedSemaphore, Thread

from fishaudio import AsyncFishAudio
from fishaudio.types import TTSConfig, Prosody

MAX_BODY_BYTES = 1024 * 1024
MAX_CONCURRENT_REQUESTS = 4
TOKEN_HEADER = "X-TTS-Token"
SECURE_MODE_FLAG = "--token-stdin"


def log(message):
    """Write one diagnostic line to stderr (the C# side forwards it to its log)."""
    sys.stderr.write(f"[TTS Server] {message}\n")
    sys.stderr.flush()


class TTSServer(ThreadingHTTPServer):
    """Threading HTTP server carrying the auth token and the request gate."""

    daemon_threads = True
    allow_reuse_address = False

    def __init__(self, server_address, handler_class, auth_token, parent_pid):
        super().__init__(server_address, handler_class)
        self.auth_token = auth_token
        self.request_slots = BoundedSemaphore(MAX_CONCURRENT_REQUESTS)
        self.parent_pid = parent_pid


class TTSRequestHandler(BaseHTTPRequestHandler):
    """HTTP request handler for TTS conversion"""

    # Do not advertise the Python/BaseHTTP versions to clients.
    server_version = "RimTalkTTS"
    sys_version = ""

    def version_string(self):
        return self.server_version

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
            model = request_data.get("model", "s2-pro")
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
            # Full traceback stays on stderr; the client only gets one error line.
            import traceback
            traceback.print_exc(file=sys.stderr)
            sys.stderr.flush()
            return {
                "success": False,
                "error": f"{type(e).__name__}: {e}"[:300]
            }

    # -- request plumbing -------------------------------------------------

    def _send_json(self, status, payload):
        body = json.dumps(payload).encode('utf-8')
        self.send_response(status)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _is_authorized(self):
        expected = self.server.auth_token
        if not expected:
            return False
        provided = self.headers.get(TOKEN_HEADER)
        if not provided:
            return False
        return hmac.compare_digest(
            provided.encode('utf-8', 'ignore'),
            expected.encode('utf-8', 'ignore')
        )

    def _read_request_body(self):
        """Validate headers/size, then parse the JSON body.

        Returns ``(status, data)``; ``status`` is 200 only on success.
        """
        content_type = (self.headers.get('Content-Type') or '').split(';', 1)[0].strip().lower()
        if content_type != 'application/json':
            return 415, None
        try:
            content_length = int(self.headers.get('Content-Length', '0'))
        except (TypeError, ValueError):
            return 400, None
        if content_length <= 0:
            return 400, None
        if content_length > MAX_BODY_BYTES:
            # Reject before reading; the connection is closed after the response.
            return 413, None
        body = self.rfile.read(content_length)
        try:
            return 200, json.loads(body.decode('utf-8'))
        except (UnicodeDecodeError, json.JSONDecodeError):
            return 400, None

    def _run_tts_request(self, request_data):
        loop = asyncio.new_event_loop()
        try:
            asyncio.set_event_loop(loop)
            return loop.run_until_complete(self.handle_tts_request(request_data))
        except Exception as e:
            import traceback
            traceback.print_exc(file=sys.stderr)
            sys.stderr.flush()
            return {"success": False, "error": f"{type(e).__name__}: {e}"[:300]}
        finally:
            try:
                loop.close()
            finally:
                asyncio.set_event_loop(None)

    def do_POST(self):
        """Handle POST requests"""
        if not self._is_authorized():
            log("Rejected request without a valid auth token")
            self._send_json(401, {"success": False, "error": "Unauthorized"})
            return

        status, request_data = self._read_request_body()
        if status != 200:
            messages = {
                400: "Invalid JSON request body",
                413: "Request body too large",
                415: "Content-Type must be application/json",
            }
            self._send_json(status, {"success": False, "error": messages[status]})
            return

        # Shutdown is handled before the concurrency gate so it still works
        # while TTS jobs are in flight.
        if isinstance(request_data, dict) and request_data.get("command") == "shutdown":
            self._send_json(200, {"success": True, "message": "Shutting down"})
            # Shutdown server in a separate thread to avoid blocking
            Thread(target=self.server.shutdown, daemon=True).start()
            return

        if not self.server.request_slots.acquire(blocking=False):
            log("Rejected request: concurrency limit reached")
            self._send_json(429, {"success": False, "error": "Too many concurrent requests"})
            return

        try:
            response = self._run_tts_request(request_data)
            if not isinstance(response, dict):
                response = {"success": False, "error": "Invalid response from TTS handler"}
            self._send_json(200 if response.get("success") else 400, response)
        except (ConnectionAbortedError, ConnectionResetError, BrokenPipeError) as e:
            # Client disconnected, log but don't crash
            log(f"Client disconnected during response send: {e}")
        finally:
            self.server.request_slots.release()

    def log_message(self, format, *args):
        """Suppress HTTP access logs (200 OK responses are too verbose)"""
        # Silently ignore all HTTP request logs
        pass


# --- Windows process liveness helpers ---------------------------------------
# OpenProcess/GetExitCodeProcess alone only prove "a process with this PID
# exists". A recycled PID would keep the helper alive after the game is gone, so
# the process creation time is compared as well.

if os.name == 'nt':
    import ctypes
    from ctypes import wintypes as _wintypes

    _kernel32 = ctypes.windll.kernel32
    _kernel32.OpenProcess.argtypes = [_wintypes.DWORD, _wintypes.BOOL, _wintypes.DWORD]
    _kernel32.OpenProcess.restype = _wintypes.HANDLE
    _kernel32.CloseHandle.argtypes = [_wintypes.HANDLE]
    _kernel32.CloseHandle.restype = _wintypes.BOOL
    _kernel32.GetExitCodeProcess.argtypes = [_wintypes.HANDLE, ctypes.POINTER(_wintypes.DWORD)]
    _kernel32.GetExitCodeProcess.restype = _wintypes.BOOL
    _kernel32.GetProcessTimes.argtypes = [
        _wintypes.HANDLE,
        ctypes.POINTER(_wintypes.FILETIME),
        ctypes.POINTER(_wintypes.FILETIME),
        ctypes.POINTER(_wintypes.FILETIME),
        ctypes.POINTER(_wintypes.FILETIME),
    ]
    _kernel32.GetProcessTimes.restype = _wintypes.BOOL

    PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
    STILL_ACTIVE = 259
else:
    _kernel32 = None


def _windows_process_snapshot(pid):
    """Return ``(alive, creation_time)`` for a PID.

    ``alive`` is False when the process is gone, None when it cannot be
    determined; ``creation_time`` is None when unavailable.
    """
    if _kernel32 is None:
        return None, None

    handle = _kernel32.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
    if not handle:
        return False, None

    try:
        exit_code = _wintypes.DWORD()
        if not _kernel32.GetExitCodeProcess(handle, ctypes.byref(exit_code)):
            return None, None
        if exit_code.value != STILL_ACTIVE:
            return False, None

        creation = _wintypes.FILETIME()
        exit_time = _wintypes.FILETIME()
        kernel_time = _wintypes.FILETIME()
        user_time = _wintypes.FILETIME()
        if _kernel32.GetProcessTimes(
                handle, ctypes.byref(creation), ctypes.byref(exit_time),
                ctypes.byref(kernel_time), ctypes.byref(user_time)):
            creation_time = (creation.dwHighDateTime << 32) | creation.dwLowDateTime
            return True, creation_time
        return True, None
    finally:
        _kernel32.CloseHandle(handle)


def monitor_parent_process(parent_pid, server):
    """Shutdown the server when the parent process exits.

    Backstop for the stdin EOF watch: covers cases where the pipe stays open
    while the parent is gone. PIDs are compared together with the process
    creation time, so a recycled PID cannot keep the helper alive.
    """
    sys.stderr.write(f"[TTS Server] Monitoring parent process PID {parent_pid}\n")
    sys.stderr.flush()

    expected_creation = None
    if os.name == 'nt':
        alive, expected_creation = _windows_process_snapshot(parent_pid)
        if alive is False:
            log(f"Parent process {parent_pid} is already gone, shutting down")
            server.shutdown()
            return

    while True:
        try:
            if os.name == 'nt':
                alive, creation = _windows_process_snapshot(parent_pid)
                if alive is False:
                    log(f"Parent process {parent_pid} has exited, shutting down")
                    server.shutdown()
                    return
                if (alive and expected_creation is not None and creation is not None
                        and creation != expected_creation):
                    log(f"Parent PID {parent_pid} was reused, shutting down")
                    server.shutdown()
                    return
            else:
                # On Unix, use os.kill with signal 0
                os.kill(parent_pid, 0)
        except OSError:
            # Process doesn't exist or we don't have permission
            log(f"Parent process {parent_pid} is gone, shutting down")
            server.shutdown()
            return
        except Exception as e:
            log(f"Error monitoring parent process: {e}")

        # Check every 5 seconds
        time.sleep(5)


def _parse_token_payload(raw):
    """Decode and parse the stdin handshake line, tolerating a leading BOM.

    The .NET Framework Process stdin stream can emit a UTF-8 BOM before the
    payload, and the console encoding on the reader side may vary, so decode
    the raw bytes explicitly and fall back to locating the JSON object.
    """
    if not raw:
        return None

    line = None
    for encoding in ('utf-8-sig', 'utf-8', 'gbk', 'latin-1'):
        try:
            line = raw.decode(encoding).strip()
            break
        except UnicodeDecodeError:
            continue

    if not line:
        return None

    line = line.lstrip('\ufeff').strip()
    data = None
    try:
        data = json.loads(line)
    except json.JSONDecodeError:
        # Last resort: pull the JSON object out of a line with broken mojibake.
        start = line.find('{')
        if start >= 0:
            try:
                data = json.loads(line[start:])
            except json.JSONDecodeError:
                return None

    token = data.get("token") if isinstance(data, dict) else None
    if isinstance(token, str) and token.strip():
        return token.strip()
    return None


def read_auth_token(timeout_seconds=10.0):
    """Read the shared secret from stdin (one JSON line: {"token": "..."}).

    Uses a helper thread so a stuck stdin cannot hang the server forever.
    Returns None when no token arrives or the line is malformed.
    """
    result = {"token": None}

    def _reader():
        try:
            raw = sys.stdin.buffer.readline()
        except Exception:
            # Extremely defensive: fall back to the text wrapper.
            try:
                raw = sys.stdin.readline().encode('utf-8', 'ignore')
            except Exception:
                return
        result["token"] = _parse_token_payload(raw)

    reader = Thread(target=_reader, daemon=True)
    reader.start()
    reader.join(timeout_seconds)
    return result["token"]


def watch_stdin_for_eof(server):
    """Shutdown when the parent closes our stdin pipe.

    The C# client keeps the pipe open for the whole game session. On a forced
    termination (Alt+F4, crash, task manager) Windows closes the write end and
    this thread sees EOF immediately, so the helper exits without waiting for
    the 5-second PID poll.
    """
    try:
        while True:
            chunk = sys.stdin.buffer.read(4096)
            if not chunk:
                break
    except Exception:
        pass

    log("stdin closed (parent exited), shutting down")
    try:
        server.shutdown()
    except Exception:
        pass


def run_server(port, parent_pid, auth_token):
    """Start the TTS HTTP server with threading support for concurrent requests.

    Args:
        port: port to listen on (0 = OS-assigned ephemeral port)
        parent_pid: parent process ID to monitor (optional)
        auth_token: shared secret required in X-TTS-Token (mandatory)
    """
    if not auth_token:
        raise ValueError("auth_token is required")

    httpd = TTSServer(('127.0.0.1', port), TTSRequestHandler, auth_token, parent_pid)

    # Start parent process monitor thread
    if parent_pid is not None:
        Thread(target=monitor_parent_process, args=(parent_pid, httpd), daemon=True).start()

    # Primary abrupt-exit path: the pipe closes as soon as the game process dies.
    Thread(target=watch_stdin_for_eof, args=(httpd,), daemon=True).start()

    actual_port = httpd.server_address[1]
    print(json.dumps({
        "status": "ready",
        "port": actual_port,
        "auth": True,
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


def main():
    if SECURE_MODE_FLAG not in sys.argv:
        sys.stderr.write("[TTS Server] This helper must be started by the RimTalk TTS addon (--token-stdin)\n")
        sys.stderr.flush()
        sys.exit(1)

    parent_pid = None

    # Log startup information (stderr; stdout is reserved for the ready line)
    sys.stderr.write("[TTS Server] Starting Fish Audio TTS Server...\n")
    sys.stderr.write(f"[TTS Server] Python version: {sys.version}\n")
    sys.stderr.write(f"[TTS Server] Python executable: {sys.executable}\n")
    sys.stderr.flush()

    if "--parent-pid" in sys.argv:
        try:
            parent_pid = int(sys.argv[sys.argv.index("--parent-pid") + 1])
        except (IndexError, ValueError):
            sys.stderr.write("[TTS Server] Warning: Invalid parent PID\n")
            sys.stderr.flush()
    sys.stderr.write("[TTS Server] Ephemeral port + auth token\n")
    sys.stderr.flush()

    auth_token = read_auth_token()
    if not auth_token:
        sys.stderr.write("[TTS Server] No auth token received on stdin, refusing to start\n")
        sys.stderr.flush()
        sys.exit(1)

    # Verify fishaudio package is available
    try:
        import fishaudio
        sys.stderr.write(f"[TTS Server] fishaudio package version: {getattr(fishaudio, '__version__', 'unknown')}\n")
        sys.stderr.flush()
    except ImportError:
        sys.stderr.write("[TTS Server] fishaudio package not found. Please install: pip install fish-audio-sdk\n")
        sys.stderr.flush()
        sys.exit(1)

    run_server(0, parent_pid, auth_token)


if __name__ == "__main__":
    main()
