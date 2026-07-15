\ rasterbars.fs - moving colour bars, the classic demo
\ effect, driven by the raster interrupt.
\
\   include rasterbars
\   bars          \ colour bars roll down the screen
\   stop-bars     \ stop them, restore a plain screen
\
\ The callback is written in ASSEMBLY (code ... end-code).
\ That matters: a multi-bar raster must re-arm the compare
\ register $d012 several times per frame, once per bar, and
\ finish each time before the beam passes the next line.  A
\ colon-threaded Forth callback is far too slow for that and
\ storms the machine; native code does it in a handful of
\ cycles.  durexForth is subroutine-threaded, so this code
\ word is called with a plain jsr and returns with rts --
\ exactly what the raster trampoline expects.

require irq
require vic

base @ hex

#8 constant nbars              \ number of colour bars
#16 constant bar-h             \ raster lines per bar
#60 constant top-line          \ first bar starts here

\ per-frame state, all in a form the asm callback can index.
create bar-i  1 allot          \ current bar index (0..nbars-1)
create colours nbars allot     \ colour of each bar
create lines   nbars allot     \ $d012 compare line of each bar

: init-tables ( -- )
  \ a dark->light->dark colour ramp
  6 colours 0 + c!  e colours 1 + c!  4 colours 2 + c!
  e colours 3 + c!  3 colours 4 + c!  d colours 5 + c!
  1 colours 6 + c!  d colours 7 + c!
  \ evenly spaced lines from top-line down
  nbars 0 do
    top-line i bar-h * +  lines i + c!
  loop ;

\ --- the raster callback, in assembly -------------------
\ On entry the trampoline has already ack'd the VIC and
\ saved x/a/y for us, so we may clobber a/x/y freely.
\
\ Using x as the bar index:
\   border   = colours,x
\   next bar = (x+1) mod nbars ; on wrap, reset x=0
\   $d012    = lines of the *next* bar (so it fires there)
\ We reload $d012 for the next bar every time; on wrap we
\ point it back at the first bar's line.
code do-bar
bar-i ldx,                     \ x = current bar index
colours lda,x d020 sta,        \ border = this bar's colour  (colours,x)
inx,                           \ next bar
nbars cpx,#
+branch bne,                   \ if not past the end, keep x
0 ldx,#                        \ wrapped: back to bar 0
:+
bar-i stx,                     \ save next bar index
lines lda,x d012 sta,          \ arm $d012 for the next bar (lines,x)
rts,
end-code

: bars ( -- )
  #147 emit
  init-tables
  0 bar-i c!
  6 d021 c!                    \ blue screen
  \ arm the first bar's line and install the asm callback.
  lines c@ ['] do-bar raster! ;

: stop-bars ( -- )
  raster-off
  e d020 c!  6 d021 c! ;       \ plain light border, blue screen

base !
