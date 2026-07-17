@echo off
rem Original durexForth v5 (upstream), for side-by-side comparison
rem with the game-dev build. The d64 rides along in drive 8 so the
rem same source files can be opened in v; the v5 cart boots its own
rem ROM regardless of what is on the disk.
rem Uses the VICE bundled in emulator\bin (see emulator\README.md).
rem Machine model (PAL/NTSC) follows your saved VICE settings; add
rem -ntsc or -pal to the start line below to force one.
cd /d "%~dp0"
if not exist "emulator\bin\x64sc.exe" (
  echo emulator\bin\x64sc.exe not found - see emulator\README.md
  pause
  exit /b 1
)
if not exist "deploy\durexforth-v5.crt" (
  echo deploy\durexforth-v5.crt is missing.
  pause
  exit /b 1
)
start "" emulator\bin\x64sc.exe -cartcrt "deploy\durexforth-v5.crt" -8 "deploy\durexforth.d64"
