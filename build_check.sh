#!/usr/bin/env bash
set -e

# ==============================================================================
# Fast C# Compilation Checker for Unity (Roslyn csc)
# Checks Assembly-CSharp for errors in ~1-2 seconds without opening Unity GUI.
# ==============================================================================

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$PROJECT_DIR"

# 1. Detect Unity Editor version from ProjectVersion.txt
UNITY_VERSION=$(grep 'm_EditorVersion:' ProjectSettings/ProjectVersion.txt | awk '{print $2}')
if [ -z "$UNITY_VERSION" ]; then
    UNITY_VERSION="6000.6.0f1"
fi

DOTNET_PATH="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet"
if [ ! -f "$DOTNET_PATH" ]; then
    # Fallback search for any installed Unity dotnet
    DOTNET_PATH=$(find /Applications/Unity/Hub/Editor -name "dotnet" -type f 2>/dev/null | head -n 1)
fi

if [ -z "$DOTNET_PATH" ] || [ ! -f "$DOTNET_PATH" ]; then
    echo "❌ Error: Could not find Unity .NET SDK at: $DOTNET_PATH"
    echo "Please ensure Unity ${UNITY_VERSION} is installed via Unity Hub."
    exit 1
fi

DOTNET_DIR="$(dirname "$DOTNET_PATH")"
CSC_PATH=$(find "${DOTNET_DIR}/sdk" -name "csc.dll" 2>/dev/null | head -n 1)
if [ -z "$CSC_PATH" ] || [ ! -f "$CSC_PATH" ]; then
    echo "❌ Error: Could not find Roslyn compiler (csc.dll)."
    exit 1
fi

# 2. Find the compiler response files (.rsp)
RSP_FILES=()
for rsp_name in "Assembly-CSharp.rsp" "Assembly-CSharp-Editor.rsp"; do
    found=$(find Library/Bee/artifacts -name "$rsp_name" 2>/dev/null | head -n 1)
    if [ -n "$found" ] && [ -f "$found" ]; then
        RSP_FILES+=("$found")
    fi
done

if [ ${#RSP_FILES[@]} -eq 0 ]; then
    echo "⚠️ Warning: No rsp files found in Library/Bee/artifacts/."
    echo "Please open the project in Unity Editor once to generate compilation artifacts."
    exit 1
fi

# 3. Run Roslyn compile check
echo "🔍 Checking C# compilation using Roslyn (Runtime & Editor)..."
TOTAL_WARNINGS=0
ALL_ERRORS=""

for rsp in "${RSP_FILES[@]}"; do
    TARGET_NAME=$(basename "$rsp" .rsp)
    OUTPUT=$(DOTNET_CLI_HOME=/tmp "$DOTNET_PATH" "$CSC_PATH" @"$rsp" 2>&1)
    STATUS=$?
    if [ $STATUS -ne 0 ]; then
        ALL_ERRORS+=$'\n'"--- Errors in $TARGET_NAME ---"$'\n'"$OUTPUT"
    else
        W_COUNT=$(echo "$OUTPUT" | grep -c "warning CS" || true)
        TOTAL_WARNINGS=$((TOTAL_WARNINGS + W_COUNT))
    fi
done

if [ -z "$ALL_ERRORS" ]; then
    echo "✅ [SUCCESS] 0 compilation errors across all assemblies (${RSP_FILES[*]})!"
    if [ "$TOTAL_WARNINGS" -gt 0 ]; then
        echo "ℹ️  Found $TOTAL_WARNINGS total compiler warning(s)."
    fi
    exit 0
else
    echo "❌ [FAILED] Compilation errors detected:"
    echo "$ALL_ERRORS"
    exit 1
fi

