\ scrolldemo.fs - smooth scrolling banners.
\
\ Two demos, one per direction. Run them one at a time - that
\ is the whole point. With a single banner on screen the vic's
\ fine scroll is free to move it a pixel at a time and the
\ result glides; two banners would have to share that one
\ register and neither could have it.
\
\   include scrolldemo
\   hbanner        \ a sentence gliding left to right
\   vbanner        \ a word gliding top to bottom
\
\ Press RUN/STOP to stop either one. They hold the screen while
\ they run rather than returning to the prompt, because "ok"
\ and a blinking cursor land right on top of the banner.
\
\ Each is the fine+coarse loop: move the display one pixel with
\ the vic, and every eighth pixel move the characters one cell
\ and snap the fine offset back where it started. Done right
\ the join is invisible - the banner just keeps going. They
\ wrap, so the sentence cycles forever with nothing feeding it
\ new characters.
\ The stepping is driven from the FOREGROUND, one step per
\ frame, not from irq!. It has to be: scroll.fs's wrap words
\ use do/loop, and do/loop is not allowed in an interrupt
\ callback - it tsx'es onto the cpu stack and collides with the
\ private data-stack window the callback runs on, which shows
\ up as a stack underflow in the foreground. A banner does not
\ need the interrupt anyway; syncing to the jiffy clock gives
\ exactly one step a frame.

require scroll
require keyb

base @ hex

#12 constant hrow       \ across the middle
#19 constant vcol       \ down the middle
#25 constant vlen       \ full height, so it wraps top to bottom

cc constant curflag     \ $cc: non-zero stops the cursor blinking

( Frames per pixel: bigger is slower. 1 is a demo scroller -
  60 pixels a second, far too quick to read. 8 is about one
  character a second, which is a banner you can read. Change
  it and restart the demo:
    4 to bspeed      \ twice as fast
    #16 to bspeed    \ half as slow )
#8 value bspeed

: hmsg s" durexforth scrolls forever   " ;
: vmsg s" durexforth " ;

\ p>s and text! come from charset (via scroll)

: v-put ( addr len -- )   \ and this one down column vcol
  0 do
    dup i + c@ p>s  vcol i scr-at c!
  loop drop ;

0 value fine            \ pixels moved since the last cell step
0 value fc              \ frames since the last pixel

( wait for the jiffy clock to tick = one frame )
: frame ( -- ) a2 c@ begin dup a2 c@ <> until drop ;

\ --- left to right --------------------------------------------
\ col38 matters: it makes the vic cover the screen edge with
\ border, hiding the column that is only half scrolled in.

: h-step
  fc 1+ to fc
  fc bspeed < if exit then
  0 to fc
  fine 1+ to fine
  fine 8 = if 0 to fine  0 hrow #40 1 wrap-right then
  fine xscroll! ;

: hbanner ( -- )
  1 curflag c!            \ before the clear, or the blink leaves a block
  page col38
  hmsg 0 hrow text!
  7 #40 1 0 hrow tile-col!
  0 to fine  0 to fc  0 xscroll!
  begin frame h-step k-stop kb? until
  0 xscroll!  col40
  page  0 curflag c! ;

\ --- top to bottom --------------------------------------------
\ row24 does for the top and bottom edges what col38 does for
\ the sides.

: v-step
  fc 1+ to fc
  fc bspeed < if exit then
  0 to fc
  fine 1+ to fine
  fine 8 = if 0 to fine  vcol 0 1 vlen wrap-down then
  fine yscroll! ;

: vbanner ( -- )
  1 curflag c!
  page row24
  vmsg v-put
  5 1 vlen vcol 0 tile-col!
  0 to fine  0 to fc  0 yscroll!
  begin frame v-step k-stop kb? until
  3 yscroll!  row25
  page  0 curflag c! ;

base !
