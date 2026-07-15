\ stablebars.fs - stabilised raster colour bars.  *** WIP ***
\
\ STATUS: EXPERIMENTAL / NOT WORKING YET.  The stabiliser
\ idea is sound and, when it renders, the bars are provably
\ zero-jitter (measured pixel-identical across frames, vs the
\ ~1px wobble of forth/rasterbars.fs).  BUT the multi-bar
\ draw LOOP hangs the handler: single and unrolled two-bar
\ spins run fine and recover cleanly, yet the backward-branch
\ loop over the bars wedges before painting bar 0, for a
\ timing reason that could not be pinned down with screenshot
\ debugging alone.  Needs a real VICE monitor single-step to
\ finish.  For a working steady-ish raster use rasterbars.
\
\   include stablebars
\   sbars       \ (currently hangs -- see STATUS above)
\   stop-sbars
\
\ DESIGN (for whoever picks this up):
\ Coexists with the KERNAL like rasterbars -- the CIA jiffy
\ IRQ keeps running and we ADD the VIC raster IRQ as a second
\ source; stop-sbars only disables the VIC IRQ and restores
\ $314 (never touches the CIA), so recovery is trivial.  On
\ the raster IRQ we mean to run the classic
\   lda $d012 / cmp $d012 / beq
\ double-read stabiliser to absorb the 0-7 cycle entry skew,
\ then draw all bars in one IRQ, each pinned to its line by a
\   :- d012 lda, sbars-line cmp, -branch bcc,   (line >= target)
\ spin.  The single-bar form of that spin works; the loop
\ does not.  Suspect the interaction of the nested backward
\ branches with the per-bar timing.  The assembly touches
\ only a/x/y and its own sbars-line cell (never the shared
\ w/w2/w3 scratch -- clobbering w crashed an earlier draft).

require vic

base @ hex

#8  constant nbars
#16 constant bar-h
#40 constant first-line        \ top bar's raster line

create colours nbars allot     \ one colour per bar
create sbars-line 1 allot      \ running target line (IRQ-private)
variable sbars-old             \ saved $314 vector

: init-colours ( -- )
  6 colours 0 + c!  e colours 1 + c!  4 colours 2 + c!
  e colours 3 + c!  3 colours 4 + c!  d colours 5 + c!
  1 colours 6 + c!  d colours 7 + c! ;

code di sei, rts, end-code
code ei cli, rts, end-code

\ --- the stabilised IRQ handler -------------------------
\ Entered via $ff48 (a/x/y already pushed).  Discriminate:
\ VIC raster -> stabilise, draw, rti; else -> chain to the
\ KERNAL so the jiffy clock and keyboard keep working.
code sbars-irq
d019 lda, 1 and,#              \ VIC raster source?
+branch beq,                   \ no -> chain out (CIA path)
1 lda,# d019 sta,              \ ack the VIC raster IRQ

\ stabiliser: wait to the first bar line, then a $d012
\ double-read pins us to a fixed cycle.
:- d012 lda, first-line cmp,#
-branch bne,
d012 lda, d012 cmp,            \ double-read: 0/1 cycle by phase
+branch beq,                   \ eat the last jitter cycle
:+

\ draw the bars, now cycle-exact.
0 ldy,#
first-line lda,# sbars-line sta,
:-
  :- d012 lda, sbars-line cmp,
  -branch bne,
  colours lda,y d020 sta,
  sbars-line lda, clc, bar-h adc,# sbars-line sta,
  iny,
  nbars cpy,#
-branch bne,

e lda,# d020 sta,             \ plain border below the bars

pla, tay,                      \ our IRQ: pull a/x/y and rti
pla, tax,
pla,
rti,

:+                             \ not our source
sbars-old (jmp),               \ chain to the KERNAL jiffy handler
end-code

' sbars-irq constant 'sbars-irq

: sbars ( -- )
  #147 emit
  init-colours
  6 d021 c!                    \ blue screen
  di
  314 @ sbars-old !            \ save current $314 vector
  'sbars-irq 314 !             \ install our handler (CIA left on)
  d011 c@ 7f and d011 c!       \ raster compare bit 8 = 0
  first-line d012 c!           \ compare line
  1 d01a c!                    \ enable VIC raster IRQ
  7f d019 c!                   \ ack pending VIC IRQ
  ei ;

: stop-sbars ( -- )
  di
  0 d01a c!                    \ disable VIC raster IRQ
  7f d019 c!                   \ ack VIC
  sbars-old @ 314 !            \ restore the previous $314 vector
  e d020 c!  6 d021 c!         \ plain screen
  ei ;

base !
