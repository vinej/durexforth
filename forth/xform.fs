\ xform.fs - transforming sprite data.
\
\ Cheaper than drawing your alien twice: mirror the one you
\ already have. Rotate it, flip it, stretch it to double size.
\
\ Sprites are the blit module's shape - w*h character cells of
\ 8 bytes, stored row by row - so anything here feeds straight
\ into blit-blk, or into sp-data for a hardware sprite.
\
\ These are meant to run ONCE, when your game loads, and they
\ are written for clarity rather than speed: every pixel goes
\ through a read and a write, so a 2x2 sprite is a few hundred
\ thousand cycles. Transform at startup into a second buffer,
\ then blit the buffer every frame.

base @ hex

\ --- pixel access ---------------------------------------------
\ A sprite w cells wide stores pixel (x,y) in cell (x/8, y/8),
\ byte y&7 of it, bit 7-(x&7). Working a pixel at a time is what
\ keeps the transforms below down to two lines each.

0 value pbase
0 value pw
0 value px
0 value py

: paddr ( -- addr )
  py 3 rshift pw *  px 3 rshift +  3 lshift  py 7 and +  pbase + ;
: pmask ( -- m ) 80 px 7 and rshift ;

: pix@ ( base w x y -- f )
  to py to px to pw to pbase
  paddr c@ pmask and 0<> ;

: pix! ( f base w x y -- )
  to py to px to pw to pbase
  paddr c@ swap
  if pmask or else pmask invert and then
  paddr c! ;

\ --- transforms -----------------------------------------------

0 value xsrc
0 value xdst
0 value xw
0 value xh
0 value xr

( Mirror left-to-right. dst is the same w*h as src.
    arrow mirrored 2 2 mir )
: mir ( src dst w h -- )
  to xh to xw to xdst to xsrc
  xh 3 lshift 0 do
    i to xr
    xw 3 lshift 0 do
      xsrc xw i xr pix@
      xdst xw  xw 3 lshift 1- i -  xr  pix!
    loop
  loop ;

( Flip top-to-bottom. dst is the same w*h as src. )
: flip ( src dst w h -- )
  to xh to xw to xdst to xsrc
  xh 3 lshift 0 do
    i to xr
    xw 3 lshift 0 do
      xsrc xw i xr pix@
      xdst xw  i  xh 3 lshift 1- xr -  pix!
    loop
  loop ;

( Rotate 90 degrees clockwise. NOTE the dst comes out h*w
  cells - a 1x2 sprite spins into a 2x1 one - so size the
  buffer accordingly, and blit it as "h w". )
: spin ( src dst w h -- )
  to xh to xw to xdst to xsrc
  xh 3 lshift 0 do
    i to xr
    xw 3 lshift 0 do
      xsrc xw i xr pix@
      xdst xh  xh 3 lshift 1- xr -  i  pix!
    loop
  loop ;

( Double the width. dst is 2w*h cells. )
: xpandx ( src dst w h -- )
  to xh to xw to xdst to xsrc
  xh 3 lshift 0 do
    i to xr
    xw 3 lshift 0 do
      xsrc xw i xr pix@ dup
      xdst xw 2* i 2*     xr pix!
      xdst xw 2* i 2* 1+  xr pix!
    loop
  loop ;

( Double the height. dst is w*2h cells. )
: xpandy ( src dst w h -- )
  to xh to xw to xdst to xsrc
  xh 3 lshift 0 do
    i to xr
    xw 3 lshift 0 do
      xsrc xw i xr pix@ dup
      xdst xw i  xr 2*     pix!
      xdst xw i  xr 2* 1+  pix!
    loop
  loop ;

hide pbase
hide pw
hide px
hide py
hide paddr
hide pmask
hide xsrc
hide xdst
hide xw
hide xh
hide xr

base !
