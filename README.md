# Keel

Keel lets a Valheim mod run without BepInEx, while still getting along with
BepInEx when it's there. A mod built on Keel ships as a single download. Copy
it into your Valheim folder and it runs, or let a mod manager install it and
it loads through BepInEx like any other mod.

## How a game starts with Keel

Valheim can't load mods on its own, so every mod needs something to let it
in. Keel uses Unity Doorstop, the same small tool BepInEx starts from.
Doorstop is the `winhttp.dll` that sits next to `valheim.exe`. Windows loads
it along with the game, and `doorstop_config.ini` beside it names the file to
run before the game begins, which here is `Keel\Keel.dll`.

Unity isn't running that early, so Keel waits for the game's first scene.
Then it looks through the `Keel` folder, where each mod has a folder of its
own named after its DLL, like `Keel\Daywheel\Daywheel.dll`, and starts every
one it finds. It also sets the game's `isModded` flag, as the game's own code
asks every mod to, so the main menu shows that the game is modded.

A game folder only has room for one Doorstop. If BepInEx is installed in the
same folder, Keel's files have taken the place of BepInEx's, so Keel starts
BepInEx first, the way Doorstop does, and BepInEx and its plugins run as they
always did. The first time Keel finds BepInEx, it writes `Keel\Keel.cfg` with
a single switch, `StartBepInEx`. A player who had switched BepInEx off can
keep it off by setting that to `false`, or by putting that one line in
`Keel\Keel.cfg` before the first start. A mod manager works the other way
round: it points Doorstop at BepInEx when it launches the game, and BepInEx
loads Keel mods as ordinary plugins.

Keel also keeps a mod from starting twice. A Keel mod that's already running
won't start again from a second copy, and when BepInEx has a DLL of the same
name in its plugins folder, even an old copy from before Keel, Keel leaves
that mod to BepInEx. If BepInEx fails to start and writes that to its
preloader log beside the game, Keel starts the mod itself instead.

Whenever Keel starts, it writes what it did to `Keel\Keel.log`.

## Building a mod on Keel

A mod keeps a copy of this folder as `keel` at the top of its repository and
compiles `keel/lib/*.cs` into its own DLL. Beyond that it needs three pieces:

- A class that derives from `Keel.Mod`. It gives the mod's id, name and
  version, and its `Start` method sets up the settings and makes whatever the
  mod needs.
- The line `[assembly: Keel.Main(typeof(ThatClass))]`, which is how Keel
  finds that class.
- A small BepInEx plugin whose `Awake` calls
  `Keel.Bep.Start(new ThatClass(), Config, Logger)`, so that BepInEx can start
  the mod too.

`Start` is handed a `Keel.Host`, which is where settings and the log come
from. When BepInEx started the mod, its settings live in BepInEx's config
folder, where mod managers can edit them, and its messages go to BepInEx's
log. When Keel started it, the settings live in `<Name>.cfg` in the mod's own
folder, written in the same format, and the messages go to the game's log.
When a mod has no settings file of its own yet, Keel starts it from the
settings BepInEx kept for it in the same game folder, if there are any, so
switching over loses nothing. The mod itself never has to know which starter
it got.

The BepInEx plugin and Keel's own `lib/Bep.cs` are the only code that touches
BepInEx, and only BepInEx ever calls them. When Keel starts a mod they're
never loaded, so the game never goes looking for BepInEx.

`loader/Keel.csproj` builds `Keel.dll`. Like the mods, it builds against the
game's own DLLs, which aren't included here. Put them in a `refs` folder
beside this one, or pass `-p:RefDir=<folder>`.

## What a mod's zip holds

```
manifest.json, icon.png, README.md, CHANGELOG.md   for Thunderstore
LICENSE                                            the mod's own licence
winhttp.dll                                        Doorstop 4.4.0, unchanged
doorstop_config.ini                                from door/
Keel/Keel.dll
Keel/Keel-LICENSE.txt                              this folder's LICENSE
Keel/Doorstop-LICENSE.txt                          from door/
Keel/Doorstop-4.4.0-source.zip                     Doorstop's source code
Keel/<Name>/<Name>.dll
```

A player installing by hand only needs `winhttp.dll`, `doorstop_config.ini`
and the `Keel` folder; the rest is for Thunderstore and mod managers.

`winhttp.dll` is the 64-bit build from Doorstop's own release,
`doorstop_win_release_4.4.0.zip`, on
[Doorstop's releases page](https://github.com/NeighTools/UnityDoorstop/releases/tag/v4.4.0).
Its SHA-256 is
`93406d0a02e7c164b89828cbfe3b289930a112d2eca50bd4a52e72ece169e6a8`, and it's
byte for byte the file that BepInEx's Valheim pack ships, so copying a mod
over a BepInEx install doesn't change which Doorstop runs. The source zip is
the one GitHub makes for that release's tag.

## Credits and licences

Keel is released under the MIT licence, which is in `LICENSE` and travels in
every mod's zip as `Keel\Keel-LICENSE.txt`.

Keel starts through [Unity Doorstop](https://github.com/NeighTools/UnityDoorstop),
made by NeighTools and released under the GNU Lesser General Public License,
version 2.1. Every mod built on Keel ships Doorstop's `winhttp.dll` exactly as
released, together with that licence (`door/Doorstop-LICENSE.txt`) and
Doorstop's complete source code for the same version.
`door/doorstop_config.ini` is Doorstop's own settings file with the line that
names the file to start changed, and a note at its top saying so.

Keel is built to work alongside [BepInEx](https://github.com/BepInEx/BepInEx),
which is also released under the LGPL 2.1 and isn't included. Keel's settings
files follow BepInEx's config format, so a file reads the same whichever of
the two wrote it, and the BepInEx side of a Keel mod uses BepInEx's own API.

Valheim is made by Iron Gate, and none of its files are included.
