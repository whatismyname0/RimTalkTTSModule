#!/usr/bin/env python3
"""
Fish Audio TTS Dependency Checker
This script verifies that all required Python packages are installed
"""

import sys
import json

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
