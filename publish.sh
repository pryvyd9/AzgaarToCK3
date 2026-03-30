#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCAL_ONLY=0

for arg in "$@"; do
    case "$arg" in
        --local-only) LOCAL_ONLY=1 ;;
        *) echo "Unknown argument: $arg"; exit 1 ;;
    esac
done

# --- Prerequisites ---
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: 'dotnet' not found on PATH. Install .NET 10 SDK."
    exit 1
fi
if [ $LOCAL_ONLY -eq 0 ]; then
    if ! command -v gh &> /dev/null; then
        echo "ERROR: 'gh' (GitHub CLI) not found. Install from https://cli.github.com"
        exit 1
    fi
    if ! gh auth status > /dev/null 2>&1; then
        echo "ERROR: Not logged into GitHub CLI. Run 'gh auth login' first."
        exit 1
    fi
fi

# --- Extract version from changelog.md ---
VERSION=""
while IFS= read -r line; do
    if [[ "$line" =~ ^#[[:space:]]*([0-9]+\.[0-9]+\.[0-9]+)[[:space:]]*$ ]]; then
        VERSION="${BASH_REMATCH[1]}"
        break
    fi
done < "$SCRIPT_DIR/changelog.md"

if [ -z "$VERSION" ]; then
    echo "ERROR: Could not find version heading in changelog.md (expected '# X.Y.Z')."
    exit 1
fi
echo "Publishing version $VERSION"

if [ $LOCAL_ONLY -eq 0 ]; then
    # --- Safety: existing git tag ---
    if git -C "$SCRIPT_DIR" tag -l "$VERSION" | grep -q .; then
        echo "ERROR: Git tag '$VERSION' already exists. Bump the version in changelog.md before publishing."
        exit 1
    fi

    # --- Safety: existing GitHub release ---
    if gh release view "$VERSION" > /dev/null 2>&1; then
        echo "ERROR: GitHub release '$VERSION' already exists."
        exit 1
    fi

    # --- Create and push git tag ---
    echo "Creating tag $VERSION..."
    git -C "$SCRIPT_DIR" tag "$VERSION"
    git -C "$SCRIPT_DIR" push origin "$VERSION"
fi

# --- Build output directory ---
OUT="$SCRIPT_DIR/publish_tmp"
rm -rf "$OUT"
PROJ="$SCRIPT_DIR/ConsoleUI/ConsoleUI.csproj"
COMMON_ARGS="-c Release /p:DebugType=None /p:DebugSymbols=false /p:CopyOutputSymbolsToPublishDirectory=false --self-contained true"

# --- dotnet publish ---
echo "Publishing win-x64..."
# shellcheck disable=SC2086
dotnet publish "$PROJ" -r win-x64 $COMMON_ARGS \
    /p:PublishAot=true \
    /p:PublishReadyToRun=true \
    /p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$OUT/win-x64"

echo "Publishing osx-x64..."
# shellcheck disable=SC2086
dotnet publish "$PROJ" -r osx-x64 $COMMON_ARGS \
    /p:PublishSingleFile=true \
    /p:PublishTrimmed=true \
    /p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$OUT/osx-x64"

echo "Publishing linux-x64..."
# shellcheck disable=SC2086
dotnet publish "$PROJ" -r linux-x64 $COMMON_ARGS \
    /p:PublishSingleFile=true \
    /p:PublishTrimmed=true \
    /p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$OUT/linux-x64"

# --- Create ZIP archives ---
ZIPS=()
for RID in win-x64 osx-x64 linux-x64; do
    ZIP="$SCRIPT_DIR/AzgaarToCK3_${VERSION}_${RID}.zip"
    echo "Creating $ZIP..."
    rm -f "$ZIP"
    (cd "$OUT/$RID" && zip -r "$ZIP" .)
    zip -j "$ZIP" "$SCRIPT_DIR/Readme.md"
    ZIPS+=("$ZIP")
done

if [ $LOCAL_ONLY -eq 0 ]; then
    # --- Extract release notes ---
    NOTES=""
    IN_SECTION=0
    while IFS= read -r line; do
        if [[ "$line" =~ ^#[[:space:]]*${VERSION}[[:space:]]*$ ]]; then
            IN_SECTION=1
            continue
        fi
        if [[ $IN_SECTION -eq 1 ]] && [[ "$line" =~ ^#[[:space:]]*[0-9] ]]; then
            break
        fi
        if [[ $IN_SECTION -eq 1 ]]; then
            NOTES+="$line"$'\n'
        fi
    done < "$SCRIPT_DIR/changelog.md"
    NOTES="${NOTES#"${NOTES%%[! $'\t'$'\n']*}"}"  # trim leading whitespace
    NOTES="${NOTES%"${NOTES##*[! $'\t'$'\n']}"}"  # trim trailing whitespace

    # --- Create GitHub release ---
    echo "Creating GitHub release $VERSION..."
    gh release create "$VERSION" "${ZIPS[@]}" \
        --title "AzgaarToCK3 $VERSION" \
        --notes "$NOTES"
fi

# --- Cleanup ---
rm -rf "$OUT"

if [ $LOCAL_ONLY -eq 1 ]; then
    echo "Done. ZIPs created locally (no GitHub release):"
    for ZIP in "${ZIPS[@]}"; do echo "  $ZIP"; done
else
    echo "Done. Released $VERSION to GitHub."
fi
