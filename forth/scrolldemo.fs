\ scrolldemo.fs - banners that scroll forever.
\
\ A sentence across the middle of the screen cycling left to
\ right, and a second one running top to bottom down a column.
\ Both run under the interrupt, so they keep moving while you
\ type at the prompt.
\
\   include scrolldemo
\   scrollers        \ start them
\   stop-scrollers   \ stop
\
\ They wrap rather than scroll: whatever leaves one edge comes
\ back at the other, so the sentence cycles forever with no
\ code feeding it new characters.
\
\ The motion is one CHARACTER at a time, not one pixel. The
\ vic's fine scroll would smooth it, but it shifts the WHOLE
\ screen - it cannot move one banner without dragging the other
\ along with it. A single smooth banner is a different demo:
\ see "Scrolling" in the manual for the fine+coarse loop.

require scroll
require irq

base @ hex

#12 constant hrow       \ the across-the-middle banner
#2  constant vcol       \ the down-the-side one
#15 constant vtop       \ ...well below the other, so they never cross
#10 constant vlen

: hmsg s" durexforth scrolls forever " ;
: vmsg s" downwards " ;

( petscii -> screen code. Enough for lower case and spaces,
  which is all these banners are made of. )
: p>s ( c -- c' ) dup #64 < if exit then #64 - ;

0 value bp

: h-put ( addr len -- )   \ lay the sentence along row hrow
  0 hrow scr-at to bp
  0 do
    dup i + c@ p>s  bp i + c!
  loop drop ;

: v-put ( addr len -- )   \ and this one down column vcol
  0 do
    dup i + c@ p>s  vcol vtop i + scr-at c!
  loop drop ;

\ Both banners step every few frames. Straight off the 60Hz
\ interrupt a character a frame is 480 pixels a second, far too
\ fast to read, so count frames and move on every nth.
0 value hc
0 value vc

: h-step
  hc 1+ to hc
  hc 8 = if 0 to hc  0 hrow #40 1 wrap-right then ;

: v-step
  vc 1+ to vc
  vc #12 = if 0 to vc  vcol vtop 1 vlen wrap-down then ;

: tick h-step v-step ;

: scrollers ( -- )
  page
  hmsg h-put
  vmsg v-put
  7 #40 1 0 hrow tile-col!          \ yellow across
  5 1 vlen vcol vtop tile-col!      \ green down
  0 to hc  0 to vc
  ['] tick irq! ;

: stop-scrollers ( -- ) irq-off page ." stopped" cr ;

base !
