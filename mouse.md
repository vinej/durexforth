# Reading the Mouse on the Commodore 64 in Assembler

A practical tutorial for the **Commodore 1351** proportional mouse using 6502 assembly.

> **In durexForth:** this repo already ships a working driver — the `mouse` module
> ([`forth/mouse.fs`](forth/mouse.fs)) and the `mousedemo` example
> ([`forth/mousedemo.fs`](forth/mousedemo.fs), an arrow sprite that follows the mouse).
> The Forth code uses the *normalise-first* scheme described in section 2 (`mouse-raw`
> does `>>1` and masks to 6 bits; `mouse-delta` sign-extends from bit 5). This document is
> the hardware background behind that module. See the *Mouse* section of the manual
> (`manual/irq.adoc`) for the word reference.

---

## 1. Two kinds of "mouse"

The 1351 mouse can operate in **two modes**:

- **Joystick / "Amiga" mode** — hold the right button while powering on. The mouse
  fakes joystick directions. Reading it is exactly like reading a joystick. Easy, but
  coarse and jittery.
- **Proportional mode** (the normal mode) — the mouse reports *movement* through the
  SID chip's analog paddle inputs. This is what you actually want, and it's what this
  tutorial covers.

In proportional mode the 1351 does **not** use real potentiometers. Inside is a MOS 5717
gate array driving a quadrature encoder that feeds the SID a fresh value roughly every
512 CPU cycles. Each read of the paddle register gives you an updated position counter,
and your job is to turn successive readings into a signed movement delta.

---

## 2. The registers you need

| Purpose | Address | Notes |
|---|---|---|
| POT X (horizontal) | `$D419` | SID paddle X input |
| POT Y (vertical)   | `$D41A` | SID paddle Y input |
| CIA#1 Port A       | `$DC00` | joystick port 2 + **POT mux select** (bits 6–7) |
| CIA#1 Port B       | `$DC01` | joystick port 1 (fire/directions) |

The mouse is normally plugged into **control port 1**, so its buttons are read from
`$DC01`. (If it's in port 2, use `$DC00` for the buttons.)

### POT value encoding — the key detail

Each POT register returns a value where the meaningful **6-bit counter sits in the
middle** of the byte, with a noise bit below it:

```
 raw byte from the pot register:
 bit:  7   6   5   4   3   2   1   0
       x   c   c   c   c   c   c   n
           └───── 6-bit counter ──┘
       (7 = don't-care, 0 = noise bit)
```

So in the **raw** register value the 6-bit counter lives in **bits 1–6**, bit 7 is
don't-care, and bit 0 is a noise bit you throw away. The clean way to handle this is to
**shift the counter down first** (`>>1`, dropping the noise bit) and mask to 6 bits, so
you are then working with a plain 0–63 counter:

```
 after  (raw >> 1) & $3f  :
 bit:  7   6   5   4   3   2   1   0
       0   0   S   c   c   c   c   c
               └── sign of the delta
```

Now the counter is in **bits 0–5**, and after you subtract the previous reading, **bit 5
is the sign** of the movement (the value wraps mod 64). This is what `mouse.fs` does:
`mouse-raw` performs the `>>1` and `$3f` mask, and `mouse-delta` sign-extends from bit 5.
Because the counter has already been shifted down, **one pixel of movement is a delta of
1** — there is no second divide-by-2 to do.

---

## 3. Computing the movement delta

The idea: read the POT now, normalise it to a clean 6-bit counter (shift off the noise
bit, mask to 6 bits), subtract the value you read last time, mask to 6 bits again, and
sign-extend from bit 5 so the mod-64 wrap turns into a proper signed byte.

```asm
POTX   = $D419
POTY   = $D41A

oldx   .byte 0          ; previous normalised POTX reading (0..$3f)
oldy   .byte 0          ; previous normalised POTY reading (0..$3f)

; --------------------------------------------------------------
; read_x_delta:  read POTX, return signed movement since last call
; Returns:    A = signed movement in range -32..+31
;             (also updates the stored old value)
; --------------------------------------------------------------
; Example specialised for X below; duplicate for Y.

read_x_delta:
        lda POTX
        lsr a                  ; drop the noise bit (bit 0)
        and #%00111111         ; keep the 6-bit counter -> 0..$3f
        tay                    ; keep the current counter in Y
        sec
        sbc oldx               ; A = current - old
        sty oldx               ; store current as the new "old"
        and #%00111111         ; low 6 bits of the difference
        cmp #%00100000         ; is bit 5 (sign) set?
        bcc @positive          ; C clear -> value < $20 -> positive
        ; --- negative path: sign-extend from bit 5 ---
        ora #%11000000         ; set bits 6-7 so A is a proper -32..-1
@positive:
        rts                    ; already a signed value; no divide needed
```

### Why this works (worked examples)

Remember the counter has already been shifted down, so **one pixel is a delta of 1**.

- Move **+1 pixel**: current `$05`, old `$04`. `$05 - $04 = $01`. Mask `-> $01`. `< $20`,
  so positive. Result **+1**. ✔
- Move **-1 pixel**: current `$04`, old `$05`. `$04 - $05 = $ff`. Mask `-> $3f`. `>= $20`,
  so negative. `ORA #$C0 -> $ff` = **-1**. ✔
- **Wrap up** past 63: current `$00`, old `$3f`. `$00 - $3f = $c1`. Mask `-> $01` = **+1**.
  The mod-64 mask makes the wrap read as a single step forward. ✔

The result is a signed 8-bit number. To accumulate it into a 16-bit screen coordinate,
sign-extend it first:

```asm
; A holds the signed delta from read_x_delta
        ldx #$00
        cmp #$80               ; sign bit set?
        bcc @pos
        ldx #$FF               ; negative -> high byte = $FF
@pos:
        clc
        adc xpos+0
        sta xpos+0
        txa
        adc xpos+1
        sta xpos+1             ; xpos (16-bit) += delta
```

> **Note on Y:** on the real hardware, rolling the mouse **up** produces a *positive*
> POT-Y delta. If you want "up = smaller Y" screen coordinates, negate the Y delta
> (`EOR #$FF` then `CLC : ADC #1`) before accumulating.

---

## 4. Reading the buttons

The buttons are wired into the joystick lines of the control port, **active low**
(0 = pressed):

- **Left button**  = the fire button  -> bit 4 (`$10`)
- **Right button** = "joystick up"    -> bit 0 (`$01`)

For a mouse in **port 1** (`$DC01`):

```asm
        lda $DC01
        and #%00010000         ; left button (fire)
        bne @left_up           ; bit set = released
        ; ... left button is pressed ...
@left_up:

        lda $DC01
        and #%00000001         ; right button (up)
        bne @right_up
        ; ... right button is pressed ...
@right_up:
```

---

## 5. The POT multiplexer gotcha

The SID has only one pair of A/D converters, shared between the two control ports. Bits
6–7 of CIA#1 `$DC00` select which port the SID currently samples:

- bit 6 set — `%01000000` (`$40`) -> read pots from **port 1**
- bit 7 set — `%10000000` (`$80`) -> read pots from **port 2**

The KERNAL's keyboard-scan IRQ toggles these bits constantly, which can make raw POT
reads flicker if you fight it. Two common, reliable approaches:

1. **Let the KERNAL do the work.** Read `$D419`/`$D41A` normally — the running IRQ keeps
   the mux cycling, and for continuous *delta* reading (where absolute value doesn't
   matter, only the change) this is usually good enough.
2. **Take control.** Turn off the CIA keyboard-scan IRQ, write the select bits yourself,
   wait a few hundred cycles for the SID ADC to settle (it samples every 512 cycles),
   then read. This gives the steadiest values but means you handle the keyboard yourself.

For a first version, start with approach 1 and only reach for approach 2 if you see
jitter.

---

## 6. Putting it together — a minimal poll loop

```asm
;----------------------------------------------------------
; Call once per frame (e.g. from your raster IRQ)
;----------------------------------------------------------
mouse_poll:
        jsr read_x_delta       ; A = signed dx
        jsr add_dx_to_xpos     ; xpos += dx (16-bit, from section 3)

        jsr read_y_delta       ; A = signed dy  (mirror of read_x_delta on POTY/oldy)
        jsr add_dy_to_ypos

        lda $DC01              ; buttons (port 1)
        sta buttons            ; bit4 = left, bit0 = right (active low)
        rts
```

Then use `xpos`/`ypos` to position a sprite as your pointer, clamping to the screen
bounds you care about.

---

## 7. Common pitfalls

- **Forgetting to normalise the raw reading.** Shift the noise bit off (`LSR`) and mask to
  6 bits (`AND #$3F`) *before* you subtract; that turns the register into a clean 0..63
  counter so one pixel is a delta of 1. (If instead you subtract the raw registers first,
  one pixel is a delta of 2 and you owe a final divide-by-2 — pick one scheme and stay
  consistent; this tutorial normalises first.)
- **Masking to the wrong width / wrong sign bit.** After the subtraction, `AND #$3F` and
  test **bit 5** (`$20`) for the sign — not bit 6/7. The mod-64 subtraction can leave
  garbage in the upper bits, and testing the wrong bit breaks the wrap handling.
- **Wrong port for buttons.** Port 1 = `$DC01`, port 2 = `$DC00`. The buttons are active
  **low**.
- **First reading jumps.** On the very first frame `oldx`/`oldy` are uninitialised, so
  seed them with one throwaway read before your main loop starts.
- **Reading pots on the wrong mux setting** (section 5) — the usual cause of jitter.

---

## Sources

- [1351 Mouse, and Mouse Driver — C64 OS](https://c64os.com/post/1351mousedriver) (the delta / sign-extend algorithm)
- [Mouse 1351 — C64-Wiki](https://www.c64-wiki.com/wiki/Mouse_1351) (register encoding, "modulo 64", button wiring)
- [Serial mouse interface for Commodore — zimmers.net](http://www.zimmers.net/anonftp/pub/cbm/documents/projects/interfaces/mouse/Mouse.html) (hardware background)
