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

\ A raster-line-locked variant (raster!/raster-off) is
\ planned but not shipped yet: cleanly sharing the VIC
\ raster IRQ with the running Kernal CIA IRQ needs more
\ care than the 60Hz hook above.  The 60Hz irq! is
\ enough for background music, sprite movement, and
\ game logic.

hide irq-save
hide irq-savex
hide irq-x

base !
