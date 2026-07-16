\ charset.fs - character set and tile support.
\
\ The C64 is a character machine: most games build their
\ world out of an 8x8 charset rather than the bitmap, because
\ a screen is 1000 bytes instead of 8000 and scrolls for free.
\ This covers the pieces needed for that: pointing the VIC at
\ a screen/charset, getting an editable copy of the ROM font,
\ redefining characters, and stamping tiles onto the screen.

\ rdb (source pixel rows), sp-base.
\ Nothing may follow a require on its line - not even a comment.
\ include only advances TIB_PTR past a line it has NOT finished
\ reading, so a bare require costs nothing, while a commented one
\ costs the whole line. Three levels of that walks off the end of
\ the 88-byte TIB at $258, into the kernal's open-file tables, and
\ the include quietly stops interpreting.
require sprite

base @ hex

\ --- vic bank -------------------------------------------------
\ $dd00 bits 0-1 pick which 16K the VIC can see, and they are
\ INVERTED: %11 = bank 0 ($0000), %00 = bank 3 ($c000).
\ The VIC only ever sees its own bank, so a screen or charset
\ must live inside it. Colour ram is the exception: it is wired
\ at $d800 no matter what.

: vic-bank@ ( -- 0..3 ) dd00 c@ 3 and 3 xor ;
: vic-bank! ( 0..3 -- )
  3 and 3 xor  dd00 c@ fc and or  dd00 c! ;
: vic-base ( -- addr ) vic-bank@ e lshift ;   \ bank * $4000

\ --- screen and charset pointers ------------------------------
\ $d018 holds both, as offsets inside the current bank:
\ bits 4-7 = screen / $400, bits 1-3 = charset / $800.

: screen@ ( -- addr ) d018 c@ f0 and 6 lshift vic-base + ;
: charset@ ( -- addr ) d018 c@ 0e and a lshift vic-base + ;

: screen! ( addr -- )
  dup 3f8 + to sp-base  \ sprite pointers ride at screen+$3f8
  vic-base - a rshift f and 4 lshift
  d018 c@ 0f and or  d018 c! ;

: charset! ( addr -- )
  vic-base - b rshift 7 and 1 lshift
  d018 c@ f1 and or  d018 c! ;

\ --- the rom font ---------------------------------------------
\ The character generator rom is only reachable with CHAREN=0,
\ which swaps it in over the i/o area at $d000. That means no
\ i/o - and therefore no kernal irq, which acks the timer at
\ $dc0d - while it is banked in, hence the sei. $01 is saved
\ and put back rather than forced to a constant: durexForth
\ never sets it, so it is whatever the boot happened to leave.

d000 constant charrom     \ upper case + graphics
d800 constant charrom-lc  \ lower case + upper case

code cs-di sei, rts, end-code
code cs-ei cli, rts, end-code

: charrom> ( src dst -- )   \ copy one 2K set out of rom
  cs-di
  1 c@ >r
  32 1 c!                   \ charen=0, and keep ram at $a000
  800 move
  r> 1 c!
  cs-ei ;

hide cs-di
hide cs-ei

\ --- keeping the charset you chose ----------------------------
\ The KERNAL swaps between the upper- and lower-case ROM fonts
\ when SHIFT+C= is pressed, and it does it by flipping $d018 -
\ the very register charset! just set. Some hosts (MiSTer among
\ them) reach the same toggle from CAPS. With a custom charset
\ that is not a cosmetic annoyance: your font is swapped out for
\ a rom one mid-game and the screen turns to garbage. $0291 bit
\ 7 turns the swap off; call charset-lock before charset!.

: charset-lock ( -- )   80 291 c! ;   \ $0291 bit7: shift+C= ignored
: charset-unlock ( -- ) 0 291 c! ;

\ --- characters -----------------------------------------------

: chardef ( c -- addr ) 8 * charset@ + ;

( Redefine a character from pixel rows in the source, exactly
  like sprite's sp-data: any non-"." is a set pixel.
    41 chardef char-data
    ..####..
    .#....#.
    ... 8 rows ... )
: char-data ( addr -- )
  8 0 do refill drop rdb loop drop ;

\ --- screen addressing ----------------------------------------

: scr-at ( x y -- addr ) #40 * + screen@ + ;
: col-at ( x y -- addr ) #40 * + d800 + ;

\ --- text ------------------------------------------------------
\ Screen ram holds SCREEN CODES, not petscii - poke a string from
\ s" in raw and every letter is off by an alphabet. p>s is the
\ standard mapping (both letter cases, digits, punctuation;
\ control codes pass through untouched, so filter those first if
\ the text can contain them).

: p>s ( c -- c' )
  dup 40 < if exit then         \ $20-$3f: digits etc, unchanged
  dup 60 < if 40 - exit then    \ $40-$5f: lower case
  dup 80 < if 20 - exit then    \ $60-$7f
  dup a0 < if exit then         \ $80-$9f: control, pass through
  dup c0 < if 40 - exit then    \ $a0-$bf
  80 - ;                        \ $c0-$fe: upper case

( Write a string straight into screen ram at a character
  position. This is the game path: no cursor, no scrolling, no
  KERNAL - the prompt stays wherever it was, so it works from an
  irq! callback too. For cursor-and-scroll text output use io's
  at-xy with type instead. Colour the same cells with tile-col!:
    s" score" 0 #24 text!
    1 5 1 0 #24 tile-col! )
0 value tp
: text! ( addr u x y -- )
  scr-at to tp
  over + swap ?do
    i c@ p>s  tp c!  tp 1+ to tp
  loop ;
hide tp

\ --- tiles ----------------------------------------------------
\ A tile is just w*h character codes stored row by row, so a
\ 2x2 tile is 4 bytes. Nothing is stored about tiles beyond
\ that: build them with "create ... c,".

0 value tw
0 value tscr

: tile! ( addr w h x y -- )   \ stamp a tile at char position x,y
  scr-at to tscr
  swap to tw
  0 do
    dup tscr tw move
    tw +
    tscr #40 + to tscr
  loop drop ;

: tile-col! ( col w h x y -- )  \ colour the same w*h area
  col-at to tscr
  swap to tw
  0 do
    dup tscr tw rot fill
    tscr #40 + to tscr
  loop drop ;

hide tw
hide tscr

base !
