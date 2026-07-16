\ mousedemo.fs - a sprite pointer you move with a 1351
\ mouse, updated under interrupt.
\
\   include mousedemo
\   mouse-demo        \ an arrow sprite follows the mouse
\   ( move the mouse; left button turns it red )
\   stop-mouse-demo   \ stop and hide the pointer
\
\ Plug a 1351 mouse into control port 1.  In VICE: enable a
\ mouse (Settings > Input > Mouse, type 1351, port 1) and
\ grab it.
\
\ Shows mouse.fs + irq.fs together: mouse-update runs every
\ frame in the background, and the foreground (here, the irq
\ callback itself) moves sprite 0 to the pointer position.

require sprite
require irq
require mouse

base @ hex

\ sprite 0 data: a small arrow pointer in the top-left of
\ the 24x21 cell.  It lives at $0380 (sprite block $0e), NOT
\ the more usual $0340: durexForth's word-lookup buffer is at
\ $033c..$035b, so a sprite at $0340 gets overwritten by the
\ names of every word parsed afterwards.  $0380..$03bf is
\ clear of that buffer and below the $0400 screen.
$0380 sp-data
XX......................
XXX.....................
XXXX....................
XXXXX...................
XXXXXX..................
XXXXXXX.................
XXXXXXXX................
XXXXXXXXX...............
XXXXXXXXXX..............
XXXXXX..................
XXX.XXX.................
XX..XXX.................
.....XXX................
.....XXX................
......XXX...............
......XXX...............
.......X................
........................
........................
........................
........................

\ Keep the pointer inside the visible sprite range.  Sprite X is a
\ 9-bit coordinate (0..511); the visible screen is roughly x=24..343.
\ The arrow's tip is the TOP-LEFT pixel of the sprite cell, so the
\ tip position equals the sprite x/y.  These max values were tuned on
\ real hardware (MiSTer C64 core) so the tip reaches the right edge
\ and ymax=249 leaves 1px of the pointer on the last visible line.
: setup-range ( -- )
  #24  mouse-xmin !  #343 mouse-xmax !
  #50  mouse-ymin !  #249 mouse-ymax ! ;

\ the per-frame tick: read the mouse, move the sprite, and
\ tint it while the left button is held.  Pure register /
\ variable pokes, so it is irq-safe.
: tick ( -- )
  mouse-update
  mouse-x @ mouse-y @ 0 sp-xy!
  mouse-lb? if 2 else 1 then 0 sp-col! ;   \ red while pressed

: mouse-demo ( -- )
  #147 emit                         \ clear screen
  0 $d021 c!  6 $d020 c!           \ black screen, blue border
  1 mouse-port                      \ mouse in port 1
  \ In VICE the 1351 maps a full window sweep to only ~64
  \ pot units, so scale up to cover the 320-wide screen in
  \ one swipe (64 units * 5 ~= 320).  On real hardware you
  \ may prefer 1 or 2.
  5 mouse-speed !
  setup-range
  mouse-init
  $0380 $40 / $07f8 c!              \ sprite 0 pointer -> $0380
  1 0 sp-col!                       \ white
  0 sp-on                           \ enable sprite 0
  ['] tick irq! ;                   \ track under interrupt

: stop-mouse-demo ( -- )
  irq-off
  0 sp-off ;

base !
