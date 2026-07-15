\ joy.fs - joystick reading.
\
\ joy2 / joy1 return a byte with one bit per
\ direction/fire, 1 = pressed (the hardware is
\ active-low, so we invert):
\   bit 0 up   bit 1 down  bit 2 left
\   bit 3 right  bit 4 fire
\
\ Port 1 ($dc01) shares the keyboard matrix, so
\ joy1 reads are noisy while the user is typing.

base @ hex

: joy2 ( -- b ) dc00 c@ invert 1f and ;
: joy1 ( -- b ) dc01 c@ invert 1f and ;

: joy-up?    ( b -- f ) 1 and 0<> ;
: joy-down?  ( b -- f ) 2 and 0<> ;
: joy-left?  ( b -- f ) 4 and 0<> ;
: joy-right? ( b -- f ) 8 and 0<> ;
: joy-fire?  ( b -- f ) 10 and 0<> ;

base !
