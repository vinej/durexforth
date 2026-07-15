\ irq.fs - attach a forth word to the interrupt.
\
\ ' myword irq!   runs myword ~60x/sec in the
\ background while the foreground interpreter runs.
\ irq-off         stops it.
\
\ The callback runs at interrupt time. It MUST NOT:
\   - call the interpreter (include/evaluate)
\   - do disk / iec i/o
\   - throw
\   - run longer than one frame
\ It MAY: read joystick, poke vic/sid, update
\ variables, run the mml background tick.
\ Guard any variable shared with the foreground
\ using  di ... ei  (disable/enable interrupts).
\
\ The callback runs on a private data-stack window
\ (~13 cells), so it may push/pop freely but should be
\ stack-neutral overall.  x/a/y and the w/w2/w3 scratch
\ are saved and restored for you.  Keep foreground data
\ stack depth < ~42 cells while a callback is installed.

base @ hex

variable irq-xt         \ callback xt (0 = none)
variable irq-old        \ saved previous $314 vector
variable irq-jsr        \ address of the callback jsr operand
create irq-save 6 allot \ saved w/w2/w3 (6 bytes)
create irq-savex 1 allot \ saved foreground x register
0 irq-xt !              \ variable is not zero-initialised

\ irq-x = private data-stack pointer for the callback.
\ The foreground data stack grows downward from $3a
\ ($3b+x wraps mod 256); with x=$d5, $3b+$d5 = $10, so
\ the callback owns cells [$10..$03] while the
\ foreground owns [$3a..$11], keeping them disjoint as
\ long as foreground depth stays < ~42 cells.  We MUST
\ give the callback its own x: the foreground x at irq
\ time is not always the data-stack pointer (e.g. it is
\ the cpu stack pointer midway through (loop)).
d5 constant irq-x

\ --- di / ei : critical-section guards ---
code di sei, rts, end-code
code ei cli, rts, end-code

\ --- the trampoline installed into $314 ---
\ Fully transparent to the interrupted foreground:
\ saves x/a/y and the w/w2/w3 scratch, runs the
\ callback on a private data-stack window via a
\ self-modified jsr, restores everything, then chains
\ to the previous handler.
code irq-trampoline
irq-savex stx,          \ save foreground x
pha,                    \ save a
tya, pha,               \ save y

w    lda, irq-save    sta,
w 1+ lda, irq-save 1+ sta,
w2   lda, irq-save 2 + sta,
w2 1+ lda, irq-save 3 + sta,
w3   lda, irq-save 4 + sta,
w3 1+ lda, irq-save 5 + sta,

cld,                    \ ensure binary mode

irq-xt lda, irq-xt 1+ ora,
+branch beq,            \ no callback -> teardown
irq-x ldx,#             \ private data-stack window
here 1+ irq-jsr !       \ record the jsr operand address
0 jsr,                  \ call callback (operand patched by irq!)

:+                      \ teardown
irq-save    lda, w    sta,
irq-save 1+ lda, w 1+ sta,
irq-save 2 + lda, w2   sta,
irq-save 3 + lda, w2 1+ sta,
irq-save 4 + lda, w3   sta,
irq-save 5 + lda, w3 1+ sta,

pla, tay,               \ restore y
pla,                    \ restore a
irq-savex ldx,          \ restore foreground x

irq-old (jmp),          \ chain to previous handler
end-code

\ --- user-facing install / uninstall ---

\ trampoline entry, captured at load time.
' irq-trampoline constant 'irq-tramp

: irq! ( xt -- )
  di
  dup irq-jsr @ !          \ patch callback into the jsr
  irq-xt @ 0= if
    314 @ irq-old !         \ save old vector once
    'irq-tramp 314 !        \ install our trampoline
  then
  irq-xt !                 \ store callback
  ei ;

: irq-off ( -- )
  di
  irq-xt @ if
    irq-old @ 314 !         \ restore old vector
    0 irq-xt !
  then
  ei ;

: ?irq ( -- f ) irq-xt @ 0<> ;

\ ============================================================
\ raster! : run a forth word when the beam reaches a chosen
\ raster line, instead of at the 60Hz jiffy IRQ.
\
\   #100 ' myword raster!   \ myword fires on line 100
\   raster-off              \ stop, restore the jiffy IRQ
\
\ Same callback rules as irq! (see the header): no
\ interpreter, no i/o, no throw, one frame max, guard shared
\ variables with di/ei.  The callback still runs on the
\ private data-stack window, with x/a/y and w/w2/w3 saved.
\
\ How it coexists with the Kernal: we ADD the VIC raster IRQ
\ as a second source and leave the CIA jiffy IRQ running.
\ Every IRQ, the trampoline reads $d019 to see who fired:
\  - raster bit set  -> ack the VIC, run the callback, then
\                       pull a/x/y (pushed by $ff48) and rti.
\  - otherwise       -> chain to the old vector so the Kernal
\                       services the CIA (keyboard, clock...).
\ Acking the VIC means WRITING $d019 (writing a 1 clears the
\ latch); merely reading it leaves the line asserted and the
\ machine re-enters forever -- that was the classic hang.
\ ============================================================

variable raster-xt      \ raster callback xt (0 = none)
variable raster-old     \ saved previous $314 vector
variable raster-jsr     \ address of the raster callback jsr operand
0 raster-xt !           \ variable is not zero-initialised

\ --- the raster trampoline installed into $314 ---
code raster-trampoline
d019 lda,               \ read VIC irq status
1 and,#                 \ raster source (bit 0)?
+branch beq,            \ no -> not ours, chain to old vector

\ --- it is our raster IRQ ---
1 lda,# d019 sta,       \ ack: write bit0 of $d019 to clear latch

w    lda, irq-save    sta,
w 1+ lda, irq-save 1+ sta,
w2   lda, irq-save 2 + sta,
w2 1+ lda, irq-save 3 + sta,
w3   lda, irq-save 4 + sta,
w3 1+ lda, irq-save 5 + sta,

cld,                    \ ensure binary mode

raster-xt lda, raster-xt 1+ ora,
+branch beq,            \ no callback -> teardown
irq-x ldx,#             \ private data-stack window
here 1+ raster-jsr !    \ record the jsr operand address
0 jsr,                  \ call callback (operand patched by raster!)

:+                      \ teardown
irq-save    lda, w    sta,
irq-save 1+ lda, w 1+ sta,
irq-save 2 + lda, w2   sta,
irq-save 3 + lda, w2 1+ sta,
irq-save 4 + lda, w3   sta,
irq-save 5 + lda, w3 1+ sta,

pla, tay,               \ restore y (pushed by $ff48)
pla, tax,               \ restore x
pla,                    \ restore a
rti,                    \ return; we serviced this IRQ ourselves

:+                      \ not our source
raster-old (jmp),       \ chain to previous handler (Kernal CIA)
end-code

' raster-trampoline constant 'raster-tramp

\ raster! ( line xt -- )
\ Install the trampoline (once), enable the VIC raster IRQ,
\ and latch the compare line.  line is 0..311; bit 8 of the
\ line lives in $d011 bit7, which we clear (all supported
\ lines are < 256 for simplicity).
: raster! ( line xt -- )
  di
  dup raster-jsr @ !        \ patch callback into the jsr
  raster-xt @ 0= if
    314 @ raster-old !       \ save old vector once
    'raster-tramp 314 !      \ install our trampoline
    \ enable raster IRQ source in the VIC
    d011 c@ 7f and d011 c!   \ clear raster compare bit 8
    1 d01a c!                \ VIC irq mask: enable raster only
    7f d019 c!               \ ack any pending VIC irqs
  then
  raster-xt !               \ store callback
  d012 c!                   \ latch the compare line (low 8 bits)
  ei ;

: raster-off ( -- )
  di
  raster-xt @ if
    0 d01a c!                \ disable all VIC irqs
    7f d019 c!               \ ack any pending VIC irqs
    raster-old @ 314 !       \ restore old vector
    0 raster-xt !
  then
  ei ;

: ?raster ( -- f ) raster-xt @ 0<> ;

hide irq-save
hide irq-savex
hide irq-x

base !
