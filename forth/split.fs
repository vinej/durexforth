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
\ THE SEAM RULE. A band's registers are written in the line
\ ABOVE it (the handler explains why there is no better time),
\ so the tail of that line takes the new colours and scroll.
\ Make the seam a line that cannot show it: a solid row of
\ reverse spaces in a fixed colour, or the same colours on both
\ sides of the boundary. One designed row buys a pixel-clean
\ split everywhere else.
\
\ THE HANDLER IS ASSEMBLY, and it has to be. A forth callback
\ cannot do this job: by the time the interpreter had threaded
\ its way to the register writes the beam would be lines past
\ the split, and it could not re-arm $d012 for the next band
\ inside the same frame. Everything here happens between the
\ irq and the first pixel of the band.
\
\ bands-on OWNS THE INTERRUPT. It turns the Kernal's CIA timer
\ IRQ off and makes the VIC raster the only source, because the
\ two fighting is what makes a split flicker: the CIA handler
\ runs ~40 rasterlines with interrupts disabled, and when that
\ straddles a band's line the raster IRQ is serviced dozens of
\ lines late and the band is drawn a whole frame wrong. With the
\ CIA IRQ off the raster IRQ is serviced within a few cycles
\ every time, and the busy-wait to the exact line takes out the
\ rest. bands-off puts the CIA IRQ back.
\
\ While bands run there is therefore no jiffy clock and no Kernal
\ keyboard scan - drive timing from band-sync (below) and read
\ keys with kb? straight off the matrix. Per-frame game logic
\ goes in the foreground after band-sync, not in irq!, which the
\ CIA drove and which is now silent.

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
create band-fr   1 allot        \ frame counter, ticked once per frame
variable band-old               \ saved $314
0 value bands-live

0 band-n c!
0 band-i c!
0 band-fr c!

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

( ALL the registers land in the line ABOVE the band, and that is
  a considered retreat, not carelessness. Two vic facts force it:

  - the vic decides at cycle 14 of a line whether that line is a
    badline, and re-reads $d018 during the fetch that follows -
    so the mode registers must arrive before the band's first
    line begins. Early is the only correct time for them.

  - on a badline the vic STEALS THE CPU for cycles 12-54, and a
    band aligned to a character row starts ON a badline. The
    first version waited for the band's own line so the colours
    would land invisibly in its left border - and the stall held
    those writes until cycle ~55, two-thirds across the visible
    line, at a position that wobbled with interrupt jitter. A
    wiggling seam, drawn by the fix itself.

  Writing everything a line early is immune to all of that. The
  price is honest and documented: the TAIL of the line above the
  band takes the band's colours and scroll. Make that line
  something that cannot show it - a solid separator row of
  reverse spaces in a fixed colour, or matching colours across
  the seam - and the split is pixel-clean. splitdemo does it. )
band-d011 lda,x  d011 sta,
band-d018 lda,x  d018 sta,
band-d016 lda,x  d016 sta,
band-d020 lda,x  d020 sta,
band-d021 lda,x  d021 sta,

inx,                    \ on to the next band, round the frame
band-n cpx,
3 @@ bne,
0 ldx,#                 \ wrapped: we just serviced the last band,
band-fr inc,            \ so the frame is over - tick, in the lower
                        \ border, which is where band-sync releases
                        \ the foreground to move characters
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
  0 band-fr c!
  bands-live 0= if
    314 @ band-old !     \ save whatever is there ONCE
    'band-tramp 314 !
  then
  -1 to bands-live
  7f dc0d c!               \ CIA1 irq off: the jiffy must not race us
  dc0d c@ drop             \ ack any pending CIA irq
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
    81 dc0d c!             \ CIA1 timer-A irq back on (kernal default)
    band-old @ 314 !       \ back to whoever had it
    0 to bands-live
  then
  ei ;

: ?bands ( -- f ) bands-live ;

( Wait for the next frame, locked to the beam - one tick per
  frame, released in the lower border just after the last band.
  This is where a split scroller does its character move and
  updates the bands: the beam is below the playfield, so nothing
  tears, and band 0 does not re-read its scroll until the top of
  the next frame. It replaces waiting on the jiffy clock, which
  bands-on has stopped and which never tracked the beam anyway. )
: band-sync ( -- ) band-fr c@ begin dup band-fr c@ <> until drop ;

hide bx

base !
