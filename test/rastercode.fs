\ rastercode.fs - raster callback written in ASSEMBLY, so
\ it is fast enough for tight, beam-timed work (the colon-
\ threaded callback in test/raster.fs is too slow to re-arm
\ $d012 mid-frame; native code is not).
\
\ durexForth is subroutine-threaded: a colon word's body is
\ literally  jsr A / jsr B / ... / rts .  So machine code
\ laid down inside a colon body with  [ ... ]  runs NATIVELY
\ when the word is called -- no interpreter, just opcodes.
\
\ Two idioms are tested, both valid callbacks for raster! :
\   1. a standalone  code cb ... rts, end-code  word,
\   2. a colon word whose body is inline  [ asm ]  code.
\
\ Run:  include rastercode
\ Pass: writes  ok  and exits VICE.

include irq
include vic

base @ hex

variable rerr  0 rerr !

\ a memory cell the callbacks bump so the foreground can
\ see they fired.
create acnt 1 allot  0 acnt c!

\ --- idiom 1: standalone code word --------------------
\ pure machine code, ends in rts, ; call it as  ' acode .
code acode
acnt inc,               \ inc the counter
2 lda,# d020 sta,       \ red border below the split
rts,
end-code

\ --- idiom 2: colon word with inline [ asm ] ----------
\ Same effect, but the fast bytes live inside a normal
\ colon word.  [ leaves compile state so the assembler
\ runs at HERE, laying opcodes straight into the body; ]
\ resumes.  Because compilation is subroutine-threaded,
\ these bytes execute as native code, then the word's rts
\ (from ;) returns to the trampoline.
: bcode
  [ acnt inc,
    6 lda,# d020 sta, ] ;

\ crude foreground jiffy wait (do/loop in the foreground is
\ fine; only IRQ callbacks must avoid it).
: 1jiffy a2 c@ begin dup a2 c@ <> until drop ;
: waitj ( n -- ) 0 do 1jiffy loop ;

: try ( xt -- fired )    \ install xt on a line, run, count
  0 acnt c!
  80 swap raster!         \ fire on line $80
  #8 waitj
  raster-off
  acnt c@ ;               \ how many times it fired

: check
  #147 emit
  6 d020 c!  0 d021 c!

  \ idiom 1: standalone code word
  ['] acode try  #4 < if 1 rerr ! then

  \ idiom 2: colon word with inline asm
  ['] bcode try  #4 < if 2 rerr ! then

  \ must be cleanly off afterwards
  ?raster if 3 rerr ! then

  #147 emit
  rerr @ if
    ." rastercode FAIL err=" rerr @ . cr
  else
    ." rastercode OK" cr
    0 1 s" ok" saveb
  then ;

check

0 $d7ff c!

base !
