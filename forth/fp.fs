\ fp.fs - floating point, on a float stack of its own.
\
\ Floats live on their own stack, as they do in every Forth that
\ takes them seriously: the data stack carries addresses and
\ flags, the float stack carries the numbers. So where a cell has
\
\   0<  0<>  0>  0=  <  >  <>  .  !  @
\
\ a float has the same ten with an f on the front, and none of
\ them disturbs the data stack beyond an address or a flag.
\
\   #7 s>f  #2 s>f  f/  f.        \ prints 3.5
\   #1 s>f  #2 s>f  f<  .         \ prints -1
\
\ WHY THIS IS NOT forth/float.fs. That module says of itself that
\ it "cannot be used safely", and it is right. Basic's floating
\ point accumulators sit at $61-$70 and its string pointer at
\ $22, while durexForth's parameter stack is $03-$72 - so every
\ basic fp call lands on top of the data stack. float.fs gets
\ away with it only while the stack happens to be shallow.
\
\ The whole fix is fbasic below: ONE assembly primitive that
\ saves $61-$70, banks basic in, makes the call, banks out and
\ puts the zero page back. It touches the data stack nowhere in
\ between - in assembly it does not need to - so nothing of ours
\ is live in basic's registers while basic runs. Every float word
\ goes through it.
\
\ A float is basic's own 5-byte format, so anything the rom can
\ do is available: f. is FOUT, f< is FCOMP, s>f is GIVAYF.
\
\ Needs basic rom, which fbasic banks in around each call.

\ 0> lives there, and f> needs it. Bare on purpose - see charset.fs.
require compat

base @ hex

8 constant fdepth               \ floats the stack holds
create fstk fdepth 5 * allot
create fzp #16 allot            \ $61-$70 parked while basic runs
create fzero 0 c, 0 c, 0 c, 0 c, 0 c,   \ basic's zero: exponent 0
0 value fsp                     \ floats on the float stack

: ftop ( -- addr ) fsp 1- 5 * fstk + ;
: f2nd ( -- addr ) fsp 1- 1- 5 * fstk + ;  \ durexForth has no 2-
: fpush ( -- addr ) fsp 5 * fstk +  fsp 1+ to fsp ;
: fdrop ( -- ) fsp 1- to fsp ;
: fdepth? ( -- n ) fsp ;

\ --- the one dangerous primitive, contained -------------------
\ Patched by fcall, then run with the data stack untouched.

variable fb-a1  variable fb-y1      \ operand loaded into fac1
variable fb-a2  variable fb-y2      \ operand the routine works with
variable fb-j   variable fb-r

( Everything happens inside ONE call, and it has to. Basic's fac1 IS
  the result register, so restoring $61-$70 afterwards would throw the
  answer away - and fac1 cannot survive between two calls either. So:
  load fac1, run the routine, and pack the result down to $57-$5b,
  which is outside the parked range and therefore survives. Every
  operation is movfm of op1, then the routine with op2 - which covers
  all of them: givayf ignores op1, fout ignores op2, fadd and fcomp
  use both. )
code fbasic ( -- )
txa, pha,                       \ [1] forth stack pointer
01 lda, pha,                    \ [2] memory config

0f ldx,#                        \ park $61-$70 (16 bytes)
1 @:
61 lda,x  fzp sta,x  dex,  1 @@ bpl,

01 lda, 03 ora,# 01 sta,        \ basic rom in

here 1+ fb-a1 !  0 lda,#        \ fac1 = (op1)
tax,
here 1+ fb-y1 !  0 ldy,#
bba2 jsr,

here 1+ fb-a2 !  0 lda,#        \ fac1 = (op2) op fac1
tax,
here 1+ fb-y2 !  0 ldy,#
here 1+ fb-j !   0 jsr,
fb-r sta,                       \ keep a, for fcomp

bbca jsr,                       \ pack fac1 -> $57-$5b: it survives

pla, 01 sta,                    \ [2] memory config back

0f ldx,#                        \ zero page back
2 @:
fzp lda,x  61 sta,x  dex,  2 @@ bpl,

pla, tax,                       \ [1] forth stack pointer back
rts, end-code

: fcall ( a1 y1 a2 y2 routine -- )
  fb-j @ !  fb-y2 @ c!  fb-a2 @ c!  fb-y1 @ c!  fb-a1 @ c!  fbasic ;

: fop ( f1 f2 routine -- )      \ fac1 = (f1) ; fac1 = (f2) op fac1
  >r swap split rot split r> fcall ;

\ --- basic rom entry points ----------------------------------
bba2 constant movfm     \ fac1 <- (a/y)
b867 constant b-fadd    \ fac1 <- (a/y) + fac1
b850 constant b-fsub    \ fac1 <- (a/y) - fac1
ba28 constant b-fmul    \ fac1 <- (a/y) * fac1
bb0f constant b-fdiv    \ fac1 <- (a/y) / fac1
bc5b constant b-fcomp   \ a <- 0 / 1 / $ff
bddd constant b-fout    \ fac1 -> string at $0100
b391 constant b-givayf  \ fac1 <- signed int in a(hi)/y(lo)

: fac> ( -- ) 57 fpush 5 move ; \ $57 holds fac1, packed by fbasic

\ --- getting numbers in and out ------------------------------

: s>f ( n -- )                  \ integer -> float
  fzero split rot split swap b-givayf fcall  fac> ;

: f. ( -- )                     \ print and drop the top float
  ftop fzero b-fout fop
  #256 begin dup c@ ?dup while emit 1+ repeat drop
  space fdrop ;

: f@ ( addr -- ) fpush 5 move ;         \ memory -> float stack
: f! ( addr -- ) ftop swap 5 move fdrop ; \ float stack -> memory
: fdup ( -- ) ftop fpush 5 move ;

\ --- arithmetic ----------------------------------------------
\ Basic's fadd/fsub/fmul/fdiv all compute (a/y) op fac1, so load
\ the TOP into fac1 and pass the SECOND: that gives 2nd-top and
\ 2nd/top the right way round, which is what a stack wants.

: (fop) ( routine -- )          \ fac1 = top ; fac1 = 2nd op fac1
  >r ftop f2nd r> fop
  fdrop fdrop fac> ;

: f+ ( -- ) b-fadd (fop) ;
: f- ( -- ) b-fsub (fop) ;
: f* ( -- ) b-fmul (fop) ;
: f/ ( -- ) b-fdiv (fop) ;

\ --- comparisons ---------------------------------------------
\ fcomp returns 0 equal, 1 if fac1 > memory, $ff if fac1 < it.

: fsign ( -- n ) fb-r c@ dup 7f > if #256 - then ;

: (fcmp) ( -- n )               \ sign of 2nd - top; drops both
  f2nd ftop b-fcomp fop
  fdrop fdrop fsign ;

: (f0cmp) ( -- n )              \ sign of top; drops it
  ftop fzero b-fcomp fop
  fdrop fsign ;

: f<  ( -- f ) (fcmp) 0< ;
: f>  ( -- f ) (fcmp) 0> ;
: f=  ( -- f ) (fcmp) 0= ;
: f<> ( -- f ) (fcmp) 0<> ;

: f0<  ( -- f ) (f0cmp) 0< ;
: f0>  ( -- f ) (f0cmp) 0> ;
: f0=  ( -- f ) (f0cmp) 0= ;
: f0<> ( -- f ) (f0cmp) 0<> ;

base !
