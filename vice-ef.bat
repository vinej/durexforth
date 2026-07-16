@echo off
rem durexForth EasyFlash cartridge: all modules served from flash, no disk.
rem +easyflashcrtwrite is REQUIRED - without it VICE writes the emulated
rem flash back over the .crt file when it exits, silently changing it.
rem Uses the VICE bundled in emulator\bin (see emulator\README.md).
rem Machine model (PAL/NTSC) follows your saved VICE settings; add
rem -ntsc or -pal to the start line below to force one.
cd /d "%~dp0"
if not exist "emulator\bin\x64sc.exe" (
  echo emulator\bin\x64sc.exe not found - see emulator\README.md
  pause
  exit /b 1
)
if not exist "deploy\durexforth-ef.crt" (
  echo deploy\durexforth-ef.crt is missing - rebuild the deploy artifacts first.
  pause
  exit /b 1
)
start "" emulator\bin\x64sc.exe +easyflashcrtwrite -cartcrt "deploy\durexforth-ef.crt"
