\ split.fs - cut the screen into bands, each with its own vic
\ settings.
\
\ One C64 screen, several looks: a scrolling playfield above a
\ status panel that does not move, a bitmap half over a text
\ half, a different charset top and bottom. The vic has one set
\ of registers for the whole frame, so the only way to have two
\ is to change them while the beam is between the bands - which
\ means an interrupt at a chosen raster line, every frame.
\
\ You describe a band by SETTING THE SCREEN UP THE WAY YOU WANT
\ IT and saying where it starts. band+ snapshots the vic as it
\ is right now, so there is no table of magic numbers to get
\ wrong:
\
\   include split
\   bands-clear
\   6 d021 c!  col38          ( the playfield: blue, 38 cols )
\   #1 band+                  ( ...from the top of the frame )
\   0 d021 c!  col40          ( the panel: black, 40 cols )
\   #211 band+                ( ...from char row 20 down )
\   bands-on
\   ( ...the two halves now look different... )
\   bands-off
\
\ Each band carries $d011, $d016, $d018, $d020 and $d021: the
\ scroll offsets, the graphics mode, the screen and charset
\ pointers, and the two colours. That is everything that makes
\ a band look like itself. A register a band does not care
\ about just keeps whatever value the snapshot caught.
\
\ Change a band while it is running with the band-* setters -
\ that is how a split scroller works, and the reason this
\ module exists:
\
\   fine 0 band-xscroll!      ( band 0 glides, band 1 does not )
\
\ WHERE THE BANDS CAN GO. Lines 1 to 255, and at least two
\ apart. Line 0 is out because a band is armed one line early
\ (see the handler), and adjacent bands are out because the
\ raster compare cannot fire twice on the same line - the
\ second band would be a frame late.
\
\ THE HANDLER IS ASSEMBLY, and it has to be. A forth callback
\ cannot do this job: by the time the interpreter had threaded
\ its way to the register writes the beam would be lines past
\ the split, and it could not re-arm $d012 for the next band
\ inside the same frame. Everything here happens between the
\ irq and the first pixel of the band.
\
\ It coexists with irq! and sid-on, but only in this order:
\ install those FIRST, bands-on LAST, and take them off in
\ reverse. The band handler chains to whatever was in $314
\ when bands-on ran, so it has to be the outermost one.

\ irq for di / ei, charset for vic-base (band-charset!, band-screen!).
\ Bare on purpose - see charset.fs.
require vic
require irq
require charset

base @ hex

8 constant max-bands

create band-line #8 allot       \ raster line the band starts on
create band-d011 #8 allot       \ y-scroll, mode, rows
create band-d016 #8 allot       \ x-scroll, mode, cols
create band-d018 #8 allot       \ screen + charset
create band-d020 #8 allot       \ border
create band-d021 #8 allot       \ background
create band-i    1 allot        \ band the next irq belongs to
create band-n    1 allot        \ bands defined
variable band-old               \ saved $314
0 value bands-live

0 band-n c!
0 band-i c!

( The band handler. Runs entirely off the tables above, with x
  as the band index - no forth, no data stack, nothing that
  could take longer than the beam allows. )
code band-tramp
d019 lda,               \ vic irq status
1 and,#                 \ raster source (bit 0)?
1 @@ beq,               \ no - not ours, chain on

1 lda,# d019 sta,       \ ack: WRITING the bit clears the latch.
                        \ reading alone leaves the line asserted
                        \ and the machine re-enters forever.

band-i ldx,

( The mode registers go first, and land in the line ABOVE the
  band. They have to. The vic decides at cycle 14 of a line
  whether that line is a badline, and if it is it re-reads
  $d018 during the fetch that follows - so a write arriving
  after cycle 14 is a whole line late and the row comes out
  holding the previous row's characters. We are still in the
  line above here, whose fetch is long done, so writing early
  is free: it is the next line that reads these. )
band-d011 lda,x  d011 sta,
band-d018 lda,x  d018 sta,

( Colour is the exact opposite. Written this early it would
  recolour the tail of the line above and leave a bright stub
  hanging off the band. So wait for the band's own line and
  write them in the left border, before any of it is drawn.
  The wait costs most of a rasterline and is worth it. )
band-line lda,x
2 @:
d012 cmp,
2 @@ bne,

band-d020 lda,x  d020 sta,
band-d021 lda,x  d021 sta,
band-d016 lda,x  d016 sta,

inx,                    \ on to the next band, round the frame
band-n cpx,
3 @@ bne,
0 ldx,#
3 @:
band-i stx,

band-line lda,x         \ arm it one line early, per the above
sec, 1 sbc,#
d012 sta,

pla, tay,               \ a/x/y back as $ff48 pushed them
pla, tax,
pla,
rti,                    \ we serviced this one ourselves

1 @:
band-old (jmp),         \ not ours: on to the kernal's cia handler
end-code

' band-tramp constant 'band-tramp

\ --- describing bands -----------------------------------------

0 value bx

: bands-clear ( -- ) 0 band-n c! ;

( Add a band starting at raster line, taking the vic exactly as
  it stands. Set the screen up to look the way the band should,
  then say where it begins. )
: band+ ( line -- )
  band-n c@ to bx
  bx max-bands < 0= abort" too many bands"
  dup 1 <    abort" band line below 1"
  dup #255 > abort" band line above 255"
  bx band-line + c!
  ( $d011 bit 7 does NOT read back what you wrote - it reads the
    beam's current raster bit 8. Snapshot it unmasked and the
    band arms the compare register with a stray high bit, and
    fires somewhere in the next field instead. )
  d011 c@ 7f and  bx band-d011 + c!
  d016 c@ bx band-d016 + c!
  d018 c@ bx band-d018 + c!
  d020 c@ bx band-d020 + c!
  d021 c@ bx band-d021 + c!
  bx 1+ band-n c! ;

\ --- changing a band while it runs -----------------------------

: band-bg!     ( col n -- ) band-d021 + c! ;
: band-border! ( col n -- ) band-d020 + c! ;

: band-xscroll! ( 0..7 n -- )
  >r 7 and  r@ band-d016 + c@ f8 and or  r> band-d016 + c! ;
: band-yscroll! ( 0..7 n -- )
  >r 7 and  r@ band-d011 + c@ f8 and or  r> band-d011 + c! ;

: band-charset! ( addr n -- )
  >r vic-base - b rshift 7 and 1 lshift
  r@ band-d018 + c@ f1 and or  r> band-d018 + c! ;
: band-screen! ( addr n -- )
  >r vic-base - a rshift f and 4 lshift
  r@ band-d018 + c@ 0f and or  r> band-d018 + c! ;

\ --- running ---------------------------------------------------

: bands-on ( -- )
  band-n c@ 0= abort" no bands defined"
  di
  0 band-i c!
  bands-live 0= if
    314 @ band-old !     \ save whatever is there ONCE
    'band-tramp 314 !
  then
  -1 to bands-live
  d011 c@ 7f and d011 c!   \ raster compare bit 8 = 0: our lines are all < 256
  1 d01a c!                \ vic irq mask: raster only
  7f d019 c!               \ ack anything already pending
  band-line c@ 1- d012 c!  \ arm band 0, a line early
  ei ;

: bands-off ( -- )
  di
  bands-live if
    0 d01a c!              \ no more vic irqs
    7f d019 c!
    band-old @ 314 !       \ back to whoever had it
    0 to bands-live
  then
  ei ;

: ?bands ( -- f ) bands-live ;

hide bx

base !
