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

( Frames per pixel - and a real trade, because the vic has no
  sub-pixel scroll. 1 is the smoothest motion the machine can
  make and also the fastest, a brisk 60 pixels a second; the
  higher you go the slower and the more the eye sees the pixel
  steps as steps. Change it live:  2 to pspeed )
3 value pspeed

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

( The seam row: solid black, drawn with reverse spaces. This is
  the one designed row the split needs - the band's registers
  land in the tail of the line above it, and solid ink is the
  thing register changes cannot show through: not the background
  switch, not the scroll. See split.fs, THE SEAM RULE. )
: seam ( -- )
  #40 0 do
    #160 i panel-row 1- scr-at c!       \ 160 = reverse space
  loop
  0 #40 1 0 panel-row 1- tile-col! ;

: panel ( -- )
  #40 0 do #45 i panel-row scr-at c! loop   \ a rule along the top
  s" score 000000    lives 3" 2 #22 put
  1 #40 5 0 panel-row tile-col! ;

0 value fine                    \ pixels moved since the last wrap
0 value fc                      \ frames since the last pixel

( The order in the wrap branch is deliberate, and it is the
  demo's one piece of real timing. band-sync releases us at
  ~line 212, and two deadlines follow: band 0 re-reads its
  scroll register at line 0, ~51 lines away - but the beam does
  not redraw the text rows until line ~123 of the NEXT frame,
  ~170 lines away. The register write is tiny and the 3-row
  character move is most of the short budget, so the register
  goes FIRST and the characters take the long one. The other
  way round, a slightly slow move leaves the new offset a frame
  behind the new characters: an 8-pixel stutter at every wrap. )
: h-step ( -- )
  fc 1+ to fc
  fc pspeed < if exit then
  0 to fc
  fine 1+ to fine  fine 7 and to fine
  fine 0 band-xscroll!          \ band 0 only; the panel is band 1
  fine 0= if 0 #9 #40 3 wrap-right then ;

: splitdemo ( -- )
  1 curflag c!
  0 to col-scroll               \ blue and white are each uniform: the
                                \ wrap never needs to touch colour ram
  page
  6 d021 c!  0 d020 c!          \ blue screen, black border
  playfield  seam  panel

  ( Both bands carry the SAME border. $d020 is written at the
    seam like everything else, and a border change lands as a
    mid-line notch in the side border, wobbling with interrupt
    jitter. A border that does not change cannot notch. )
  bands-clear
  6 d021 c!  0 d020 c!  col38  0 xscroll!   \ band 0: the playfield
  #1 band+
  0 d021 c!  0 d020 c!  col38  0 xscroll!   \ band 1: the panel
  panel-line band+
  bands-on

  0 to fine  0 to fc
  begin band-sync h-step k-stop kb? until

  bands-off
  0 xscroll! col40
  6 d021 c!  e d020 c!          \ back to the boot look
  -1 to col-scroll              \ restore the default
  page 0 curflag c! ;

base !
