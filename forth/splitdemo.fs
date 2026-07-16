\ splitdemo.fs - a playfield that scrolls above a panel that
\ does not.
\
\   include splitdemo
\   splitdemo        \ run/stop quits
\
\ This is the split screen every C64 game has, and the point of
\ it is the one register both halves want and only one can
\ have. $d016 holds the fine x-scroll for the WHOLE frame, so
\ scrolling the playfield with it drags the score panel along
\ too - unless something changes $d016 back in the few
\ microseconds between the two. That something is a raster
\ interrupt, and split.fs is it.
\
\ Watch the join: the grid and the sentence glide left, the
\ panel underneath never moves a pixel. Both are on one screen
\ sharing one scroll register.

require split
require scroll
require keyb

base @ hex

#20 constant panel-row          \ the panel starts here...
#211 constant panel-line        \ ...which is this raster line:
                                \ the display opens at line 51,
                                \ so char row N starts at 51+8N.

cc constant curflag             \ non-zero stops the cursor blinking

( Frames per pixel. The panel proves itself either way, but
  slow enough to read is the point. )
#4 value pspeed

: p>s ( c -- c' ) dup #64 < if exit then #64 - ;   \ petscii -> screen

0 value pp
: put ( addr len x y -- )
  scr-at to pp
  0 do dup i + c@ p>s  pp i + c! loop drop ;

: playfield ( -- )      \ rows 0..19: a grid that shows the motion
  panel-row 0 do
    #40 0 do
      i 3 and 0= if #43 else #32 then   \ '+' every fourth column
      i j scr-at c!
      #14 i j col-at c!
    loop
  loop
  s" this half glides " 2 #9 put
  s" the one below does not " 2 #11 put ;

: panel ( -- )          \ rows 20..24: the part that must hold still
  #40 0 do
    #45 i panel-row scr-at c!           \ a rule along the top
  loop
  s" score 000000    lives 3" 2 #22 put
  #5 0 do
    #40 0 do  1 i panel-row j + col-at c!  loop
  loop ;

0 value fine            \ pixels since the last cell step
0 value fc              \ frames since the last pixel

: frame ( -- ) a2 c@ begin dup a2 c@ <> until drop ;

: step ( -- )
  fc 1+ to fc
  fc pspeed < if exit then
  0 to fc
  fine 1+ to fine
  fine 8 = if 0 to fine  0 0 #40 panel-row wrap-right then
  ( band 0 only. Setting $d016 here instead would work for a
    single frame and then be overwritten by the next split -
    the bands own the register now. )
  fine 0 band-xscroll! ;

: splitdemo ( -- )
  1 curflag c!
  page
  playfield
  panel

  bands-clear
  ( Band 0 - the playfield. col38 is part of the band, because
    it lives in $d016 too: it makes the vic paint border over
    the column that is only half scrolled in. )
  6 d021 c!  e d020 c!  col38  0 xscroll!
  #1 band+
  ( Band 1 - the panel. Same snapshot, taken with the scroll
    back at zero. That single difference is the whole demo. )
  0 d021 c!  0 d020 c!  col38  0 xscroll!
  panel-line band+
  bands-on

  0 to fine  0 to fc
  begin frame step k-stop kb? until

  bands-off
  0 xscroll! col40
  6 d021 c!  e d020 c!
  page 0 curflag c! ;

base !
