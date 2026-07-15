\ irq.fs test - phase 1 core irq mechanism.
\ Run by including it; on success it writes "ok".

marker ---irqtest---

base @ hex

.( include irq )
parse-name irq included

\ --- test 1: callback fires ~60x per second ---
variable ticks
: inc-ticks 1 ticks +! ;

\ wait n jiffies by counting low-byte transitions
\ (robust against the 256-wrap of $a2).
: 1jiffy ( -- ) a2 c@ begin dup a2 c@ <> until drop ;
: wait-jiffies ( n -- ) 0 do 1jiffy loop ;

0 ticks !
' inc-ticks irq!
#60 wait-jiffies
irq-off
\ allow +/- 3 jiffies of scheduling slack
ticks @ #57 < abort" irq too slow"
ticks @ #63 > abort" irq too fast"

\ --- test 2: foreground data stack survives ---
\ install a callback that churns its own stack, then
\ check our sentinels are intact.
: churn 1 2 3 + + drop ;
1234 5678
' churn irq!
#3 wait-jiffies
irq-off
5678 <> abort" stack corrupted (top)"
1234 <> abort" stack corrupted (2nd)"

\ --- test 3: reinstall swaps callback, ?irq works ---
0 ticks !
' inc-ticks irq!
?irq 0= abort" ?irq false after install"
' churn irq!            \ second install wins
#3 wait-jiffies
irq-off
?irq abort" ?irq true after off"
\ inc-ticks was replaced by churn, so ticks unchanged
ticks @ abort" callback not swapped"

\ --- test 4: irq-off when idle is a no-op ---
irq-off
?irq abort" irq-off idle broke state"

.( irq tests passed ) cr

base !

\ signal success and exit vice (matches test harness)
0 1 s" ok" saveb
0 $d7ff c!

---irqtest---
