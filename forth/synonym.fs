\ synonym.fs - readable names for the sigil-heavy words.
\ include synonym, and store/fetch/print spell out what
\ !/@/. abbreviate, across cell, double and float alike.

\ the double, float and string words must exist before they
\ can be renamed. Bare requires on purpose - see charset.fs.
require double
require fp
require str

synonym cstore c!
synonym cfetch c@

synonym store !
synonym fetch @
synonym print .
synonym uprint u.

synonym dstore d!
synonym dfetch d@
synonym dprint d.
synonym udprint ud.
synonym stod s>d

synonym fstore f!
synonym ffetch f@
synonym fprint f.
synonym stof s>f

( The r-stack trio work anywhere EXCEPT as the very last word
  of a definition: the compiler turns a final call into a jump,
  and jump-into-jump makes them reach one caller too far. The
  real >r r> r@ carry a header flag that forbids that; a
  synonym does not. )
synonym rstore >r
synonym rfetch r>
synonym rcopy r@

synonym str s"
synonym sprint .(
synonym cplstring ."

synonym cupper >upper
synonym clower >lower
