\ keybdemo.fs - watch the keyboard matrix working.
\
\ kb? is the one thing an emulator harness cannot check:
\ VICE's -keybuf feeds the KERNAL's keyboard BUFFER and never
\ touches the matrix, so a physically held key is untestable
\ headlessly. This demo has to be run by hand - on real
\ hardware, a MiSTer, or an interactive emulator.
\
\ The point to look for is several keys AT ONCE. key/key? can
\ never show you that: they are buffered and hand you one
\ keypress at a time. Hold Z and X and SPACE together and all
\ three should light up - that is what a game needs and what
\ the KERNAL cannot give you.
\
\   include keybdemo
\   keys          \ hold keys; RUN/STOP quits

require keyb

base @ hex

13 constant home

( Lowest-numbered key currently held, -1 for none. Press any key
  and this names its matrix number, so you can check a constant -
  or work out one this module does not define - without a table. )
: kb-any ( -- n | -1 )
  #64 0 do i kb? if i unloop exit then loop -1 ;

: keys ( -- )
  page
  begin
    home emit
    ." hold keys - several at once" cr
    ." run/stop quits" cr cr
    ." > "
    k-space  kb? if ." SPACE " then
    k-z      kb? if ." Z " then
    k-x      kb? if ." X " then
    k-a      kb? if ." A " then
    k-s      kb? if ." S " then
    k-q      kb? if ." Q " then
    k-w      kb? if ." W " then
    k-lshift kb? if ." LSHIFT " then
    k-rshift kb? if ." RSHIFT " then
    k-ctrl   kb? if ." CTRL " then
    ."                     " cr cr
    ." raw key = "
    kb-any dup 0< if drop ." none " else . then
    ."      " cr
    k-stop kb?
  until
  page ." keyb ok" cr ;

base !
