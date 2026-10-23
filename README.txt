HEARTS OF IRON IV - PLAYSET PRESERVER v1.1.0
============================================

Makes a frozen copy of a launcher playset: every enabled mod is merged, in load order,
into ONE local mod. Steam updates can no longer break it, so running campaigns stay playable
after a game patch while your mods update one by one.

Usage
  1. Make sure the playset works in the game.
  2. Double-click HOI4_PP.exe.
  3. Pick the playset, confirm the game version and the name.
  4. Optionally let it create a new launcher playset that contains only the merged mod
     (close the Paradox Launcher first - the tool waits for it and backs up the database
     as launcher-v2.sqlite.pp_backup_<date> before writing).

Notes
  - Use it BEFORE mods update for a new patch.
  - Later mods in the load order overwrite files of earlier ones, just like in the game.
    file_to_mod_map.txt in the new mod shows which mod each file came from,
    README.txt lists all source mods and their versions.
  - Disabled mods are skipped; missing mods are reported.
  - Steam Workshop, local and Paradox Mods (zip) mods are supported.
  - It never overwrites an existing mod.
  - Windows 10/11 only. No Python or installer needed.
  - If Windows SmartScreen warns you: More info -> Run anyway (the source code is on GitHub).
  - Do not distribute preserved playsets - all content belongs to the mod authors.

Credits
  Port of the Crusader Kings 3 Playset Preserver by escalonn and Kaepbora
  (Community Mods for Historicity): https://github.com/Ant0nidas/CK3-Playset-Preserver
  This HOI4 version was coded with Claude Code (AI) and directed and tested by schnicks17.
  https://github.com/schnicks17/HOI4-Playset-Preserver - GPL-3.0