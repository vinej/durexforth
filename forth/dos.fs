require io

base @ hex

\ send command string to drive and
\ print response
: send-cmd ( addr len -- )
?dup if $f $f open ioabort
$f chkin ioabort
begin chrin emit readst until
clrchn $f close cr
else drop rderr then ;

\ grab the remainder of the input line (used by dos and the
\ file-name shortcuts below).  consumes the buffer so these
\ words must be the last thing on their line.
: rest-of-line ( -- addr len )
source >in @ /string
dup >in +! ;

\ send remainder of line as dos command and print response
: dos rest-of-line send-cmd ;

\ --- friendly shortcuts ---------------------------------
\ Thin wrappers over send-cmd so you don't have to remember
\ the raw CBM DOS command letters.  Each takes the rest of
\ the line as its argument, so file names may contain spaces,
\ and the word must be the LAST thing on its line.
\
\   scratch old file      delete "old file"
\   rename  new=old        rename "old" to "new"
\   copy    new=old        copy   "old" to "new"
\   newdisk mydisk,42      full format, name "mydisk" id 42
\   clear-disk mydisk      quick clear (no low-level format)
\   validate               collect the disk (free unclosed blocks)
\   initialize             re-read the BAM / directory
\   status                 print the drive status line
\ (to change unit use the built-in  device  word, e.g.  9 device )
\
\ All operate on drive 0 of the current device (single-drive
\ units like the 1541).  For a dual drive, edit the "0".

\ command builder: assemble "<prefix><rest of line>" in dosbuf,
\ then send.  Do NOT print or touch the input buffer between
\ rest-of-line and the move - that would clobber the argument.
create dosbuf $28 allot          \ up to 40 chars
variable pfxn                    \ length of the prefix just copied

: (cmd) ( pfx-addr pfx-len -- )
dup pfxn !                       \ remember prefix length
dosbuf swap move                 \ dosbuf = prefix
rest-of-line                     \ ( src slen )
dup pfxn @ +                     \ ( src slen total ) - stash total for send
-rot                             \ ( total src slen )
dosbuf pfxn @ +                  \ ( total src slen dst )
swap move                        \ move(src,dst,slen)  -> ( total )
dosbuf swap send-cmd ;           \ ( dosbuf total ) send

: scratch    s" s0:" (cmd) ;
: rename     s" r0:" (cmd) ;
: copy       s" c0:" (cmd) ;
: newdisk    s" n0:" (cmd) ;
: clear-disk s" n0:" (cmd) ;     \ same cmd with no ,id => quick clear
: validate   s" v0" send-cmd ;
: initialize s" i0" send-cmd ;

\ print the drive status / error channel ("00, ok,00,00")
: status 0 0 send-cmd ;

\ (to switch drive units use the built-in  device  word:  9 device )

base !
