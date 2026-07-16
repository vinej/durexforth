\ Use logical file as input device
\ ioresult is 0 on success, kernal
\ error # on failure.
code chkin ( file# -- ioresult )
w stx,
lsb lda,x tax, \ x = file#
$ffc6 jsr, \ CHKIN
+branch bcs, \ carry set = error
0 lda,# \ A is only valid on error
:+
w ldx,
lsb sta,x
0 lda,# msb sta,x
rts, end-code

\ Use logical file as output device
\ ioresult is 0 on success, kernal
\ error # on failure.
code chkout ( file# -- ioresult )
w stx,
lsb lda,x tax, \ x = file#
$ffc9 jsr, \ CHKOUT
+branch bcs, \ carry set = error
0 lda,# \ A is only valid on error
:+
w ldx,
lsb sta,x
0 lda,# msb sta,x
rts, end-code

\ Reset input and output to console
code clrchn ( -- )
txa, pha,
$ffcc jsr,  \ CLRCH
pla, tax,
rts, end-code

\ Read status of last IO operation
code readst ( -- status )
dex, 0 lda,# msb sta,x
$ffb7 jsr, \ READST
lsb sta,x
rts, end-code

\ Get a byte from input device
code chrin ( -- chr )
dex, w stx, 0 lda,# msb sta,x
$ffcf jsr, \ CHRIN
w ldx, lsb sta,x
rts, end-code

\ Cursor position. at-xy is the standard word (ANS Facility);
\ it goes through the KERNAL's PLOT vector so the screen-line
\ pointer is recomputed - poking the zero-page column/row
\ directly leaves output going to the old line. Reading them
\ back, by contrast, is just two zero-page bytes.
code at-xy ( x y -- )
w stx,
lsb lda,x pha,          \ row = y (top of stack)
lsb 1+ lda,x tay,       \ column = x
pla, tax,
clc,                    \ carry clear = set position
$fff0 jsr,              \ PLOT
w ldx, inx, inx,
rts, end-code

: xy@ ( -- x y ) $d3 c@ $d6 c@ ;
