#!/usr/bin/env python3
"""
Fish Audio TTS Dependency Checker
This script verifies that all required Python packages are installed
"""

import sys
import json


def sdk_details():
    """Best-effort identity of the installed fishaudio SDK.

    Reported only, never enforced: the addon works with any build whose
    ``tts.convert()`` accepts a ``model`` argument, and older builds degrade to
    generic error messages instead of failing.
    """
    details = {"fishaudio_version": None, "model_selectable": None}

    try:
        import importlib.metadata as metadata
        details["fishaudio_version"] = metadata.version("fish-audio-sdk")
    except Exception:
        pass

    if details["fishaudio_version"] is None:
        try:
            import fishaudio
            details["fishaudio_version"] = getattr(fishaudio, "__version__", None)
        except Exception:
            pass

    try:
        import inspect
    except Exception:
        return details

    for class_name in ("AsyncTTSClient", "TTSClient"):
        try:
            module = __import__("fishaudio.resources.tts", fromlist=[class_name])
            convert = getattr(getattr(module, class_name), "convert")
            selectable = "model" in inspect.signature(convert).parameters
        except Exception:
            continue
        details["model_selectable"] = selectable
        break

    return details


def check_dependencies():
    """Check if all required dependencies are installed"""
    results = {
        "success": True,
        "python_version": f"{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}",
        "missing_packages": [],
        "installed_packages": [],
        "errors": []
    }

    required_packages = [
        ("fishaudio", "fish-audio-sdk"),
        ("asyncio", "asyncio (built-in)"),
    ]

    for module_name, package_name in required_packages:
        try:
            __import__(module_name)
            results["installed_packages"].append(package_name)
        except ImportError as e:
            results["success"] = False
            results["missing_packages"].append({
                "package": package_name,
                "module": module_name,
                "error": str(e)
            })

    results.update(sdk_details())
    return results


if __name__ == "__main__":
    try:
        results = check_dependencies()
        print(json.dumps(results, indent=2))
        sys.exit(0 if results["success"] else 1)
    except Exception as e:
        print(json.dumps({
            "success": False,
            "error": str(e)
        }), file=sys.stderr)
        sys.exit(1)
