\ sidmusic.fs - play a GoatTracker / PSID tune under the irq.
\
\ A .sid file is a 124-byte PSID header followed by the C64
\ payload - and that payload already starts with its own
\ 2-byte load address, so once the header is off it IS a prg
\ and loadb takes it as-is. Strip the header on the host:
\
\   dd if=tune.sid bs=1 skip=124 of=tune.prg
\
\ (Check the header first: dataOffset is the 16-bit big-endian
\ word at offset 6, and is $7c for PSID v2. GoatTracker's own
\ "pack" can emit the prg directly and skip all this.)
\
\ The payload holds a player with two entry points, almost
\ always "jmp init" at the load address and "jmp play" three
\ bytes on. Call init once, then play once a frame:
\
\   include sidmusic
\   s" tune" $5000 sid-load
\   0 sid-init
\   sid-on
\   ( ...tune plays while you work... )
\   sid-off
\
\ WHERE THE TUNE MUST LIVE. This is the part that bites.
\ durexForth's code runs from $0801 up to here (about $4a9a),
\ its dictionary comes down from $9cff, and the cart's romfs
\ driver owns $9d00-$9f27. That leaves roughly $4b00-$8c00
\ free. GoatTracker defaults to $1000, which lands squarely on
\ top of the interpreter and takes it out - and a sid player
\ cannot simply be loaded somewhere else, its addresses are
\ baked in. Build or pack the tune for ~$5000 and it fits.

require sys
require irq

base @ hex

( The irq trampoline can only jsr an address, so give it a
  native "jmp <play>" to jsr into: play's own rts then returns
  through it. This must be machine code, not a colon word -
  same reason rasterbars' callback is assembly. )
here 4c c, 0 c, 0 c, constant sid-jmp

0 value sid-init-addr
0 value sid-play-addr

: sid! ( init play -- )   \ point at a tune's two entry points
  dup to sid-play-addr  sid-jmp 1+ !
  to sid-init-addr ;

( Load a tune and assume the usual layout: init at the load
  address, play three bytes after it. If yours differs - some
  tunes put init at the END of the payload - follow sid-load
  with an explicit "init play sid!". )
: sid-load ( nameaddr namelen dst -- )
  dup >r
  loadb 0= abort" sid load failed"
  r> dup dup 3 + sid! ;

: sid-init ( song -- ) ar c!  sid-init-addr sys ;

: sid-on ( -- )  sid-jmp irq! ;
: sid-off ( -- ) irq-off  0 d418 c! ;   \ stop, then volume 0

base !
