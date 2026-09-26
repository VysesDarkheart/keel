# Keel

Keel lets a Valheim mod run without BepInEx. A mod built on Keel ships as
one zip. Copy its files into your Valheim folder and play, with no BepInEx
needed. Or let a mod manager install it, and it loads through BepInEx like any
other mod. On Linux and a Mac, a copy installed by hand also needs one launch
option in Steam.

## What Keel does

- It starts mods without BepInEx, from the mod's own zip, so a player
  downloads one thing.
- It gets along with BepInEx. A mod manager loads a Keel mod as an ordinary
  BepInEx plugin. A Keel mod copied into a game folder that has BepInEx
  starts BepInEx first, so every BepInEx plugin keeps running.
- It starts each mod once, however many ways it could start, and leaves a
  mod to BepInEx when BepInEx has its own copy.
- It keeps each mod's settings in a file laid out the way BepInEx lays out
  its own. The first time it starts a mod, it copies the settings BepInEx
  kept for that mod in the same game folder, so moving from BepInEx to Keel
  loses nothing.
- For mods installed by hand, it runs one shared core for all of them, the
  newest in the game. So a fix to Keel's core reaches every Keel 2 mod
  installed by hand as soon as any one of them brings it.
- It tells the game it's modded, as the game asks every mod to, so the main
  menu says so and achievements are off. It writes what it did to a log.

## Installing a Keel mod by hand

With a mod manager there's nothing extra to do: install the mod as usual,
and the manager loads it through BepInEx. The steps below are for installing
by hand, and they're the same for every Keel mod.

### On Windows

1. Download the mod's zip and open it.
2. Open your Valheim folder. In Steam, right-click Valheim and choose
   **Manage**, then **Browse local files**. It's the folder with
   `valheim.exe` in it.
3. Copy these three from the zip into that folder: `winhttp.dll`,
   `doorstop_config.ini` and the `Keel` folder.
4. Start Valheim from Steam as usual.

The main menu now says the game is modded. `Keel\Keel.log` in the Valheim
folder says what Keel started, and why if something didn't.

### On Linux or the Steam Deck

1. Do steps 1 to 3 above. On the Steam Deck, switch to Desktop Mode first.
   The Valheim folder is the one with `valheim.x86_64` in it, or
   `valheim.exe` if you play the Windows version through Proton.
2. In Steam, right-click Valheim and choose **Properties**. In **Launch
   Options**, enter exactly this line:

   ```
   sh ./Keel/run.sh %command%
   ```

3. Start Valheim from Steam.

Use the same line if you play the Windows version of Valheim through Proton.
So far this has only run on Valheim's Linux dedicated server, not yet in the
game through Steam, on a Steam Deck or through Proton.

### On a Mac

1. Do steps 1 to 3 of the Windows steps. The Valheim folder is the one with
   `valheim.app` in it.
2. On a Mac with Apple Silicon, install Rosetta once. Open Terminal and run:

   ```
   softwareupdate --install-rosetta --agree-to-license
   ```

   The launch line in step 5 runs the game through Rosetta. Without Rosetta
   the game still starts, but natively, and Keel and any BepInEx mods don't.
   `Keel/Keel.log` then says why. Upgrading to macOS 27 removes Rosetta, so
   install it again after that upgrade.
3. Let macOS load the files you downloaded. In Terminal, type
   `xattr -dr com.apple.quarantine ` with a space at the end, drag the `Keel`
   folder in your Valheim folder onto the Terminal window, and press Return. Or, once the first
   start is blocked, allow `libdoorstop.dylib` in **System Settings**,
   **Privacy & Security**, and start again.
4. Copy the full path of `run.sh`. In Finder, open the `Keel` folder in your Valheim folder, hold
   **Option**, right-click `run.sh` and choose **Copy "run.sh" as Pathname**.
5. In Steam, right-click Valheim and choose **Properties**. In **Launch
   Options**, type `sh "`, paste the path, and type `" %command%` after it.
   It should look like this:

   ```
   sh "/Users/<name>/Library/Application Support/Steam/steamapps/common/Valheim/Keel/run.sh" %command%
   ```

6. Start Valheim from Steam.

The small library that starts Keel on a Mac, `libdoorstop.dylib`, is built
for macOS 15.5 and later, so an older macOS may not load it. No real Mac has run Keel yet: what
it does on a Mac has only run against stand-ins for a Mac's tools.

### Beside BepInEx or a mod manager

If you installed BepInEx by hand in the same Valheim folder, Keel starts it
first, so BepInEx and its plugins run as they would without Keel.
On Windows, the zip's `winhttp.dll` and `doorstop_config.ini` take the
place of BepInEx's own, so choose to replace them when Windows asks. Keep the
`Keel` folder from then on, because Keel is what starts BepInEx now. If you
had switched BepInEx off and want it to stay off, open `Keel\Keel.cfg` and
set `StartBepInEx = false`. If that file isn't there yet, create it with just
that one line before you start the game.

If you use a mod manager, install Keel mods through it too. Each time it
starts the game, it puts its own startup files back into the Valheim folder,
so a copy installed by hand wouldn't start. On Linux a mod manager asks for a
launch option of its own, so use one or the other. Keel's line should also
let an r2modman installed the usual way start the game with its mods, though
not the Flatpak one, but that hasn't been tested yet. No common mod manager
runs on a Mac.

### Updating

Copy the new zip's files in again, over the old ones. Keel keeps settings
inside the `Keel` folder, so merge the new `Keel` folder into the old one
rather than deleting the old one first.

- On Windows, copying the new `Keel` folder over the old one merges them. If
  Windows asks, choose to replace the files.
- On Linux, if your file manager asks, choose to merge the folders (**Write
  Into** in Dolphin, the Steam Deck's file manager) and to replace the files.
- On a Mac, don't use Finder's Replace, which deletes your settings. In
  Terminal, type `ditto ` with a space at the end, drag in the new `Keel`
  folder, then the `Keel` folder in your Valheim folder, and press Return.
  Then do step 3 of the Mac steps again.

### Taking Keel out

1. On Linux or a Mac, clear Valheim's launch option in Steam.
2. Delete `winhttp.dll`, `doorstop_config.ini` and the `Keel` folder from the
   Valheim folder.
3. If you installed BepInEx by hand there too, copy its own `winhttp.dll` and
   `doorstop_config.ini` back in from its download, or on Linux and a Mac set
   its own launch option again. Otherwise BepInEx won't start either.

## Building a mod on Keel

1. Copy Keel into your repository as a folder named `keel`: its `lib`,
   `loader`, `core` and `door` folders, `LICENSE` and this readme.
2. Compile `keel/lib/*.cs` into your mod's own DLL.
3. Add three small pieces:
   - A class that derives from `Keel.Mod`. It gives the mod's id, name and
     version, and its `Start` method sets up the settings and makes whatever
     the mod needs.
   - The line `[assembly: Keel.Main(typeof(ThatClass))]`, which is how Keel
     finds that class.
   - A small BepInEx plugin whose `Awake` calls
     `Keel.Bep.Start(new ThatClass(), Config, Logger)`, so BepInEx can start
     the mod too. Give it the mod's id as its GUID: BepInEx names the mod's
     settings file after its GUID, and that's how Keel finds the settings
     BepInEx kept.

A whole mod can be this small:

```csharp
// Main.cs
[assembly: Keel.Main(typeof(MyMod.Main))]

namespace MyMod
{
    internal sealed class Main : Keel.Mod
    {
        public const string Guid = "com.example.mymod";

        internal static Keel.Setting<bool> Show;

        internal override string Id { get { return Guid; } }
        internal override string Name { get { return "MyMod"; } }
        internal override string Version { get { return "1.0.0"; } }

        internal override void Start(Keel.Host host)
        {
            Show = host.Bind("General", "Show", true, "Turns it on or off.");
            host.Log.Info(Name + " " + Version + " started by " + host.Starter + ".");
        }
    }
}
```

```csharp
// Plugin.cs
using BepInEx;

namespace MyMod
{
    [BepInPlugin(Main.Guid, "MyMod", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Keel.Bep.Start(new Main(), Config, Logger);
        }
    }
}
```

`Start` is handed a `Keel.Host`, which is where settings and the log come
from. When BepInEx started the mod, its settings live in BepInEx's config
folder, where mod managers can edit them, and its messages go to BepInEx's
log. When Keel started it, the settings live in the mod's own folder, in a
file named after the mod's `Name`, such as `Keel\MyMod\MyMod.cfg`, and the
messages go to the game's log. The mod itself never has to know which
starter it got.

The BepInEx plugin and Keel's own `lib/Bep.cs` are the only code that
touches BepInEx, and only BepInEx ever calls them. When Keel starts a mod
they're never loaded, so the game never goes looking for BepInEx.

To build, `loader/Keel.csproj` makes `Keel.dll`, the starter, which needs
nothing from the game, and `core/Keel.Core.csproj` makes the core, which
ships as `Keel\Keel.Core.<version>.dll`. The core and the mods build against
the game's own DLLs, which aren't included here. Put them in a `refs` folder
beside this one, or pass `-p:RefDir=<folder>`.

When you pack your zip, take `Keel.dll` and the core from any Keel 2 mod's
zip rather than your own builds. Every Keel 2 zip carries the very same
starter, and for each released version the very same core, while a build
with a different compiler can come out different. Never give a core you've
changed a version number Keel has used. The zip's layout is under "What a
mod's zip holds" below.

## How Keel works

### Starting with the game

Valheim can't load mods on its own, so every mod needs something to let it
in. Keel uses Unity Doorstop, the same small tool BepInEx starts from.

On Windows, Doorstop is the `winhttp.dll` beside `valheim.exe`. Windows loads
it along with the game, and `doorstop_config.ini` beside it names the file to
run before the game begins, which here is `Keel\Keel.dll`. On Linux and a
Mac, Doorstop is a library the game has to be started with:
`Keel/libdoorstop.so` on Linux, and `Keel/libdoorstop.dylib` on a Mac.
`Keel/run.sh`, Keel's copy of Doorstop's own launch script, starts the game
with it. Steam puts the game's own command where `%command%` is, and on Linux
starts it from the game folder. For the Windows game run through Proton, the script
tells Wine, which Proton is built on, to load Doorstop's `winhttp.dll` from
the game folder, so from there Keel starts just as it does on Windows. On
Apple Silicon it hands Doorstop's library to the game through `arch`, as
Doorstop's own `run.sh` does, and adds a Rosetta check of Keel's own.

`Keel.dll` is a small starter. It takes the game folder to be the folder that
holds the `Keel` folder, on every system, since on a Mac the game's program
sits inside `valheim.app`. If BepInEx is installed in that game folder, the
starter starts it first, the way Doorstop does, before any core runs, so
BepInEx starts even when no core can. Then it looks in the `Keel` folder for
Keel's core, a file named after its version, like `Keel.Core.2.0.0.dll`, and
hands over to the newest one there. It only takes a name of exactly that
shape, whose file really is the version the name says. If the newest can't
be read or loaded, it tries the next one down. Everything else is the core's
work.

Unity isn't running that early, so the core waits for the game's first
scene. Then it looks through the `Keel` folder, where each mod has a folder
of its own named after its DLL, like `Keel\Daywheel\Daywheel.dll`, and starts
every one it finds. It also sets the game's `isModded` flag, so the main menu
shows that the game is modded and achievements are off.

A mod manager works the other way round. It points Doorstop at BepInEx when
it launches the game, and BepInEx loads Keel mods as ordinary plugins, while
Keel's own copy of Doorstop sits unused in the mod's folder.

### One core for every mod installed by hand

Every Keel 2 mod carries the whole of Keel in its zip, but a game where mods
are installed by hand only ever runs one core, the newest one in the `Keel`
folder. Each core's file is named after its version, so installing a mod
that brings an older core adds that file beside the newer one, and the newer
core still runs. When Keel's core is fixed, every Keel 2 mod installed by
hand gets the fix as soon as any one of them brings the new core, updated or
not.

Each mod keeps only a small part of Keel inside it: the part BepInEx starts
it through, and the part that finds the core. A change to that part reaches
a mod only when the mod itself is updated. Under a mod manager, BepInEx is
the shared piece instead: it starts every Keel mod through that part, and the
core isn't used at all. The mod and the core talk only through types every
.NET library has, never a shared Keel DLL, so there's no second copy of
anything to clash. Each new core keeps everything the older ones offered, so
a mod built on any Keel 2 works with every later core. Mods built on Keel 1.0
keep working too, on the copy of Keel they carry, with its own settings code.

### The starter never changes

The starter, `Keel.dll`, never changes after Keel 2.0.0. Every Keel 2 zip
carries it byte for byte, so copying one Keel 2 mod over another never swaps
it. A mod built on Keel 1.0 brings Keel 1.0's own `Keel.dll`, which still
starts Keel 2 mods, until the next Keel 2 mod copied in brings the starter
back. A released core is one file for good too. If a fault is ever found in
the starter, a later release's `doorstop_config.ini` and `run.sh` name a new
starter file beside it, and `Keel.dll` itself still never changes.

Doorstop's files, the door the game starts Keel through, aren't frozen:
`winhttp.dll`, `doorstop_config.ini`, `run.sh`, `libdoorstop.so` and
`libdoorstop.dylib` follow Doorstop's own releases. Each Keel release ships
the newest Doorstop that still starts the starter the same way, and a new
Doorstop's `run.sh` is merged into Keel's copy, never dropped in. A player
who copies an older mod in last brings back that mod's older door, `run.sh`
included, with any fault it had, and every Doorstop 4 still starts the same
starter.

### Keel's own settings, and when a core is passed over

Keel keeps two settings of its own in `Keel\Keel.cfg`, which it writes the
first time it runs. `StartBepInEx` lets a player who had switched BepInEx off
keep it off. `SkipCores` lists versions of the core to pass over, such as
`2.1.0`, with commas between them.

Keel adds a core to `SkipCores` itself when the core loads but can't run,
since the game won't load a second core in the same start. It does the same
when a core fails as Keel hands over to it, or when the game stops twice in
a row during that hand-over, before the game loads. Keel learns that last one from
`Keel\Keel.starting`, a file it writes just before the hand-over and deletes
once it's done. The file names the game that wrote it, so games started
together from one folder, such as several servers run from one install,
don't take each other's hand-over for a stop. A core that stops the game
later, once the game is loading, isn't caught this way; add its version to
`SkipCores` yourself. Each time Keel adds a core, it adds a line at the end
of `Keel.cfg` with a note saying when and why, so the next start passes over
that core, and it never changes a line already there.

Keel also keeps a mod from starting twice. A Keel mod that's already running
won't start again from a second copy, and when BepInEx has a DLL of the same
name in its plugins folder, even an old copy from before Keel, Keel leaves
that mod to BepInEx. If BepInEx fails to start and writes that to its
preloader log, in the game folder or beside the game's program, Keel starts
the mod itself instead.

Whenever Keel starts, it writes what it did to `Keel\Keel.log`, and keeps
the log from the start before as `Keel\Keel.previous.log`.

## Files and hashes

The SHA-256 hashes of this release's files:

```
Keel.dll              3d550e1c92b2df366a0b518e7a2e075b8c83a2ae82d1749cc77cc3a77281bdbb
doorstop_config.ini   4cb9c011c370c6c6042002995cef60b7d37fed1288a9db8b771efab512903b24
winhttp.dll           8c6cdbc38836dee87e3368f5de1994d7c0ccebf29e4ce7aba3c0981f9375412c
run.sh                7b7b5186ec986dbf4a91003d2dbcfd927b7692ba4d40edbc7424b285ab303ce8
libdoorstop.so        07ec6ee28c7d200c000ba9db6dfecec466a6ed449194a37decb719bf5321aee0
libdoorstop.dylib     cb4aaa97bd9a08178ac2d165b33284b744d18498d5dec5a07fc2b6f6d87d80b9
Keel.Core.2.0.0.dll   db3d651e6f41317e9444fa7e0e393bd17ceaee26d4f2f62fe3f765a0d866cdf1
```

`Keel.log` names the core that ran and the SHA-256 that core gives for itself.
A changed core could give any hash, so to be sure, hash the file itself and
check it against this list.

### What a mod's zip holds

```
manifest.json, icon.png, README.md, CHANGELOG.md   for Thunderstore
LICENSE                                            the mod's own licence
winhttp.dll                                        Doorstop 4.5.0 for Windows, unchanged
doorstop_config.ini                                from door/
Keel/Keel.dll                                      the starter
Keel/Keel.Core.<version>.dll                       the core
Keel/run.sh                                        from door/, for Linux and a Mac
Keel/libdoorstop.so                                Doorstop 4.5.0 for Linux, unchanged
Keel/libdoorstop.dylib                             Doorstop 4.5.0 for a Mac, unchanged
Keel/Keel-LICENSE.txt                              this folder's LICENSE
Keel/Doorstop-LICENSE.txt                          from door/
Keel/Doorstop-4.5.0-source.zip                     Doorstop's source code
Keel/<Name>/<Name>.dll
```

A player installing by hand copies `winhttp.dll`, `doorstop_config.ini` and
the `Keel` folder. The rest is for Thunderstore and mod managers.

`winhttp.dll`, `libdoorstop.so` and `libdoorstop.dylib` are Doorstop 4.5.0's
own files, unchanged, from its releases on
[Doorstop's releases page](https://github.com/NeighTools/UnityDoorstop/releases/tag/v4.5.0):
the 64-bit `winhttp.dll` from `doorstop_win_release_4.5.0.zip`, the 64-bit
`libdoorstop.so` from `doorstop_linux_release_4.5.0.zip`, and
`libdoorstop.dylib`, one file for Intel and Apple Silicon Macs alike, from
`doorstop_macos_release_4.5.0.zip`. The source zip is the one GitHub makes for
that release's tag. `run.sh` is Keel's copy of the `run.sh` in those
releases, with seven changes:

- It starts `Keel.dll`, which sits beside it in the `Keel` folder.
- It finds the game's program from the folder it's started in.
- Under Steam it runs itself again through `sh`, so it never needs permission
  to run as a program of its own, which a file copied out of a zip may not
  have.
- It needs no `file` tool, the small program Doorstop's own script asks
  whether the game is 32-bit or 64-bit.
- For the Windows game run through Proton or Wine, it only tells Wine to load
  Doorstop's `winhttp.dll` from the game folder, then starts the game.
- On a Mac it passes Doorstop's library by its full path and keeps Steam's
  own libraries. On Apple Silicon it runs the game through Rosetta when it's
  installed, unless the player set `ARCHPREFERENCE`, which `arch` then gets
  as it is. Without Rosetta, and with no `ARCHPREFERENCE` set, the game runs
  natively, where Keel can't start, and `Keel.log` says why.
- On Linux it adds no empty entries to `LD_LIBRARY_PATH`.

## Credits and licences

Copyright 2026 VysesDarkheart. Keel is released under the Mozilla Public
License 2.0, whose full text is in `LICENSE` and travels in every mod's zip as
`Keel\Keel-LICENSE.txt`:

> This Source Code Form is subject to the terms of the Mozilla Public
> License, v. 2.0. If a copy of the MPL was not distributed with this file,
> You can obtain one at https://mozilla.org/MPL/2.0/.

That notice covers the files that make up Keel: everything in this folder
and the folders below it, except `LICENSE` itself, which is Mozilla's text,
and the files in `door`, which are Doorstop's. Keel 1.0.0 was released under
the MIT licence.

A mod built on Keel can have any licence of its own. When it ships Keel's
files, it keeps Keel's licence with them and says where Keel's source is, as
a link to this repository does. If it changes any of Keel's files, it makes
those changed files available under the Mozilla Public License 2.0 too.

Keel starts through [Unity Doorstop](https://github.com/NeighTools/UnityDoorstop),
made by NeighTools and released under the GNU Lesser General Public License,
version 2.1. Every mod built on Keel 2 ships Doorstop's `winhttp.dll`,
`libdoorstop.so` and `libdoorstop.dylib` exactly as released, together with
that licence (`door/Doorstop-LICENSE.txt`) and Doorstop's complete source code
for the same version. `door/doorstop_config.ini` is Doorstop's own settings
file with the line that names the file to start changed, and `door/run.sh` is
Doorstop's own launch script with the changes listed above. Each has a note
at its top saying so.

Keel is built to work alongside
[BepInEx 5](https://github.com/BepInEx/BepInEx/tree/v5-lts), which is
released under the MIT licence and isn't included. Keel's settings files
follow BepInEx's config format, so each of the two reads a file the other
wrote the way it reads its own, and the BepInEx side of a Keel mod uses
BepInEx's own API.

Valheim is made by Iron Gate, and none of its files are included.
