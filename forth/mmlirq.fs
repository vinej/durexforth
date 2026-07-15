\ mmlirq.fs - interrupt-driven background music.
\
\ bg-play plays three mml voice strings under the
\ irq, so music keeps going while you type and run
\ other words.  bg-stop silences it.
\
\   include mmlirq
\   : song  mml" ..." mml" ..." mml" ..." bg-play ;
\   song           \ music now plays in the background
\   ( do other things - the tune keeps going )
\   bg-stop
\
\ Note: mml" only works inside a : definition (it is a
\ compile-time word, like in play-mml), so wrap the
\ bg-play call in a colon word as shown.
\ Do not run the foreground `play` while bg music is
\ active: they share the same sid/voice buffers.

require mml
require irq

base @ hex

\ The three voice string-pointer slots used by the
\ mml engine live at .str, .str+2, .str+4.
\ Saved sentinels so bg-stop can restore the byte
\ overwritten with a terminating 0 at each string end.
create bg-end 6 allot    \ 3 end addresses (2 bytes each)
create bg-byte 3 allot   \ 3 overwritten bytes
variable bg-on           \ flag: bg music installed
0 bg-on !                \ variable is not zero-initialised

\ address of voice i's string-pointer slot
: voice-ptr ( i -- addr ) 2* .str + ;

\ arm voice string i: point the engine at the string,
\ terminate it with a sentinel 0, and save what we
\ overwrote so bg-stop can restore it.
: bg-arm ( addr len i -- )
  >r                       \ r: i
  over +                   \ ( addr end )
  r@ 2* bg-end + over swap ! \ save end addr
  dup c@ r@ bg-byte + c!   \ save old byte
  0 over c!                \ write sentinel
  drop r> voice-ptr ! ;    \ store start into slot

\ restore voice string i's terminating byte.
: bg-disarm ( i -- )
  dup 2* bg-end + @        ( i end )
  swap bg-byte + c@        ( end byte )
  swap c! ;

\ silence all three voices and flush to the sid.
: bg-silence ( -- )
  3 0 do i voice c! gate-off loop
  apply-sid ;

: bg-restore ( -- )   \ undo all string sentinels
  3 0 do i bg-disarm loop ;

\ Uninstall unconditionally: after a natural song end
\ mml-tick has already cleared irq-xt, so irq-off's
\ guard would skip the vector restore.  Key off bg-on
\ and restore the vector ourselves.
: bg-stop ( -- )
  bg-on @ if
    di
    irq-old @ 314 !     \ restore previous irq vector
    0 irq-xt !
    ei
    bg-silence
    bg-restore
    0 bg-on !
  then ;

\ Runs at irq time.  On song end we must NOT call the
\ full bg-stop (it does sei/cli and rewrites the irq
\ vector, unsafe from inside the handler).  Instead we
\ just silence and clear irq-xt with a plain store, so
\ no further ticks run; the vector harmlessly chains
\ on.  The user's later bg-stop finishes cleanup.
: mml-tick ( -- )
  notdone if
    voice0 voicetick
    voice1 voicetick
    voice2 voicetick voicedone
    apply-sid
  else
    bg-silence
    0 irq-xt !        \ irq-safe: stop further ticks
  then ;

: bg-play ( str1 str2 str3 -- )
  bg-on @ if bg-stop then   \ replace any current song
  \ strings pushed in order 1,2,3 -> arm voices 2,1,0
  2 bg-arm 1 bg-arm 0 bg-arm
  init-voices
  \ prime the first tick's commands like `play` does
  voice0 do-commands
  voice1 do-commands
  voice2 do-commands voicedone
  -1 bg-on !
  ['] mml-tick irq! ;

base !
