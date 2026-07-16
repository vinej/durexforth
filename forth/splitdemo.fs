\ splitdemo.fs - a message that scrolls above a panel that does
\ not.
\
\   include splitdemo
\   splitdemo        \ run/stop quits
\
\ The split screen every C64 game has, and the point of it is
\ the one register both halves want and only one can have.
\ $d016 holds the fine x-scroll for the WHOLE frame, so scrolling
\ the top with it would drag the panel along too - unless a
\ raster interrupt puts $d016 back in the few microseconds
\ between the two bands. split.fs does exactly that.
\
\ Three things make this read cleanly, and all three were wrong
\ in the first version:
\
\ 1. TIMING IS LOCKED TO THE BEAM, not the jiffy clock. bands-on
\    turns the clock off; band-sync waits for the raster handler
\    to tick once a frame, in the lower border. So the stepping
\    is one pixel per frame exactly, on PAL or NTSC, and the
\    character move happens below the playfield where it cannot
\    tear - and before band 0 re-reads its scroll at the top of
\    the next frame, so the offset and the characters always
\    agree. Out of step, that disagreement is the classic
\    one-frame jump right, then back left.
\
\ 2. ONLY THE TEXT ROWS ARE WRAPPED. The playfield is solid
\    blue, and fine-scrolling a solid colour is invisible, so
\    the fine scroll can shift the whole band while the coarse
\    wrap touches just the three rows that carry the message.
\    Wrapping all twenty would overrun the border window and
\    tear; three rows finish with time to spare.
\
\ 3. THE PANEL NEVER FLICKERS, because bands-on owns the
\    interrupt - no CIA jiffy racing the raster (see split.fs).

require split
require scroll
require keyb

base @ hex

#20 constant panel-row          \ the panel starts on this char row
#211 constant panel-line        \ = raster 51 + 8*20, its first line
                                \ (the display opens at line 51, the
                                \ same on PAL and NTSC, so this split
                                \ lands in the right place on both)

cc constant curflag             \ non-zero stops the cursor blinking

( Frames per pixel: bigger is slower. 3 reads well; 1 is a fast
  scroller, 8 an unhurried banner. )
#3 value pspeed

: p>s ( c -- c' ) dup #64 < if exit then #64 - ;   \ petscii -> screen

0 value pp
: put ( addr len x y -- )
  scr-at to pp
  0 do dup i + c@ p>s  pp i + c! loop drop ;

: playfield ( -- )
  \ solid blue; the message sits on rows 9-11, and because the
  \ empty rows are a single colour the fine scroll leaves them
  \ visibly unchanged while it glides the text
  s" this half glides right ->" 2 #9 put
  s" the panel below stays put" 2 #11 put
  1 #40 3 0 #9 tile-col! ;      \ white, so wrapping chars (not
                                \ colour) keeps the colour uniform

: panel ( -- )
  #40 0 do #45 i panel-row scr-at c! loop   \ a rule along the top
  s" score 000000    lives 3" 2 #22 put
  1 #40 5 0 panel-row tile-col! ;

0 value fine                    \ pixels moved since the last wrap
0 value fc                      \ frames since the last pixel

: h-step ( -- )
  fc 1+ to fc
  fc pspeed < if exit then
  0 to fc
  fine 1+ to fine
  fine 8 = if 0 to fine  0 #9 #40 3 wrap-right then
  fine 0 band-xscroll! ;        \ move band 0 only; the panel is band 1

: splitdemo ( -- )
  1 curflag c!
  0 to col-scroll               \ blue and white are each uniform: the
                                \ wrap never needs to touch colour ram
  page
  6 d021 c!  e d020 c!          \ blue screen, light border...
  playfield  panel

  bands-clear
  6 d021 c!  e d020 c!  col38  0 xscroll!   \ band 0: the playfield
  #1 band+
  0 d021 c!  0 d020 c!  col38  0 xscroll!   \ band 1: the panel
  panel-line band+
  bands-on

  0 to fine  0 to fc
  begin band-sync h-step k-stop kb? until

  bands-off
  0 xscroll! col40
  6 d021 c!  e d020 c!
  -1 to col-scroll              \ restore the default
  page 0 curflag c! ;

base !
