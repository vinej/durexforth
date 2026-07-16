@echo off
rem durexForth standard cartridge, with the d64 in drive 8 for include
rem Uses the VICE bundled in emulator\bin (see emulator\README.md).
rem Machine model (PAL/NTSC) follows your saved VICE settings; add
rem -ntsc or -pal to the start line below to force one.
cd /d "%~dp0"
if not exist "emulator\bin\x64sc.exe" (
  echo emulator\bin\x64sc.exe not found - see emulator\README.md
  pause
  exit /b 1
)
if not exist "deploy\durexforth.crt" (
  echo deploy\durexforth.crt is missing - rebuild the deploy artifacts first.
  pause
  exit /b 1
)
if not exist "deploy\durexforth.d64" (
  echo deploy\durexforth.d64 is missing - rebuild the deploy artifacts first.
  pause
  exit /b 1
)
start "" emulator\bin\x64sc.exe -cartcrt "deploy\durexforth.crt" -8 "deploy\durexforth.d64"
