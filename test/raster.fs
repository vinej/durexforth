\ raster.fs - smoke test for the raster-line IRQ (raster!).
\
\ Success criteria:
\   1. the machine does NOT hang when raster! installs
\      (the classic failure was an IRQ storm),
\   2. the callback actually fires (a counter advances),
\   3. the foreground keeps running (we reach saveb),
\   4. raster-off cleanly restores the jiffy IRQ.
\
\ Run:  include raster
\ Pass: writes the file  ok  and exits VICE.

include irq
include vic

base @ hex

variable rcount   0 rcount !     \ times the callback fired
create rerr 1 allot  0 rerr c!   \ nonzero => a check failed

\ the raster callback: bump the counter and paint the
\ border so the split is visible on the screenshot.
: on-raster ( -- )
  rcount @ 1+ rcount !
  2 d020 c! ;                     \ red border below the split line

: settle ( n -- )                 \ crude busy-wait, n outer loops
  0 do #2000 0 do loop loop ;

: check ( -- )
  #147 emit                       \ clear screen
  6 d020 c!  0 d021 c!            \ light-blue border, black bg
  0 rcount !

  \ fire on line $80 (128): mid-screen, safely in the
  \ visible area so the split shows.
  80 ['] on-raster raster!

  \ let it run for a bunch of frames.
  #10 settle

  \ the counter must have advanced (callback fired) and we
  \ must still be alive to run this code (no hang).
  rcount @ 0= if 1 rerr c! then

  raster-off

  \ after raster-off the counter must FREEZE.
  rcount @  #4 settle  rcount @
  <> if 2 rerr c! then

  \ restore a sane border and report.
  #147 emit
  rerr c@ if
    ." raster FAIL err=" rerr c@ . cr
  else
    ." raster OK fires=" rcount @ . cr
    0 1 s" ok" saveb              \ signal PASS to the harness
  then ;

check

0 $d7ff c!                        \ -debugcart: quit VICE

base !
