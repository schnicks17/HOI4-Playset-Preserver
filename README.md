<div align="center">

# 🛡️ HOI4 Playset Preserver

### Freeze your Hearts of Iron IV playset before the next patch breaks it.

[![License: GPL v3](https://img.shields.io/badge/license-GPLv3-blue.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6)
![Game](https://img.shields.io/badge/Hearts%20of%20Iron%20IV-all%20versions-8B0000)
![No install](https://img.shields.io/badge/install-none%20needed-success)

**One click. Every mod. One frozen copy that Steam can never touch.**

[⬇️ Download the latest release](../../releases/latest) · [📖 How to use](#-usage) · [❓ FAQ](#-faq)

</div>

> 🤖 **Transparency note:** this tool was coded with **[Claude Code](https://www.anthropic.com/claude-code)** (Anthropic's AI coding assistant), directed and tested by me. It is a HOI4 adaptation of the [CK3 Playset Preserver](https://github.com/Ant0nidas/CK3-Playset-Preserver) - all credit for the original idea and design goes to its authors (see [Credits](#-credits)).

---

## 🎯 The problem

A new HOI4 patch drops. Half of your mods update within days, the other half need weeks or months. Your 40-mod Millennium Dawn / Kaiserreich / Road to 56 playset is broken, your 200-hour campaign won't load, and **Steam gives you no way to go back to the old mod versions.**

## ✅ The solution

The **Playset Preserver** takes a snapshot of a playset from your HOI4 launcher and merges every enabled mod - **in the exact load order** - into **one single local mod**. Local mods are never updated by Steam, so the snapshot keeps working with the game version it was made for. Optionally it also creates a launcher playset containing just that mod, ready to play.

Keep playing your campaign on the old version while your mods catch up. 🎖️

*Example run:*

```
  ==============================================================
        HEARTS OF IRON IV  -  PLAYSET PRESERVER  v1.1.0
  ==============================================================

1. Initial playset
2. WW2
3. MD  (active)

Select the playset by typing its number: 3

Load order (later mods override earlier ones):
   1. Millennium Dawn: A Modern Day Mod  (1.19.3)
   2. Millennium Dawn New Companies Expanded  (2.4)
   ...
Total size: 6,84 GB  -  free on drive C: 86,55 GB

Copying...
  [1/11] Millennium Dawn: A Modern Day Mod
  ...
Preserved playset "MD (2026-10-24)" created.
Playset "MD (2026-10-24)" created in the launcher.
```

## ✨ Features

| | |
|---|---|
| 🎮 **Reads your launcher directly** | Pick the playset from a numbered list - no file hunting |
| 📚 **Exact load order** | Later mods override earlier ones, exactly like the game does it |
| 🧹 **Smart filtering** | Skips disabled mods, warns about mods the launcher can't find |
| 📦 **All mod sources** | Steam Workshop, local mods and Paradox Mods (zip) |
| 🧾 **Full documentation** | `README.txt` with every source mod + version, `file_to_mod_map.txt` showing which mod every file came from |
| 🔗 **Keeps `replace_path` and tags** | Total conversions and overhauls keep working |
| 💾 **Safety first** | Checks free disk space, never overwrites an existing mod, backs up the launcher database before touching it |
| 🛣️ **Long paths? No problem** | Uses robocopy, so deep mod folders don't hit the Windows 260-character limit |
| 🪶 **Zero dependencies** | One small `.exe` (about 50 KB) - no Python, no installer. Uses the .NET Framework and SQLite that ship with Windows 10/11 |

## 📥 Installation

1. Download **`HOI4-Playset-Preserver-v1.1.0.zip`** from the [latest release](../../releases/latest).
2. Extract it into your HOI4 mod folder so the files end up here:
   ```
   Documents\Paradox Interactive\Hearts of Iron IV\mod\HOI4-Playset-Preserver\HOI4_PP.exe
   ```

## 🚀 Usage

1. Make sure the playset works in the game and you have enough free disk space (about the size of all its mods together).
2. Double-click **`HOI4_PP.exe`**.
3. Answer the prompts - press **Enter** to accept the default shown in `[brackets]`:
   - confirm the terms
   - pick the playset
   - confirm game version and name
4. Grab a coffee ☕ while the mods are copied (big playsets take a few minutes).
5. Say **yes** to creating a launcher playset - close the Paradox Launcher completely first (also from the tray); the program waits for you.
6. Reopen the launcher, select the new playset, play. 🎉

> 💡 **Best time to use it:** *before* your mods update for a new patch - ideally right when a patch is announced. You can keep the game itself on the old version via Steam → HOI4 → *Properties → Betas*.

## ❓ FAQ

**Windows says "Windows protected your PC".**
That is SmartScreen being careful with a new, unsigned `.exe` that few people have downloaded yet. Click *More info → Run anyway*. If you prefer, check the source code and [build it yourself](#-build-it-yourself) - it takes one command. The program only reads your mods, writes the new mod into your mod folder and - if you agree - adds one playset to the launcher after backing up its database. All source code is right here in the repository.

**Does it change my original mods or playsets?**
No. It only reads them. The preserved mod is a new folder, the launcher playset is a new entry.

**Can I undo the launcher change?**
Yes - delete the playset in the launcher, or restore `launcher-v2.sqlite.pp_backup_<date>` from your HOI4 documents folder.

**Can I share my preserved playset?**
**No.** The content belongs to the mod authors. Share this tool instead, so everyone can make their own snapshot.

**Something is broken in my preserved playset - can I ask the mod authors?**
Please don't. Preserved playsets are unsupported snapshots; mod authors only support their current versions.

**Mac / Linux?**
Windows only for now.

## 🔧 Build it yourself

No Visual Studio or SDK needed - Windows already contains the C# compiler.

1. Download the repository (*Code → Download ZIP*) and extract it.
2. Open PowerShell in the extracted folder and run:
   ```
   powershell -ExecutionPolicy Bypass -File build.ps1
   ```
3. `HOI4_PP.exe` appears in the same folder.

`Program.cs` is the whole program, `PPSqlite.cs` reads the launcher database through Windows' own `winsqlite3.dll`, and `build.ps1` draws the icon and compiles both with `csc.exe` from .NET Framework 4.

## 🙏 Credits

### The original: CK3 Playset Preserver
This project would not exist without the **[Crusader Kings 3 Playset Preserver](https://github.com/Ant0nidas/CK3-Playset-Preserver)**, created by **Community Mods for Historicity (CMH)**:

| | Role |
|---|---|
| **[escalonn](https://github.com/escalonn)** | Developer |
| **[Kaepbora](https://github.com/Ant0nidas)** | Release Manager |

Everything that makes this tool useful was **their idea and their design**:
- snapshotting a playset straight from the launcher database
- merging all mods in load order into one local mod that Steam won't update
- skipping disabled mods and warning about missing ones
- carrying over `replace_path` lines and tags
- the README with all source mods and versions, and the file-to-mod map (which they in turn credit to CK2's HIP)
- creating a ready-to-play launcher playset
- the "don't redistribute, don't ask mod authors for support" rules that protect mod authors

The HOI4 Playset Preserver is a port of that design to Hearts of Iron IV, rewritten in C# so it runs as a small .exe without Python. If you play CK3, use their tool - and please go give their project a ⭐!

### How this port was made
The HOI4 version was **coded with [Claude Code](https://www.anthropic.com/claude-code)**, Anthropic's AI coding assistant, and directed and tested by me (schnicks17). Bug reports and suggestions are very welcome in the [Issues](../../issues).

*Hearts of Iron IV is a trademark of Paradox Interactive AB. This is a fan-made tool and is not affiliated with or endorsed by Paradox Interactive.*

## 📜 License

[GNU General Public License v3.0](LICENSE) - free to use, share and modify, as long as it stays under the same license.
