# emulator\ — bundled VICE (gitignored)

Everything in this folder except this README is gitignored. Supply it
yourself:

| What | Where to get it | Notes |
| --- | --- | --- |
| VICE 3.9 (GTK3, win64) | https://vice-emu.sourceforge.io/ | Unzip the release so that `emulator\bin\x64sc.exe` (and `c1541.exe`, `petcat.exe`) exist. The C64 ROMs ship with VICE — nothing else to supply. |
| `vice-c64debug.ini` | auto-created | `tools\binmon.py` writes it if missing; it isolates debug sessions from your global VICE settings (see the header in binmon.py). |

The debug adapter launches `bin\x64sc.exe -binarymonitor` itself;
`c1541.exe` and `petcat.exe` are used both by `tools\build-durexforth.ps1`
and at debug time (writing your program onto the per-session work disk).
