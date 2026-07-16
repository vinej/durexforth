\ scroll8demo.fs - push the joystick, the world moves.
\
\   include scroll8demo
\   scroll8demo      \ joystick in port 2; run/stop quits
\
\ A camera over a 64x48 map, moving a pixel a frame in whatever
\ direction you push - including diagonally, which costs no more
\ than the straight directions and needs no extra code: the
\ camera has an x and a y and scroll8 works out the rest.
\
\ The map is a grid of 8x8 blocks, each labelled with its own
\ column and row digits, so you can read straight off the screen
\ whether the camera is where it claims. Hold a diagonal into a
\ corner and it stops cleanly: the camera clamps to the map
\ rather than scrolling off into whatever lies past the end.
\
\ It takes a moment to build the map at startup - 3072 cells,
\ laid down one at a time by the interpreter.

require scroll8
require joy
require keyb

base @ hex

#64 constant mw
#48 constant mh

create the-map mw mh * allot
create the-col mw mh * allot

cc constant curflag

: m-at ( x y -- addr ) mw * + the-map + ;
: c-at ( x y -- addr ) mw * + the-col + ;

( Screen codes, not petscii: 91 is the cross, 64 the horizontal
  bar and 93 the vertical one, which draw the grid. Each block
  gets its column and row digit at a fixed spot inside it. )
: build-map ( -- )
  mh 0 do
    mw 0 do
      i 7 and 0= j 7 and 0= and if #91 else
      j 7 and 0= if #64 else
      i 7 and 0= if #93 else
      j 7 and 2 = i 7 and 2 = and if i 3 rshift #10 mod #48 + else
      j 7 and 2 = i 7 and 3 = and if j 3 rshift #10 mod #48 + else
      #32 then then then then then
      i j m-at c!
      ( a checkerboard, so the blocks read as blocks )
      i 3 rshift j 3 rshift + 1 and if #14 else #3 then
      i j c-at c!
    loop
  loop ;

: dx ( b -- n ) dup joy-left? if drop -1 exit then
                joy-right? if 1 else 0 then ;
: dy ( b -- n ) dup joy-up? if drop -1 exit then
                joy-down? if 1 else 0 then ;

: frame ( -- ) a2 c@ begin dup a2 c@ <> until drop ;

: scroll8demo ( -- )
  1 curflag c!
  page
  ." building the map.." cr
  build-map
  page

  the-map mw mh map!
  the-col map-col!              \ drop this line and it scrolls
                                \ twice as cheaply, in one colour
  0 0 #40 #25 win!
  0 to cam-x  0 to cam-y
  cam-start

  begin
    frame
    joy2 dup dx swap dy cam+
    k-stop kb?
  until

  cam-stop
  page 0 curflag c! ;

base !
