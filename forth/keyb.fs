\ keyb.fs - reading the keyboard matrix directly.
\
\ key and key? go through the KERNAL: buffered, one
\ keypress at a time, with a repeat delay. A game needs
\ something different - what is held down right now, and
\ several keys at once. "Left and fire" is one state, not
\ two events, and no amount of key? will tell you that.
\ So we scan the hardware.

base @ hex

\ The matrix is 8 rows by 8 columns. Writing a row mask to
\ $dc00 (a 0 bit selects that row) makes $dc01 read back
\ that row's columns, where a 0 bit means pressed. So a key
\ is just row*8 + column, 0..63.

\ $dc00 is left at $ff afterwards - with a row still
\ selected, joy2 (which reads the same port) would report
\ phantom directions. The KERNAL's own scan would put it
\ back within a frame, but a game that has taken over the
\ irq has no such luck.
\
\ The scan is atomic because the KERNAL scans the matrix
\ sixty times a second and would otherwise move the row out
\ from under us between the write and the read. php/plp
\ rather than sei/cli, so this stays safe to call from
\ inside an irq callback, where I is already set.

code kb-scan ( mask -- bits )
php, sei,
lsb lda,x dc00 sta,
dc01 lda, pha,
ff lda,# dc00 sta,
pla, lsb sta,x
0 lda,# msb sta,x
plp,
rts, end-code

: kb? ( n -- f )   \ is key n held down right now?
  dup 3 rshift 1 swap lshift invert kb-scan
  swap 7 and 1 swap lshift and 0= ;

: kb-key ( row col -- n ) swap 3 lshift + ;

\ Common keys. Any other key is "row col kb-key" - see the
\ matrix in any C64 reference.
\
\ Two things the matrix will tell you that surprise people:
\
\ There are only FOUR function keys down there. F2 is SHIFT+F1,
\ F4 is SHIFT+F3, F6 is SHIFT+F5, F8 is SHIFT+F7 - so pressing
\ F2 makes kb? report k-f1 AND k-lshift, which is the hardware
\ being honest. To tell F1 from F2:
\   : f1? k-f1 kb? k-lshift kb? 0= and ;
\   : f2? k-f1 kb? k-lshift kb? and ;
\
\ And the two shifts are separate keys (15 and 52), not one -
\ check both if you mean "either shift".
#01 constant k-return
#02 constant k-crsr-rt
#03 constant k-f7
#04 constant k-f1
#05 constant k-f3
#06 constant k-f5
#07 constant k-crsr-dn
#09 constant k-w
#10 constant k-a
#12 constant k-z
#13 constant k-s
#15 constant k-lshift
#18 constant k-d
#20 constant k-c
#23 constant k-x
#52 constant k-rshift
#58 constant k-ctrl
#60 constant k-space
#62 constant k-q
#63 constant k-stop

base !
