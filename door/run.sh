#!/bin/sh
# Keel's copy of Unity Doorstop 4.5.0's run.sh, changed in September 2026.
# Doorstop is licensed under the GNU LGPL 2.1, which is in
# Keel/Doorstop-LICENSE.txt, and its source is in Keel/Doorstop-4.5.0-source.zip.
#
# What's changed from Doorstop's own:
# - It starts Keel.dll, which sits beside it in the Keel folder.
# - It finds the game's program from the folder it's started in, not from the
#   Keel folder.
# - Under Steam it runs itself again through sh, so it never has to be marked
#   runnable.
# - It doesn't need the file tool: Valheim is 64-bit, and Keel ships Doorstop's
#   64-bit Linux library and its Mac library for both kinds of chip.
# - For the Windows game run through Proton or Wine, it only tells Wine to load
#   Doorstop's winhttp.dll from the game folder, then starts the game.
# - On a Mac it passes Doorstop's library by its full path and keeps Steam's
#   own libraries. On Apple Silicon it runs the game through Rosetta when it's
#   installed, unless the player set ARCHPREFERENCE, which arch then gets as
#   it is. Without Rosetta, and with no ARCHPREFERENCE set, the game runs
#   natively, where Keel can't start, and Keel.log says why.
# - On Linux it adds no empty entries to LD_LIBRARY_PATH.
#
# In Steam, set Valheim's launch options to:
#
#   sh ./Keel/run.sh %command%
#
# On a Mac, use run.sh's full path instead, in quotes:
#
#   sh "/Users/<name>/Library/Application Support/Steam/steamapps/common/Valheim/Keel/run.sh" %command%
#
# Or from the game folder: sh ./Keel/run.sh <the game's program> [doorstop arguments] [game arguments]

# LINUX: name of Unity executable
# MACOS: name of the .app directory
executable_name=""

# All of the below can be overriden with command line args

# General Config Options

# Enable Doorstop?
# 0 is false, 1 is true
enabled="1"

# Path to the assembly to load and execute, from this script's folder
# NOTE: The entrypoint must be of format `static void Doorstop.Entrypoint.Start()`
target_assembly="Keel.dll"

# Overrides the default boot.config file path
boot_config_override=

# If enabled, DOORSTOP_DISABLE env var value is ignored
# USE THIS ONLY WHEN ASKED TO OR YOU KNOW WHAT THIS MEANS
ignore_disable_switch="0"

# Mono Options

# Overrides default Mono DLL search path
# Sometimes it is needed to instruct Mono to seek its assemblies from a different path
# (e.g. mscorlib is stripped in original game)
# This option causes Mono to seek mscorlib and core libraries from a different folder before Managed
# Original Managed folder is added as a secondary folder in the search path
# To specify multiple paths, separate them with colons (:)
dll_search_path_override=""

# If 1, Mono debugger server will be enabled
debug_enable="0"

# When debug_enabled is 1, specifies the address to use for the debugger server
debug_address="127.0.0.1:10000"

# If 1 and debug_enabled is 1, Mono debugger server will suspend the game execution until a debugger is attached
debug_suspend="0"

# CoreCLR options (IL2CPP)

# Path to coreclr shared library WITHOUT THE EXTENSION that contains the CoreCLR runtime
coreclr_path=""

# Path to the directory containing the managed core libraries for CoreCLR (mscorlib, System, etc.)
corlib_dir=""

################################################################################
# Everything past this point is the actual script
set -e

# This script's own full path, so that under Steam it can run itself again
# through sh
script_path="$(cd "$(dirname "$0")" && pwd -P)/$(basename "$0")"

# The Windows game, run through Proton or Wine: Doorstop's winhttp.dll in the
# game folder starts Keel there, once Wine is told to load it before its own
for a in "$@"; do
    case "$a" in
        *.exe|*.EXE)
            export WINEDLLOVERRIDES="winhttp=n,b${WINEDLLOVERRIDES:+;$WINEDLLOVERRIDES}"
            exec "$@"
        ;;
    esac
done

# Special case: program is launched via Steam on Linux
# In that case rerun the script via their bootstrapper to delay adding Doorstop to LD_PRELOAD
# This is required until https://github.com/NeighTools/UnityDoorstop/issues/88 is resolved
for a in "$@"; do
    if [ "$a" = "SteamLaunch" ]; then
        rotated=0; max=$#
        while [ $rotated -lt $max ]; do
            # Test if argument is prefixed with the value of $PWD
            if [ "$1" != "${1#"${PWD%/}/"}" ]; then
                to_rotate=$(($# - rotated))
                set -- "$@" sh "$script_path"
                while [ $((to_rotate-=1)) -ge 0 ]; do
                    set -- "$@" "$1"
                    shift
                done
                exec "$@"
            else
                set -- "$@" "$1"
                shift
                rotated=$((rotated+1))
            fi
        done
        echo "Could not determine game executable launched by Steam" 1>&2
        exit 1
    fi
done

# Handle first param being executable name
if [ -x "$1" ] ; then
    executable_name="$1"
    shift
fi

if [ -z "${executable_name}" ] || [ ! -x "${executable_name}" ]; then
    echo "Please set executable_name to a valid name in a text editor or as the first command line parameter" 1>&2
    exit 1
fi

# Use POSIX-compatible way to get the directory of the executable
a="/$0"; a=${a%/*}; a=${a#/}; a=${a:-.}; BASEDIR=$(cd "$a" || exit; pwd -P)

executable_path=""
lib_extension=""

abs_path() {
    # Resolve relative path to absolute from BASEDIR
    if [ "$1" = "${1#/}" ]; then
        set -- "${BASEDIR}/${1}"
    fi
    echo "$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
}

abs_path_here() {
    # Resolve relative path to absolute from the folder the script was started in
    if [ "$1" = "${1#/}" ]; then
        set -- "${PWD}/${1}"
    fi
    echo "$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
}

# Set executable path and the extension to use for the libdoorstop shared object, and check for Apple Silicon
os_type="$(uname -s)"
case ${os_type} in
    Linux*)
        executable_path="$(abs_path_here "$executable_name")"
        lib_extension="so"
    ;;
    Darwin*)
        real_executable_name="$(abs_path_here "$executable_name")"

        # If it isn't the actual executable, check the .app's Info for it
        case $real_executable_name in
            *.app/Contents/MacOS/*)
                executable_path="${real_executable_name}"
            ;;
            *)
                # Add .app to the end if not given
                if [ "$real_executable_name" = "${real_executable_name%.app}" ]; then
                    real_executable_name="${real_executable_name}.app"
                fi
                inner_executable_name=$(defaults read "${real_executable_name}/Contents/Info" CFBundleExecutable)
                executable_path="${real_executable_name}/Contents/MacOS/${inner_executable_name}"
            ;;
        esac
        lib_extension="dylib"

        # CPUs for Apple Silicon are in the format "Apple M.."
        cpu_type="$(sysctl -n machdep.cpu.brand_string)"
        case "${cpu_type}" in
            Apple*)
                is_apple_silicon=1
            ;;
        esac
    ;;
    *)
        # alright whos running games on freebsd
        echo "Unknown operating system ($(uname -s))" 1>&2
        echo "Make an issue at https://github.com/NeighTools/UnityDoorstop" 1>&2
        exit 1
    ;;
esac

_readlink() {
    # relative links with readlink (without -f) do not preserve the path info
    ab_path="$(abs_path "$1")"
    link="$(readlink "${ab_path}")"
    case $link in
        /*);;
        *) link="$(dirname "$ab_path")/$link";;
    esac
    echo "$link"
}

resolve_executable_path () {
    e_path="$(abs_path "$1")"

    while [ -L "${e_path}" ]; do
        e_path=$(_readlink "${e_path}");
    done
    echo "${e_path}"
}

# Get absolute path of executable
executable_path=$(resolve_executable_path "${executable_path}")

# Doorstop's own script asks the file tool here whether the game is 32-bit or
# 64-bit. Valheim is 64-bit on Linux and on a Mac, so the answer is known.

# Helper to convert common boolean strings into just 0 and 1
doorstop_bool() {
    case "$1" in
        TRUE|true|t|T|1|Y|y|yes)
            echo "1"
        ;;
        FALSE|false|f|F|0|N|n|no)
            echo "0"
        ;;
    esac
}

# Read from command line
i=0; max=$#
while [ $i -lt $max ]; do
    case "$1" in
        --doorstop_enabled) # For backwards compatibility. Renamed to --doorstop-enabled
            enabled="$(doorstop_bool "$2")"
            shift
            i=$((i+1))
        ;;
        --doorstop_target_assembly) # For backwards compatibility. Renamed to --doorstop-target-assembly
            target_assembly="$2"
            shift
            i=$((i+1))
        ;;
        --doorstop-enabled)
            enabled="$(doorstop_bool "$2")"
            shift
            i=$((i+1))
        ;;
        --doorstop-target-assembly)
            target_assembly="$2"
            shift
            i=$((i+1))
        ;;
        --doorstop-boot-config-override)
            boot_config_override="$2"
            shift
            i=$((i+1))
        ;;
        --doorstop-mono-dll-search-path-override)
            dll_search_path_override="$2"
            shift
            i=$((i+1))
        ;;
        --doorstop-mono-debug-enabled)
            debug_enable="$(doorstop_bool "$2")"
            shift
            i=$((i+1))
        ;;
        --doorstop-mono-debug-suspend)
            debug_suspend="$(doorstop_bool "$2")"
            shift
            i=$((i+1))
        ;;
        --doorstop-mono-debug-address)
            debug_address="$2"
            shift
            i=$((i+1))
        ;;
        --doorstop-clr-runtime-coreclr-path)
            coreclr_path="$2"
            shift
            i=$((i+1))
        ;;
        --doorstop-clr-corlib-dir)
            corlib_dir="$2"
            shift
            i=$((i+1))
        ;;
        *)
            set -- "$@" "$1"
        ;;
    esac
    shift
    i=$((i+1))
done

target_assembly="$(abs_path "$target_assembly")"

# Move variables to environment
export DOORSTOP_ENABLED="$enabled"
export DOORSTOP_TARGET_ASSEMBLY="$target_assembly"
export DOORSTOP_BOOT_CONFIG_OVERRIDE="$boot_config_override"
export DOORSTOP_IGNORE_DISABLED_ENV="$ignore_disable_switch"
export DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE="$dll_search_path_override"
export DOORSTOP_MONO_DEBUG_ENABLED="$debug_enable"
export DOORSTOP_MONO_DEBUG_ADDRESS="$debug_address"
export DOORSTOP_MONO_DEBUG_SUSPEND="$debug_suspend"
export DOORSTOP_CLR_RUNTIME_CORECLR_PATH="$coreclr_path.$lib_extension"
export DOORSTOP_CLR_CORLIB_DIR="$corlib_dir"

# Final setup
doorstop_directory="${BASEDIR}/"
doorstop_name="libdoorstop.${lib_extension}"

case ${os_type} in
    Linux*)
        export LD_LIBRARY_PATH="${doorstop_directory}${corlib_dir:+:$corlib_dir}${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
        if [ -z "$LD_PRELOAD" ]; then
            export LD_PRELOAD="${doorstop_name}"
        else
            export LD_PRELOAD="${doorstop_name}:${LD_PRELOAD}"
        fi
        exec "$executable_path" "$@"
    ;;
    Darwin*)
        # sh and arch are system programs, so macOS strips every DYLD_ variable
        # from them. Steam passes its own list (its loader and overlay) again as
        # STEAM_DYLD_INSERT_LIBRARIES, which is kept after Doorstop's library.
        # The library goes in by its full path: it's in the Keel folder, not in
        # the folder the game starts in.
        inherited="${DYLD_INSERT_LIBRARIES:-${STEAM_DYLD_INSERT_LIBRARIES}}"
        dyld_library_path="${doorstop_directory}${DYLD_LIBRARY_PATH:+:$DYLD_LIBRARY_PATH}"
        dyld_insert_libraries="${doorstop_directory}${doorstop_name}${inherited:+:$inherited}"
        if [ -n "${is_apple_silicon}" ]; then
            # Doorstop 4.5.0's library doesn't start anything when Valheim runs
            # natively on Apple Silicon (Doorstop #112), and BepInEx's plugins
            # need Rosetta there too, so the game runs through Rosetta when it's
            # installed, unless the player set ARCHPREFERENCE.
            if [ -z "${ARCHPREFERENCE}" ]; then
                if arch -x86_64 /usr/bin/true 2>/dev/null; then
                    ARCHPREFERENCE="x86_64,arm64"
                else
                    ARCHPREFERENCE="arm64,x86_64"
                    # Natively, nothing starts Keel, so Keel.log says why,
                    # kept the way the starter keeps it: the last one as
                    # Keel.previous.log.
                    {
                        cp -f "${BASEDIR}/Keel.log" "${BASEDIR}/Keel.previous.log"
                        echo "$(date +%H:%M:%S)  Rosetta isn't installed, so Valheim runs natively on this Mac, where neither Keel nor BepInEx can start. To install it, run this in Terminal: softwareupdate --install-rosetta --agree-to-license" > "${BASEDIR}/Keel.log"
                    } 2>/dev/null || true
                fi
            fi
            export ARCHPREFERENCE
            # Never exported: with SIP off, arch itself would load them.
            exec arch -e DYLD_LIBRARY_PATH="${dyld_library_path}" \
                -e DYLD_INSERT_LIBRARIES="${dyld_insert_libraries}" "$executable_path" "$@"
        fi
        export DYLD_LIBRARY_PATH="${dyld_library_path}"
        export DYLD_INSERT_LIBRARIES="${dyld_insert_libraries}"
        exec "$executable_path" "$@"
    ;;
esac
