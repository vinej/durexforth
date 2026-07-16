\ file.fs - friendly sequential and relative (random-access) file words.
\
\ CBM DOS has two file kinds:
\   * SEQ  - a byte stream you read or write start-to-finish.
\   * REL  - a random-access file of fixed-length records; you can
\            jump to any record and read/write it.
\
\ You choose a small FILE NUMBER (2..14) for each open file, so you
\ can have several open at once.  15 is reserved (command channel).
\
\ --- sequential ---------------------------------------------------
\   s" data" 2 fcreate      \ make+open "data" for WRITING as file 2
\   65 2 fputc              \ write one byte
\   s" hello" 2 fwrite      \ write a string
\   2 fclose
\
\   s" data" 2 fopen        \ open "data" for READING as file 2
\   2 fgetc drop emit       \ read one byte ( fgetc gives  c eof? )
\   2 fread-type            \ print the whole file
\   2 fclose
\
\   s" log" 2 fappend       \ open for APPENDING
\
\ --- relative (random access) -------------------------------------
\   #20 s" db" 2 rel-create \ make REL "db", 20-byte records, file 2
\   5 2 record              \ position to record 5
\   s" jane" 2 fwrite       \ write that record
\   2 rel-close
\
\   s" db" 2 rel-open       \ open existing REL file
\   5 2 record  2 fread-type \ position to record 5 and print it
\   2 rel-close

require io

base @ hex

\ --- open a file with a given data channel / secondary address ----
\ ( nameaddr namelen file# -- )   builds "name<suffix>" and opens it.
\ We use the file number itself as the secondary address (channel),
\ which is the usual convention and keeps things simple.

\ scratch buffer for building "name,type,mode" open strings.
create fnbuf $28 allot
variable fnlen

: fn+ ( addr len -- )              \ append to fnbuf
dup fnlen @ + $28 u> if 2drop exit then
dup >r  fnbuf fnlen @ +  swap move  r> fnlen +! ;

: fn-start ( addr len -- ) 0 fnlen ! fn+ ;   \ begin with the name

\ open fnbuf's contents as file# (also used as the channel/SA).
: fn-open ( file# -- )
dup >r
fnbuf fnlen @ r@ r> open ioabort ;   \ ( name len file# sa -- )

\ --- sequential shortcuts -----------------------------------------
: fcreate ( addr len file# -- )    \ create+open for writing
>r  fn-start  s" ,s,w" fn+  r> fn-open ;

: fopen ( addr len file# -- )      \ open existing for reading
>r  fn-start  s" ,s,r" fn+  r> fn-open ;

: fappend ( addr len file# -- )    \ open for appending
>r  fn-start  s" ,s,a" fn+  r> fn-open ;

: fclose ( file# -- ) clrchn close ;

\ Reading and writing select the file as the current I/O channel,
\ transfer, then release it with clrchn.  A helper that reads one
\ byte AND reports EOF is the key piece: on CBM DOS end-of-file is
\ only known from readst right after the read that hit it.

\ fgetc: read one byte and the EOF flag from file#.
\ ( file# -- c eof? )   eof? is true once the last byte was read.
: fgetc ( file# -- c eof? )
chkin ioabort
chrin readst $40 and 0<>          \ ( c eof? )   $40 = EOI bit
clrchn ;

\ fputc: write one byte to file#.
: fputc ( c file# -- )
chkout ioabort  emit  clrchn ;

\ fwrite: write a string to file#.
: fwrite ( addr len file# -- )
chkout ioabort  type  clrchn ;

\ fread-type: read the WHOLE file and print it to the screen.
: fread-type ( file# -- )
begin dup fgetc          ( file# c eof? )
  >r emit r>             \ print the byte, keep eof?
until drop ;

\ --- relative (random access) -------------------------------------
\ create a REL file: record length reclen (1..254), opened as file#.
\ REL files need the drive's command channel (15) open as well as
\ the data channel, so we open 15 once here and keep it.  Positioning
\ is done by WRITING a "P" command to channel 15.
variable rel-open?  0 rel-open? !

: open-cmd-chan ( -- )               \ ensure command channel 15 is open
rel-open? @ if exit then
0 0 $f $f open ioabort  1 rel-open? ! ;

: rel-create ( reclen addr len file# -- )
>r                                  \ R: file#   ( reclen addr len )
fn-start                            \ fnbuf = name   ( reclen )
s" ,l," fn+                         \ append REL type marker  ( reclen )
fnbuf fnlen @ + c!                  \ store reclen byte after ",l,"
1 fnlen +!                          \ count it
r> fn-open  open-cmd-chan ;

: rel-open ( addr len file# -- )    \ open existing REL file
>r  fn-start  s" ,l," fn+  r> fn-open  open-cmd-chan ;

\ position file# to record n (1-based, as CBM DOS counts).
\ Send "P" chan rec-lo rec-hi 1  to the command channel (15).
: record ( n file# -- )
$f chkout ioabort                    \ write to command channel
$50 emit                             \ 'P'
dup emit                             \ channel = file# (the data channel/sa)
drop                                 \ done with file#   ( n )
dup $ff and emit                     \ record lo
$100 / emit                          \ record hi
1 emit                               \ byte 1 = start of the record
clrchn ;

\ rel-close: close the data file AND the shared command channel.
: rel-close ( file# -- )
fclose  rel-open? @ if $f close 0 rel-open? ! then ;

base !
