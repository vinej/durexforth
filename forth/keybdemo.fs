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
    k-lshift kb? if ." SHIFT " then
    k-ctrl   kb? if ." CTRL " then
    ."                     " cr
    k-stop kb?
  until
  page ." keyb ok" cr ;

base !
