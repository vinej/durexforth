\ scroll8.fs - scroll a window of a map in any of eight
\ directions.
\
\ scroll.fs shifts what is already on the screen, one edge at a
\ time, and leaves you to draw the newly exposed strip. That is
\ right for a banner and wrong for a world: a game does not
\ scroll THE SCREEN, it moves a CAMERA over a map and the
\ screen shows whatever the camera is looking at. Say where the
\ camera is and the screen follows - which direction it moved,
\ or whether it moved diagonally, stops being something anyone
\ has to think about:
\
\   include scroll8
\   the-map #64 #48 map!      ( 64x48 characters of world )
\   the-col map-col!          ( its colours, or drop this line )
\   0 0 #40 #25 win!          ( the whole screen looks at it )
\   cam-start
\   1 1 cam+                  ( one pixel right and down )
\   0 0 cam!                  ( or jump the camera outright )
\   cam-stop
\
\ HOW THE FINE SCROLL WORKS, because the arithmetic looks
\ arbitrary until you see it. The vic can shift the display
\ RIGHT by 0-7 pixels ($d016) and DOWN by 0-7 ($d011), and that
\ is all it can do - there is no shift left and no shift up. So
\ to move the world LEFT by one pixel you step the character
\ window one cell on (8 pixels left) and shift 7 back to the
\ right. Net: one pixel. Every 8 pixels the cell steps again
\ and the offset snaps back, and the join is invisible.
\
\ Both axes are the same rule around a different neutral. x sits
\ at 0 and y at 3 - $d011's rest value is $1b, yscroll 3 - so
\ for a camera at cam pixels and a neutral N:
\
\   cell   = (cam + 7 - N) >> 3      -> 7 for x, 4 for y
\   scroll = (N - cam) & 7
\
\ THE EDGE COLUMN IS A LIE, and col38/row24 cover it. When the
\ offset is mid-cell the strip coming in at the edge is only
\ part drawn; narrowing the display makes the vic paint border
\ over exactly that strip. cam-start turns both on, which is
\ why the screen goes a character narrower when it starts.
\
\ WHAT IT COSTS, measured rather than guessed. The window is
\ redrawn from the map only when the camera crosses a cell
\ boundary, so 7 frames in 8 are one register write and free.
\ The 8th copies win-h rows, twice over if the map has colour.
\ Timed on the cart, per redraw:
\
\   40x25 with colour   43 ms   ~2 frames
\   40x25 no colour     19 ms   ~1 frame
\   40x16 no colour     11 ms   ~half a frame
\
\ So the colour map is not a detail - it is HALF THE COST, and
\ dropping it is the single biggest thing you can do. At 40x25
\ in colour the redraw overruns its frame and the scroll pauses
\ for two every eighth pixel; in one colour it just fits; a
\ shorter window fits with room to spare. Scroll a pixel every
\ other frame and even the worst case reads as smooth, because
\ the stall is a smaller share of the time between steps.
\
\ NOT FOR AN INTERRUPT. cam! redraws with do/loop, which is
\ illegal in a callback - it tsx'es onto the cpu stack and
\ collides with the private data-stack window. Drive it from
\ the foreground, one step a frame.

\ charset for scr-at / col-at, scroll for xscroll! yscroll! col38 row24.
\ Bare on purpose - see charset.fs.
require charset
require scroll

base @ hex

( All values, not variables: a value reads back in a couple of
  instructions where a variable costs an @ on top, and map-w is
  read once per row of every redraw. Mind that durexForth's
  "variable" is itself a value holding an address - so "to" on
  one would move the storage rather than write it. )
0 value map             \ w*h character codes
0 value map-c           \ w*h colours, or 0 for none
0 value map-w
0 value map-h

0 value win-x  0 value win-y    \ where the window sits on screen
0 value win-w  0 value win-h
0 value cam-x  0 value cam-y    \ the camera, in PIXELS into the map

: map! ( addr w h -- ) to map-h to map-w to map ;
: map-col! ( addr -- ) to map-c ;
: win! ( x y w h -- ) to win-h to win-w to win-y to win-x ;

\ --- where the camera lands ------------------------------------

: cam-col ( -- n ) cam-x 7 + 3 rshift ;
: cam-row ( -- n ) cam-y 4 + 3 rshift ;
: cam-fx  ( -- n ) cam-x negate 7 and ;
: cam-fy  ( -- n ) 3 cam-y - 7 and ;

\ --- redraw ----------------------------------------------------

0 value r-row

: cam-draw ( -- )       \ blit the camera's view of the map
  win-h 0 do
    cam-row i + map-w * cam-col + to r-row
    r-row map +  win-x win-y i + scr-at  win-w move
    map-c if
      r-row map-c +  win-x win-y i + col-at  win-w move
    then
  loop ;

\ --- moving it -------------------------------------------------

-1 value last-col
-1 value last-row

: cam! ( x y -- )       \ put the camera at pixel x,y
  to cam-y  to cam-x
  cam-col last-col <>  cam-row last-row <>  or if
    cam-draw
    cam-col to last-col
    cam-row to last-row
  then
  cam-fx xscroll!
  cam-fy yscroll! ;

( The camera may not walk off the map: the window would fill
  with whatever happens to lie past the end of it. )
: cam-max-x ( -- n ) map-w win-w - 3 lshift ;
: cam-max-y ( -- n ) map-h win-h - 3 lshift ;

: cam-clamp ( x y -- x' y' )
  0 max cam-max-y min
  swap
  0 max cam-max-x min
  swap ;

: cam+ ( dx dy -- )     \ move the camera, staying on the map
  cam-y + swap cam-x + swap cam-clamp cam! ;

\ --- starting and stopping -------------------------------------

: cam-start ( -- )
  col38 row24           \ hide the part-drawn edges
  -1 to last-col        \ nothing drawn yet: force the first redraw
  -1 to last-row
  cam-x cam-y cam! ;

: cam-stop ( -- )
  col40 row25
  0 xscroll!  3 yscroll! ;      \ 3 is $d011's rest value, not 0

hide r-row

base !
