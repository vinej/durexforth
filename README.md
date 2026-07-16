# durexforth

Modern C64 Forth. Includes a vi clone written in Forth, a high-resolution graphics library, plus MML music support.

[Latest Release](https://github.com/jkotlinski/durexforth/releases) :: [Manual](https://jkotlinski.github.io/durexforth/)

### The game-dev branch

The `game-dev-features` branch turns durexForth into a game development system: interrupts (`irq!`, `raster!`), split screens, character sets and tiles, smooth scrolling in all eight directions, hardware and software sprites with transformations, keyboard matrix and joystick and 1351 mouse, SID music under interrupt (MML and GoatTracker/PSID), double and float word sets, string words, and an EasyFlash cartridge with a flash filesystem so `include` needs no disk drive at all. Everything is documented in the [manual](https://vinej.github.io/durexforth/), with a chapter on importing assets from SpritePad Pro, CharPad Pro and GoatTracker 2.

**Status: feature-complete, in field testing before a PR.** Every module is verified in VICE and the cartridge runs on MiSTer; what remains is play-testing in real use. Known polish items — rough edges, not gaps:

* **split screen**: the character wrap of a scroller lands one frame apart from the fine-scroll reset — a single-frame hiccup every 8th pixel at the seam-free default. The clean fix is double-buffering the screen with `band-screen!` and flipping at `band-sync`.
* **sidmusic**: the player mechanism is verified, but a real GoatTracker tune re-packed for ~`$5000` has not been played yet — GoatTracker's default `$1000` sits inside the interpreter.
* **scroll8demo**: the camera engine (`cam+`) is fully verified; the joystick read itself still wants a human hand on real hardware.
* **`irq!`/`raster!` hold one callback each**: modules that install their own (music, bands) replace yours — compose by calling both from one word. A true callback chain needs an assembly dispatcher; a Forth one cost 64→40 Hz and was reverted.
* **open curiosity**: single-letter words `a`–`e` defined at the prompt once produced garbage from blits while identical code under longer names was perfect; undiagnosed, suspected number-parser/`find` interaction. Avoid single-letter definitions until it is understood.
* **the module set has outgrown a raw d64**: the Makefile's one-disk flow (kernel + every module source, self-packing on first boot) now needs ~888 blocks of 664. The shipped `durexforth.d64` is therefore built pre-packed — packed system + every includable module — which fits with one exception (`gfxdemo`); `durexforth.d71` carries absolutely everything. `make deploy` as written would overflow: use `DISK_SUF=d71`, or teach it the pack-first layout.

[![build status](https://github.com/jkotlinski/durexforth/actions/workflows/build.yml/badge.svg)](https://github.com/jkotlinski/durexforth/actions/workflows/build.yml)

### Goals

* Fun. The system should be nice to work with on the real machine.
* Fast. DurexForth is the <a href=https://theultimatebenchmark.org/>fastest</a> C64 Forth, running at ~50x the speed of Basic V2!
* Easy to use. Implements the <a href=http://forth-standard.org/standard/words>Forth 2012</a> core standard, learn it with <a href=https://www.forth.com/starting-forth/>Starting Forth</a>!

### Testimonials

<img src=http://i.imgur.com/eXsaXjo.png?1>

[C64 Programming May the Forth be with you Pt 1 - YouTube](https://www.youtube.com/watch?v=TXIDqptXmiM)

[C64 Programming Into the Forth Dimension Pt 2 - YouTube](https://www.youtube.com/watch?v=1oZztCmC8kc)

[A Brief Introduction to DurexForth for the Commodore 64](https://dev.to/ianwitham/a-brief-introduction-to-durexforth-for-the-commodore-64-1c99)

"Just fooling around but Durexforth is fast and fun!" -Kevin Reno

"Ich finde das Forth klein und effektiv, wunderbar." -Peter Bierbach

"Ist eine mächtige Sprache für ein 8-Bitter." -Pebisoft

"The author of durexForth was quite helpful... There is even a vim-like editor, impressive for being on a C64." -Christian Johansson
