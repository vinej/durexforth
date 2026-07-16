\ double.fs - the standard comparison, print and store words
\ for 32-bit doubles, named to match the cell words.
\
\ durexForth already had the arithmetic - d+, dnegate, dabs,
\ m*, um*, um/mod, s>d, fm/mod - but none of the tests you
\ reach for straight afterwards. So where a cell has
\
\   0<  0<>  0>  0=  <  >  <>  .  !  @
\
\ a double now has the same set with a d on the front:
\
\   d0< d0<> d0> d0= d< d> d<> d. d! d@
\
\ A double sits on the stack as two cells, low half first, so
\ the HIGH half is on top - the same order 2@ and 2! use, and
\ the order s>d leaves.
\
\   #12345 s>d d.          \ prints 12345
\   #100 s>d #200 s>d d< . \ prints -1

require compat          \ 2@ 2! 2swap dabs

\ --- comparisons with zero -----------------------------------
\ or'ing the halves is enough for zero: it is 0 only if both are.

: d0=  ( d -- f ) or 0= ;
: d0<> ( d -- f ) d0= 0= ;
: d0<  ( d -- f ) nip 0< ;              \ sign lives in the high half
: d0>  ( d -- f ) 2dup d0= if 2drop 0 else d0< 0= then ;

\ --- comparing two doubles -----------------------------------

: d=   ( d1 d2 -- f ) rot = -rot = and ;
: d<>  ( d1 d2 -- f ) d= 0= ;

( Signed. If the high halves differ, that settles it and the
  comparison is signed; if they match, the answer is in the low
  halves - which must be compared UNSIGNED, since a low half of
  $ffff means 65535, not -1. )
: d<   ( d1 d2 -- f )
  rot 2dup =
  if   2drop u<
  else swap < >r 2drop r>
  then ;

: d>   ( d1 d2 -- f ) 2swap d< ;

\ --- print and store -----------------------------------------

: d.   ( d -- ) tuck dabs <# #s rot sign #> type space ;

( d@ and d! are 2@ and 2! under the names the rest of this
  set uses; the cell/double/float symmetry is the whole point. )
: d@   ( addr -- d ) 2@ ;
: d!   ( d addr -- ) 2! ;
