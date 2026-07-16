\ str.fs - easy string manipulation.
\
\ Strings are ordinary Forth  ( addr len )  pairs, exactly like the
\ ones s" gives you.  The slicing words (left / right / mid / trim)
\ return a VIEW into the same memory - nothing is copied and no
\ buffers are used, so a result is valid for as long as its source
\ is.  The test/search words return flags or positions.
\
\   s" hello world"  5 left            type   \ hello
\   s" hello world"  5 right           type   \ world
\   s" hello world"  6 5 mid           type   \ world  (start 6, len 5)
\   s"   spaced  "   trim              type   \ spaced
\   s" abc" s" abc" str=                .      \ -1 (true)
\   s" abc" s" abd" compare             .      \ -1  (-1<, 0=, 1>)
\   s" hello" [char] l index-of         .      \ 2
\   s" hello world" s" wor" search      .      \ 6
\
\ upper / lower change the bytes IN PLACE, so give them a writable
\ buffer, not the transient s" area. cstr makes one: a named,
\ durable, writable copy of any string - take the copy FIRST and
\ the original survives whatever you do to it:
\
\   s" hello world" cstr msg
\   msg cstr backup       \ a copy of the copy
\   msg upper  msg type   \ HELLO WORLD
\   backup type           \ hello world - untouched

require io

base @ hex

\ --- copying ------------------------------------------------------
\ Everything else in this module returns views or works in place;
\ cstr is the one word that copies. It allots the string into the
\ dictionary - u+7 bytes, permanent: create's 5-byte preamble, a
\ length cell, the characters - which is the point: the copy
\ outlives the transient s" area and every in-place edit of the
\ original. Being create/does>, it defines a word, so use it at
\ the top level, not inside a definition.
: cstr ( addr u "name" -- )
create dup , here swap dup allot move
does> dup 2+ swap @ ;

\ durexForth's resident set has 2dup / 2drop but not 2swap / 2over,
\ which the two-string words below need, so define them here.
: 2swap ( a b c d -- c d a b ) rot >r rot r> ;
: 2over ( a b c d -- a b c d a b ) >r >r 2dup r> r> 2swap ;

\ --- slicing (return views into the same memory) -----------------

\ first n characters
: left ( addr u n -- addr n' )
swap min ;                        \ n' = min(n,u)

\ last n characters.  Keep min(n,u) chars by dropping the leading
\ (u - min(n,u)) characters with /string.
: right ( addr u n -- addr' n' )
over min                          \ ( addr u n' )   n'=min(n,u)
over swap -                       \ ( addr u drop# )  drop# = u-n'
/string ;                         \ ( addr+drop#  u-drop# ) = ( addr' n' )

\ substring: start (0-based) and length.  Uses /string to drop the
\ first `start` chars (clamped), then `left` to keep `len` of them.
: mid ( addr u start len -- addr' n' )
>r                                \ R: len       ( addr u start )
over min                          \ start' = min(start,u)  ( addr u start' )
/string                           \ drop start' leading chars ( addr' avail )
r> left ;                         \ keep min(len,avail)     ( addr' n' )

\ --- whitespace --------------------------------------------------
: -trailing ( addr u -- addr u' )
begin dup while
  2dup + 1- c@ bl = if 1- else exit then
repeat ;

: -leading ( addr u -- addr' u' )
begin dup while
  over c@ bl = if 1 /string else exit then
repeat ;

: trim ( addr u -- addr' u' ) -leading -trailing ;

\ --- comparison --------------------------------------------------
\ equal?  ( true = -1 )
: str= ( a1 u1 a2 u2 -- f )
rot over <> if drop 2drop 0 exit then   \ lengths differ -> not equal
( a1 a2 u )
dup 0= if drop 2drop -1 exit then       \ both empty -> equal
0 ?do
  over i + c@  over i + c@  <> if 2drop unloop 0 exit then
loop 2drop -1 ;

\ --- searching ---------------------------------------------------
\ index of character c in ( addr u ), or -1 if not found.
\ Uses a variable for the running index to keep the stack simple
\ and avoid any do/loop return-stack interaction.
variable sidx
: index-of ( addr u c -- i )
0 sidx !
begin                             \ ( addr u c )
  over 0<>                        \ any chars left? (u is a length >=0)
while
  >r                              \ R: c        ( addr u )
  over c@ r@ = if                 \ *addr == c ?
    2drop r> drop  sidx @ exit    \ found: return index
  then
  r>                              \ ( addr u c )
  >r  1 /string  r>              \ advance addr, dec u ; ( addr' u' c )
  1 sidx +!                       \ index++
repeat
2drop drop -1 ;                   \ not found

\ "does haystack ( a1 u1 ) begin with needle ( a2 u2 )?"  ( -1/0 )
: starts-with? ( a1 u1 a2 u2 -- f )
dup >r                            \ R: u2
2swap r> left                     \ take first u2 chars of the haystack
2swap str= ;                      \ compare the two ( a2 u2 ) ( pre u2' )
\ (str= handles the case where the haystack is shorter than needle:
\  left clips it, the lengths differ, str= returns false.)

\ index of substring ( a2 u2 ) within ( a1 u1 ), or -1 if not found.
\ Empty needle matches at 0.  Slides the haystack one char at a time.
variable spos
: search ( a1 u1 a2 u2 -- i )
dup 0= if 2drop 2drop 0 exit then \ empty needle -> found at 0
0 spos !
begin                             \ ( a1 u1 a2 u2 )
  2over nip 0<>                   \ haystack not empty?
while
  2over 2over starts-with? if     \ needle at this position?
    2drop 2drop spos @ exit
  then
  2swap 1 /string 2swap           \ advance haystack by one char
  1 spos +!
repeat
2drop 2drop -1 ;                  \ exhausted, not found

\ --- case conversion (in place) ----------------------------------
\ NOTE the PETSCII layout durexForth's s" produces: lowercase a-z are
\ $41-$5a, and UPPERCASE A-Z are $c1-$da (the opposite of ASCII).  So
\ uppercasing adds $80 to a lowercase letter, lowercasing subtracts it.
: >upper ( c -- C ) dup $40 u> over $5b u< and if $80 + then ;
: >lower ( C -- c ) dup $c0 u> over $db u< and if $80 - then ;

: upper ( addr u -- )             \ uppercase in place
over + swap ?do
  i c@ >upper i c!
loop ;

: lower ( addr u -- )             \ lowercase in place
over + swap ?do
  i c@ >lower i c!
loop ;

\ --- comparison ordering -----------------------------------------
\ compare ( a1 u1 a2 u2 -- n )  n = -1 if s1<s2, 0 if equal, 1 if s1>s2
\ Lexicographic on byte values; on a common prefix the shorter string
\ sorts first.  Uses variables to keep the logic readable.
variable ca1  variable cn1
variable ca2  variable cn2
: compare ( a1 u1 a2 u2 -- n )
cn2 ! ca2 !  cn1 ! ca1 !
0 spos !
begin
  spos @ cn1 @ u<  spos @ cn2 @ u<  and    \ still within both?
while
  ca1 @ spos @ + c@                        \ c1
  ca2 @ spos @ + c@                        \ c2
  2dup <> if
    u< if -1 else 1 then                   \ c1<c2 -> -1 else 1
    exit
  then
  2drop
  1 spos +!
repeat
\ prefixes equal so far: order by remaining length
cn1 @ cn2 @
2dup u< if 2drop -1 else u> if 1 else 0 then then ;

base !
