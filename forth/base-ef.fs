: 2+ 1+ 1+ ;
: jmp, 4c c, ;
: postpone bl word dup find ?dup 0= if
count notfound then
rot drop -1 = if [ ' literal compile,
' compile, literal ] then compile,
; immediate
: ['] ' postpone literal ; immediate
: [char] char postpone literal
; immediate
: else jmp, here 0 ,
swap here swap ! ; immediate
: until postpone 0branch , ; immediate
: again jmp, , ; immediate
: recurse
latestxt compile, ; immediate

: \ source >in ! drop ; immediate
: <> = 0= ;
: u> swap u< ;
: 0<> 0= 0= ;

: parse >r source >in @ /string
over swap begin dup while over c@ r@ <>
while 1 /string repeat then r> drop >r
over - dup r> if 1+ then >in +! ;

: ( source-id 0= if ')' parse drop drop
else begin >in @ ')' parse nip >in @ rot
- = while refill drop repeat then ;
immediate

: lits ( -- addr len )
r> 1+ count 2dup + 1- >r ;

( "0 to foo" sets value foo to 0 )
: (to) >r split r@ 2+ c! r> c! ;
: to ' 1+ state c@ if
postpone literal postpone (to) exit
then (to) ; immediate

: allot ( n -- ) here + to here ;

: s" ( -- addr len )
'"' parse state @ if postpone lits
dup c, tuck here swap move allot
then ; immediate

: ." postpone s" postpone type
; immediate
: .( ')' parse type ; immediate
.( compile base..)

: case 0 ; immediate
: (of) over = if drop r> 2+ >r exit
then branch ;
: of postpone (of) here 0 , ; immediate
: endof postpone else ; immediate
: endcase postpone drop
begin ?dup while postpone then
repeat ; immediate

( dodoes words contain:
 1. jsr dodoes
 2. two-byte code pointer. default: rts
 3. variable length data )
here 60 c, ( rts )
: create
header postpone dodoes literal , ;
: does> r> 1+ latest >xt 1+ 2+ ! ;

.( asm..)
parse-name asm included

: -rot rot rot ;

( creates value that is fast to read
  but can only be rewritten by "to".
   0 value foo
   foo . \ prints 0
   1 to foo
   foo . \ prints 1 )
: value ( n -- )
( TO relies on this lda/ldy order )
code split swap lda,# ldy,#
['] pushya jmp, ;
: constant value ;
( to free up space, pad could be
  e.g. HERE+34 instead )
$35b constant pad
: spaces ( n -- )
begin ?dup while space 1- repeat ;

8b value w
8d value w2
9e value w3

: hex 10 base ! ;
: decimal a base ! ;

: 2drop ( a b -- )
postpone drop postpone drop ; immediate


: save-forth ( strptr strlen -- )
801 $a000 d word count saveb ;

code 2/
msb lda,x 80 cmp,# msb ror,x lsb ror,x
rts, end-code
code or
msb lda,x msb 1+ ora,x msb 1+ sta,x
lsb lda,x lsb 1+ ora,x lsb 1+ sta,x
inx, rts, end-code
code xor
msb lda,x msb 1+ eor,x msb 1+ sta,x
lsb lda,x lsb 1+ eor,x lsb 1+ sta,x
inx, rts, end-code

:- dup inx, rts, end-code
code lshift ( x1 u -- x2 )
lsb dec,x -branch bmi,
lsb 1+ asl,x msb 1+ rol,x
latest >xt jmp,
code rshift ( x1 u -- x2 )
lsb dec,x -branch bmi,
msb 1+ lsr,x lsb 1+ ror,x
latest >xt jmp,

: variable
0 value
here latest >xt 1+ (to)
2 allot ;

( from FIG UK... )
: / /mod nip ;
: mod /mod drop ;
: */mod >r m* r> fm/mod ;
: */ */mod nip ;
( ...from FIG UK )

: .s depth begin ?dup while
dup pick . 1- repeat ;

: abort -1 throw ;
: abort" postpone if
postpone s" postpone (abort")
postpone then ; immediate

( linked list. each element contains
  backlink + hashed file name )
0 value (includes)

: marker ( -- )
(includes) latest here create , , ,
does> dup @ to here
   2+ dup @ to latest
   2+     @ to (includes) ;

: include parse-name included ;

: :noname here here to latestxt ] ;

marker ---modules---

.( wordlist..) include wordlist

\ hides private words
hide 1mi hide 2mi hide 23mi hide 3mi
hide latestxt
hide dodoes hide (abort")

.( labels..) include labels
.( doloop..) include doloop
.( sys..) include sys
.( debug..) include debug
.( ls..) include ls
.( require..) include require
.( open..) include open
.( accept..) include accept

\ --- EasyFlash cart resident set ---------------------------------
\ The editor v is NOT packed here.  Instead we make the game-dev core
\ + SID resident, so irq!, mouse, sprite and SID words work at boot
\ with no include.  Everything else (v, dos, file, str, timer, demos)
\ still loads with "include" - served from the cart's own flash by the
\ romfs driver in asm/cart-ef.asm, so NO disk is needed.  If a name is
\ not in flash the driver falls back to the real KERNAL, so a drive 8
\ keeps working too.  NOTE: that driver owns $c000-$c2b2.
\ timer is deliberately NOT resident: it defines "start", which shadows
\ the kernel's "start" (the pointer to the boot jsr operand at $081f).
\ turnkey is included below, so save-pack would compile that shadowed
\ "start" and write the newstart xt to the jiffy clock instead of $081f.
\ The image would then boot straight into PRINT_BOOT_MESSAGE, never run
\ restore-forth, and throw "full" on every line.  "include timer" after
\ boot is fine - save-pack has already run by then.
.( vic..) include vic
.( irq..) include irq
.( joy..) include joy
.( sprite..) include sprite
.( mouse..) include mouse
.( sid..) include sid
.( mml..) include mml
.( mmlirq..) include mmlirq
.( rnd..) include rnd

decimal

\ Reserve $9d00-$9fff for the cart's romfs driver (asm/cart-ef.asm), which
\ hooks the KERNAL so "include" reads from flash.  It has to live here:
\ $a000-$cbff is the editor text buffer (v.fs bufstart) and $cc00-$cfff is
\ hi-res colour, so a driver parked up there is eaten the moment you edit a
\ file or go hi-res.  So slide the whole dictionary down 768 bytes to end at
\ $9cff instead of $9fff.
\
\ This MUST happen BEFORE "include turnkey".  turnkey's ---turnkey--- marker
\ records latest when it is created, and restore-forth replays that recorded
\ value at boot.  Sliding afterwards (e.g. with "$9cff top!") leaves the
\ marker pointing 768 bytes into the moved dictionary, which silently drops
\ every word defined after it - bg-play, mouse-update and friends just stop
\ existing, with no error until you look one up.  Keep DRIVER in cart-ef.asm
\ in sync with the $9cff below.
$9fff latest - 1+       ( len )
latest                  ( len src )
dup $300 -              ( len src dst )
rot                     ( src dst len )
move
latest $300 - to latest

include turnkey
$9cff to top            \ where the dictionary now really ends

cr
.( cart: )
$4000 $6b - \ available ROM
here $801 - \ code + data
top 1+ latest - \ dictionary
$20 + + - \ save-pack padding
. .( bytes remain.) cr

.( save new durexforth..)
save-pack @0:durexforth
.( ok!) cr

0 $d7ff c! \ for vice -debugcart
