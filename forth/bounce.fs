\ bounce.fs - a sprite bouncing around the screen,
\ animated entirely under interrupt.
\
\   include bounce
\   bounce        \ start it; returns to the ok prompt
\   ( the ball keeps bouncing while you type )
\   stop-bounce   \ stop and hide the sprite
\
\ Demonstrates irq.fs: the mover word runs 60x/sec in
\ the background via  ' move-ball irq! .

require sprite
require irq

base @ hex

\ sprite 0 data: a filled ball (a circle) centred in
\ the 24x21 sprite cell.  Each row is exactly 24 chars;
\ '.' = transparent pixel, anything else = solid.
$0340 sp-data
........XXXXXXXX........
......XXXXXXXXXXXX......
.....XXXXXXXXXXXXXX.....
....XXXXXXXXXXXXXXXX....
...XXXXXXXXXXXXXXXXXX...
..XXXXXXXXXXXXXXXXXXXX..
..XXXXXXXXXXXXXXXXXXXX..
.XXXXXXXXXXXXXXXXXXXXXX.
.XXXXXXXXXXXXXXXXXXXXXX.
.XXXXXXXXXXXXXXXXXXXXXX.
.XXXXXXXXXXXXXXXXXXXXXX.
.XXXXXXXXXXXXXXXXXXXXXX.
.XXXXXXXXXXXXXXXXXXXXXX.
..XXXXXXXXXXXXXXXXXXXX..
..XXXXXXXXXXXXXXXXXXXX..
...XXXXXXXXXXXXXXXXXX...
....XXXXXXXXXXXXXXXX....
.....XXXXXXXXXXXXXX.....
......XXXXXXXXXXXX......
........XXXXXXXX........
........................

\ ball state: position (x is 9-bit, 0..320; y is 0..255)
\ and velocity.  Kept as ordinary cells; the irq mover
\ reads/writes them, the foreground only sets them up.
variable ball-x   variable ball-y
variable ball-vx  variable ball-vy
variable frame                \ interrupt frame divider

\ speed = move the ball once every SLOW interrupts.
\ higher = slower.  1 moves the ball every frame (~60 px/sec).
#1 constant slow

\ screen extents where the ball stays visible
#24  constant x-min      \ left edge (sprite x)
#320 constant x-max      \ right edge
#50  constant y-min      \ top edge
#229 constant y-max      \ bottom edge

\ --- bounce sound (SID voice 3) ---------------------
\ We play a short percussive "blip" on each edge hit by
\ retriggering voice 3's gate.  This is IRQ-safe: it
\ only pokes SID registers and the envelope decays on
\ its own, so the callback never has to wait.
: sid-init ( -- )
  $0f $d418 c!             \ master volume
  $00 $d40e c!  $10 $d40f c! \ voice-3 freq (overwritten per beep)
  $0a $d413 c!             \ attack 0 / decay a
  $00 $d414 c! ;           \ sustain 0 / release 0

: beep ( freq -- )
  $d40e !                  \ voice-3 16-bit frequency
  $10 $d412 c!             \ gate off (triangle, no gate)
  $11 $d412 c! ;           \ gate on -> retrigger the blip

$0800 constant wall-tone   \ side walls
$1000 constant floor-tone  \ top/bottom

\ step-ball: advance one step and bounce off the edges,
\ beep on each hit, then update ball-x / ball-y.
: step-ball ( -- )
  ball-x @ ball-vx @ + ball-x !
  ball-y @ ball-vy @ + ball-y !
  ball-x @ x-min < if x-min ball-x ! ball-vx @ abs ball-vx ! wall-tone beep then
  ball-x @ x-max > if x-max ball-x ! ball-vx @ abs negate ball-vx ! wall-tone beep then
  ball-y @ y-min < if y-min ball-y ! ball-vy @ abs ball-vy ! floor-tone beep then
  ball-y @ y-max > if y-max ball-y ! ball-vy @ abs negate ball-vy ! floor-tone beep then ;

\ Write the sprite position, but first wait for the
\ raster to leave the visible area (spin until $d012
\ reaches the lower border, line >= 250).  This avoids
\ tearing: the VIC never reads the coordinate registers
\ mid-update, so no ghost of the old position appears.
: show-ball ( -- )
  begin $d012 c@ #250 u< while repeat
  ball-x @ ball-y @ 0 sp-xy! ;

\ move-ball: runs under interrupt every frame, but only
\ steps the ball once every `slow` frames so it glides.
: move-ball ( -- )
  frame @ 1+ dup slow < if frame ! exit then
  drop 0 frame !
  step-ball show-ball ;

: bounce ( -- )
  #147 emit                         \ clear screen
  6 $d021 c!                        \ blue background
  6 $d020 c!                        \ blue border
  #160 ball-x !  #120 ball-y !     \ start centre-ish
  1 ball-vx !    1 ball-vy !        \ 1 px per step
  0 frame !
  sid-init                          \ set up the blip voice
  $0340 $40 / $07f8 c!              \ sprite 0 pointer -> $0340
  1 0 sp-col!                       \ white
  0 sp-on                           \ enable sprite 0
  ['] move-ball irq! ;             \ animate under irq

: stop-bounce ( -- )
  irq-off
  0 sp-off
  $10 $d412 c!                      \ gate off voice 3
  0 $d418 c! ;                      \ silence

base !
