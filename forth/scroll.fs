\ scroll.fs - fine and character scrolling.
\
\ Scrolling a C64 screen is two jobs stitched together. The VIC
\ can shift the whole display 0-7 pixels for free, which is the
\ smooth part; but that is all it can do, so every 8 pixels you
\ move the characters themselves one cell, reset the fine offset
\ back to where it started, and draw the newly exposed edge. Done
\ right the two are invisible: the picture just keeps moving.

\ scr-at, col-at. Bare on purpose - see charset.fs.
require charset

base @ hex

\ --- fine (hardware) scroll -----------------------------------
\ $d016 bits 0-2 shift right by 0-7 pixels, $d011 bits 0-2 down.

: xscroll@ ( -- 0..7 ) d016 c@ 7 and ;
: yscroll@ ( -- 0..7 ) d011 c@ 7 and ;
: xscroll! ( 0..7 -- ) 7 and  d016 c@ f8 and or  d016 c! ;
: yscroll! ( 0..7 -- ) 7 and  d011 c@ f8 and or  d011 c! ;

\ Narrowing the display makes the VIC cover the edge with border.
\ A side-scroller wants col38 on: without it the column being
\ scrolled in is half-drawn at the screen edge, and it flickers.

: col38 ( -- ) d016 c@ f7 and d016 c! ;
: col40 ( -- ) d016 c@ 8 or d016 c! ;
: row24 ( -- ) d011 c@ f7 and d011 c! ;
: row25 ( -- ) d011 c@ 8 or d011 c! ;

\ --- character scroll -----------------------------------------
\ Shift a w*h window of the screen one cell. The vacated edge is
\ left alone rather than cleared: the caller draws the new column
\ or row into it, which is the whole point of scrolling.
\
\ Colour ram is shifted along with the characters, because it does
\ NOT move with the screen - it is wired at $d800. Games with a
\ uniform colour scheme can set col-scroll to 0 and halve the work.
\
\ move sorts out the overlap for us: it copies backwards when
\ source < destination, so scroll-right and scroll-down are safe.

-1 value col-scroll     \ shift colour ram too?

0 value s-x
0 value s-y
0 value s-w
0 value s-h
0 value s-r

: scroll-left ( x y w h -- )
  to s-h to s-w to s-y to s-x
  s-h 0 do
    s-y i + to s-r
    s-x 1+ s-r scr-at  s-x s-r scr-at  s-w 1- move
    col-scroll if
      s-x 1+ s-r col-at  s-x s-r col-at  s-w 1- move
    then
  loop ;

: scroll-right ( x y w h -- )
  to s-h to s-w to s-y to s-x
  s-h 0 do
    s-y i + to s-r
    s-x s-r scr-at  s-x 1+ s-r scr-at  s-w 1- move
    col-scroll if
      s-x s-r col-at  s-x 1+ s-r col-at  s-w 1- move
    then
  loop ;

: scroll-up ( x y w h -- )
  to s-h to s-w to s-y to s-x
  s-h 1- 0 do
    s-y i + to s-r
    s-x s-r 1+ scr-at  s-x s-r scr-at  s-w move
    col-scroll if
      s-x s-r 1+ col-at  s-x s-r col-at  s-w move
    then
  loop ;

: scroll-down ( x y w h -- )
  to s-h to s-w to s-y to s-x
  s-h 1- 0 do
    s-y s-h + 2 - i - to s-r
    s-x s-r scr-at  s-x s-r 1+ scr-at  s-w move
    col-scroll if
      s-x s-r col-at  s-x s-r 1+ col-at  s-w move
    then
  loop ;

\ --- wrapping scrolls ------------------------------------------
\ Same shift, but whatever falls off one edge comes back on the
\ other, so the window keeps its contents forever. Good for a
\ repeating background or a marquee; a scroller that draws new
\ material wants the plain scroll-* above instead.

0 value s-t             \ the character carried around
0 value s-c             \ and its colour
create s-row #40 allot  \ a whole row, for the vertical wraps
create s-rowc #40 allot

: wrap-left ( x y w h -- )
  to s-h to s-w to s-y to s-x
  s-h 0 do
    s-y i + to s-r
    s-x s-r scr-at c@ to s-t
    s-x 1+ s-r scr-at  s-x s-r scr-at  s-w 1- move
    s-t  s-x s-w + 1- s-r scr-at c!
    col-scroll if
      s-x s-r col-at c@ to s-c
      s-x 1+ s-r col-at  s-x s-r col-at  s-w 1- move
      s-c  s-x s-w + 1- s-r col-at c!
    then
  loop ;

: wrap-right ( x y w h -- )
  to s-h to s-w to s-y to s-x
  s-h 0 do
    s-y i + to s-r
    s-x s-w + 1- s-r scr-at c@ to s-t
    s-x s-r scr-at  s-x 1+ s-r scr-at  s-w 1- move
    s-t  s-x s-r scr-at c!
    col-scroll if
      s-x s-w + 1- s-r col-at c@ to s-c
      s-x s-r col-at  s-x 1+ s-r col-at  s-w 1- move
      s-c  s-x s-r col-at c!
    then
  loop ;

: wrap-up ( x y w h -- )
  to s-h to s-w to s-y to s-x
  s-x s-y scr-at  s-row  s-w move
  col-scroll if s-x s-y col-at  s-rowc  s-w move then
  s-h 1- 0 do
    s-y i + to s-r
    s-x s-r 1+ scr-at  s-x s-r scr-at  s-w move
    col-scroll if
      s-x s-r 1+ col-at  s-x s-r col-at  s-w move
    then
  loop
  s-row  s-x s-y s-h + 1- scr-at  s-w move
  col-scroll if s-rowc  s-x s-y s-h + 1- col-at  s-w move then ;

: wrap-down ( x y w h -- )
  to s-h to s-w to s-y to s-x
  s-x s-y s-h + 1- scr-at  s-row  s-w move
  col-scroll if s-x s-y s-h + 1- col-at  s-rowc  s-w move then
  s-h 1- 0 do
    s-y s-h + 2 - i - to s-r
    s-x s-r scr-at  s-x s-r 1+ scr-at  s-w move
    col-scroll if
      s-x s-r col-at  s-x s-r 1+ col-at  s-w move
    then
  loop
  s-row  s-x s-y scr-at  s-w move
  col-scroll if s-rowc  s-x s-y col-at  s-w move then ;

hide s-t
hide s-c
hide s-row
hide s-rowc
hide s-x
hide s-y
hide s-w
hide s-h
hide s-r

base !
