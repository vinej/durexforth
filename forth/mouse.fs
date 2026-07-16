\ mouse.fs - Commodore 1351 proportional mouse.
\
\ The 1351 (in its default proportional mode) reports its
\ position modulo 64 in the SID paddle registers POTX/POTY.
\ It does NOT give an absolute position - each read is 6
\ bits that wrap - so a driver keeps its own x/y and adds
\ the frame-to-frame delta.  Call  mouse-update  once per
\ frame (e.g. from an irq! / raster! callback); then read
\ mouse-x / mouse-y and the buttons.
\
\   include mouse
\   mouse-init            \ select the port, centre the pointer
\   ... once per frame ...
\   mouse-update          \ fold in this frame's movement
\   mouse-x @  mouse-y @   \ current position
\   mouse-lb?  mouse-rb?   \ buttons (true = pressed)
\
\ Plug the mouse into control port 1 (the usual place).
\ For port 2 use  2 mouse-port .
\
\ Buttons come through the joystick lines of the port, so
\ while the mouse is in port 1 they share the keyboard
\ matrix (noisy during typing) - the same caveat as joy1.

base @ hex

d419 constant potx         \ SID paddle X (mouse X, mod 64)
d41a constant poty         \ SID paddle Y (mouse Y, mod 64)

variable mouse-x           \ accumulated position (you set the range)
variable mouse-y
variable mouse-oldx        \ last raw pot reading (0..3f)
variable mouse-oldy
variable mouse-mux         \ $dc00 value that selects the mouse port
variable mouse-speed       \ movement multiplier (1 = 1:1)
0 mouse-x !  0 mouse-y !
0 mouse-oldx !  0 mouse-oldy !
1 mouse-speed !            \ default 1:1; raise to cover more ground per move

\ screen-ish clamp range for the pointer.  1351 x is often
\ used as a 9-bit sprite coordinate; keep it simple and
\ clamp to a configurable box.
variable mouse-xmin  variable mouse-xmax
variable mouse-ymin  variable mouse-ymax
0 mouse-xmin !  #319 mouse-xmax !
0 mouse-ymin !  #199 mouse-ymax !

\ --- port selection -------------------------------------
\ The SID pot lines are muxed to a control port by CIA1
\ $dc00 bits 6/7.  bit6=1 ($40) -> port 1, bit7=1 ($80) -> port 2.
\ We also remember which port register holds the buttons.
variable mouse-btnreg      \ $dc01 (port 1) or $dc00 (port 2)

: mouse-port ( n -- )      \ 1 or 2
  1 = if
    40 mouse-mux !  dc01 mouse-btnreg !
  else
    80 mouse-mux !  dc00 mouse-btnreg !
  then ;

1 mouse-port               \ default: port 1

\ --- reading --------------------------------------------
\ A pot register holds the position in bits 1..6 (bit0 is
\ noise, bit7 is don't-care), so >>1 and mask to 6 bits.
: mouse-raw ( addr -- pos ) c@ 2/ 3f and ;

\ signed 6-bit delta between two raw readings, handling the
\ mod-64 wrap: result is -32..+31.
: mouse-delta ( new old -- d )
  - 3f and                 \ low 6 bits of the difference
  dup 20 and if 40 - then ;  \ sign-extend from bit 5

: clamp ( v lo hi -- v ) rot min max ;

\ mouse-update: fold this frame's movement into mouse-x/y.
\ Select the port's pots first; the SID samples the pots
\ continuously so a single read per frame is plenty.
: mouse-update ( -- )
  mouse-mux @ dc00 c!               \ route this port's pots to SID
  potx mouse-raw                    \ new raw x
  dup mouse-oldx @ mouse-delta      \ dx
  mouse-speed @ *                   \ scale by the speed multiplier
  mouse-x @ +  mouse-xmin @ mouse-xmax @ clamp mouse-x !
  mouse-oldx !                      \ store new raw x as old
  poty mouse-raw                    \ new raw y
  dup mouse-oldy @ mouse-delta      \ dy
  mouse-speed @ *                   \ scale by the speed multiplier
  \ the 1351 y grows upward; negate so +dy moves the
  \ pointer DOWN the screen, matching sprite coordinates.
  negate mouse-y @ +  mouse-ymin @ mouse-ymax @ clamp mouse-y !
  mouse-oldy ! ;

\ --- buttons --------------------------------------------
\ Active-low on the joystick lines: left button = fire
\ (bit 4), right button = up (bit 0).
: mouse-lb? ( -- f ) mouse-btnreg @ c@ 10 and 0= ;
: mouse-rb? ( -- f ) mouse-btnreg @ c@ 1 and 0= ;

\ --- init -----------------------------------------------
\ Prime the old readings (so the first mouse-update sees a
\ zero delta) and centre the pointer in its box.
: mouse-init ( -- )
  mouse-mux @ dc00 c!
  potx mouse-raw mouse-oldx !
  poty mouse-raw mouse-oldy !
  mouse-xmin @ mouse-xmax @ + 2/ mouse-x !
  mouse-ymin @ mouse-ymax @ + 2/ mouse-y ! ;

base !
