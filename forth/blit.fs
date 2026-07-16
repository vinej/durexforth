\ blit.fs - software sprites: block blits into the bitmap.
\
\ The vic gives you eight hardware sprites. Everything else
\ a game draws - a screen full of aliens, a bullet, a piece
\ of landscape - has to be poked into the bitmap by hand.
\ That is what a blitter does: stamp a block of pixels in,
\ optionally combining it with what is already there.
\
\ Blits are CHARACTER aligned, as White Lightning's were.
\ The bitmap is stored as 8-byte cells, so a cell-aligned
\ blit is a byte copy; an arbitrary pixel x would mean
\ shifting every byte and merging it across a cell boundary,
\ which is several times the work. The classic answer is to
\ keep eight pre-shifted copies of the sprite and pick one -
\ a job for whatever draws the sprite, not for the blitter.
\
\ A sprite is w*h cells of 8 bytes, stored row by row: the
\ same shape gfx's drawchar takes, only bigger.

require gfx

base @ hex

\ The bitmap lives at $e000, underneath the kernal. Writes
\ fall through to the ram, but READS would come back as
\ kernal rom - and or/and/xor read. So every bitmap access
\ is wrapped in gfx's kernal-out/kernal-in, which also means
\ interrupts are off for the duration (kernal-out does sei)
\ and nothing may call the kernal in between. Colour memory
\ at $cc00 is not under anything, so blit-col! is exempt.

: bm-cell ( col row -- addr ) 140 * swap 3 lshift + bmpbase + ;
: co-cell ( col row -- addr ) 28 * + colbase + ;

\ How a source byte is combined with the bitmap byte. Assign
\ your own with " ' myop to blit-op " if you want something
\ exotic - a mask, say.
: op-blk ( b addr -- ) c! ;
: op-or  ( b addr -- ) tuck c@ or  swap c! ;
: op-and ( b addr -- ) tuck c@ and swap c! ;
: op-xor ( b addr -- ) tuck c@ xor swap c! ;

' op-blk value blit-op

0 value bsrc
0 value bw
0 value bh
0 value bx
0 value by

: cell! ( src dst -- )
  8 0 do
    over i + c@   over i +   blit-op execute
  loop 2drop ;

: (blit) ( -- )
  bh 0 do
    bw 0 do
      bsrc  bx i + by j + bm-cell  cell!
      bsrc 8 + to bsrc
    loop
  loop ;

: blit ( src w h x y -- )   \ blit through the current blit-op
  to by to bx to bh to bw to bsrc
  kernal-out (blit) kernal-in ;

: blit-blk ( src w h x y -- ) ['] op-blk to blit-op blit ;
: blit-or  ( src w h x y -- ) ['] op-or  to blit-op blit ;
: blit-and ( src w h x y -- ) ['] op-and to blit-op blit ;
: blit-xor ( src w h x y -- ) ['] op-xor to blit-op blit ;

( Read a block back out of the bitmap. Save the background
  before stamping a sprite on it, put it back afterwards:
    bg 2 2 #10 #5 blit-get
    ship 2 2 #10 #5 blit-or
    ... later ...
    bg 2 2 #10 #5 blit-blk )
: blit-get ( dst w h x y -- )
  to by to bx to bh to bw to bsrc
  kernal-out
  bh 0 do
    bw 0 do
      bx i + by j + bm-cell  bsrc  8 move
      bsrc 8 + to bsrc
    loop
  loop
  kernal-in ;

( Colour the cells a blit covers. Hi-res colour is per cell:
  the high nibble is the pixel colour, the low nibble the
  background - so $10 is white on black. A sprite blitted
  without this is there but invisible. )
: blit-col! ( c w h x y -- )
  to by to bx to bh to bw
  bh 0 do
    bw 0 do
      dup  bx i + by j + co-cell c!
    loop
  loop drop ;

\ --- sprite to sprite ------------------------------------------
\ Combine one sprite into another of the same size, without the
\ bitmap being involved at all - so no kernal banking, and no
\ interrupts held off. Build a masked sprite once at startup
\ (cpy-and a mask in, cpy-or the image on top) and then a single
\ blit-blk per frame draws it over any background.

: cpy ( src dst w h -- )   \ dst = dst op src, through blit-op
  * 3 lshift               ( src dst n )
  0 do
    over i + c@   over i +   blit-op execute
  loop 2drop ;

: cpy-blk ( src dst w h -- ) ['] op-blk to blit-op cpy ;
: cpy-or  ( src dst w h -- ) ['] op-or  to blit-op cpy ;
: cpy-and ( src dst w h -- ) ['] op-and to blit-op cpy ;
: cpy-xor ( src dst w h -- ) ['] op-xor to blit-op cpy ;

hide bsrc
hide bw
hide bh
hide bx
hide by
hide (blit)
hide cell!

base !
