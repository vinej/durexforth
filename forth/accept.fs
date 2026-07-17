\ accept.fs - the ok prompt's line input, with line editing.
\
\ crsr left/right move within the line, home jumps to its start,
\ del removes the character before the cursor, inst (shift+del)
\ opens a gap, and typing overwrites under the cursor - the same
\ conventions as the basic screen editor, so fingers already
\ know them. return accepts the WHOLE line wherever the cursor
\ stands. crsr up/down are ignored: this is a line editor, and
\ the screen above it is history, not a document.
\
\ The screen is repainted through emit only, so the kernal keeps
\ owning wrap and scroll, and its cursor blink follows along.

0 value aaddr           \ buffer
0 value aavail          \ its capacity
0 value alen            \ characters in it
0 value apos            \ cursor, 0..alen

: (emits) ( c u -- ) 0 ?do dup emit loop drop ;

( repaint from the cursor to the end, plus u trailing blanks to
  rub out what a deletion left behind, then walk the screen
  cursor back where it was )
: (repaint) ( u -- )
  alen apos ?do aaddr i + c@ emit loop
  dup bl swap (emits)
  $9d swap alen apos - + (emits) ;

: accept ( addr avail -- len )
  0 $cc c!              \ cursor blink on
  to aavail to aaddr  0 to alen  0 to apos
  begin key case
    $0d of              \ return: take the line, cursor anywhere
      $1d alen apos - (emits)
      space  1 $cc c!  alen exit endof
    $9d of apos if      \ crsr left
      $9d emit  apos 1- to apos then endof
    $1d of apos alen < if   \ crsr right
      $1d emit  apos 1+ to apos then endof
    $13 of              \ home: start of the line
      $9d apos (emits)  0 to apos endof
    $14 of apos if      \ del: eat the char before the cursor
      apos 1- to apos  alen 1- to alen
      aaddr apos + 1+  aaddr apos +  alen apos - move
      $9d emit  1 (repaint) then endof
    $94 of apos alen <  alen aavail <  and if  \ inst: open a gap
      aaddr apos +  dup 1+  alen apos - move
      bl aaddr apos + c!  alen 1+ to alen
      0 (repaint) then endof
    \ anything printable overwrites at the cursor; the key value
    \ itself stays on the stack for endcase to drop
    dup dup $7f and $1f >  apos aavail <  and if
      dup aaddr apos + c!  emit
      apos 1+ to apos
      apos alen > if apos to alen then
    else drop then
  endcase again ;

hide aaddr
hide aavail
hide alen
hide apos
hide (emits)
hide (repaint)
