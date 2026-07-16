; cart-ef.asm - EasyFlash boot stub + ROM filesystem for durexForth.
;
; The packed durexForth image is ~17K, so it does not fit one 16K bank.
; It is split over three half-banks and copied to RAM at $0801 at boot.
; Banks 2+ hold a read-only filesystem of module sources, so "include"
; works with no disk attached.
;
; EasyFlash powers up in ULTIMAX mode, where the memory map is:
;   $0000-$0fff RAM   (the ONLY writable RAM in ultimax)
;   $1000-$7fff open
;   $8000-$9fff ROML  (bank N, low half)
;   $a000-$cfff open
;   $d000-$dfff I/O
;   $e000-$ffff ROMH  (bank N, high half)  <- reset vector comes from here
;
; So the reset entry and the hardware vectors must live in bank 0 HI, and
; the boot code cannot copy anything above $0fff until it leaves ultimax.
;
; Leaving ultimax is the tricky part: the moment $de02 is written, $e000
; becomes the KERNAL and any code executing there is gone mid-instruction.
; The fix is to run the switch from RAM.  'start' (ultimax, $e000 window)
; copies 'tramp' into the tape buffer at $033c and jumps to it; tramp
; switches to 16K mode and copies the driver to $c000, which does
; everything else.  The tape buffer is only borrowed during boot.
;
; Boot bank layout (see build/efbuild.py, which patches lenA/lenB/lenC):
;   bank 0 LO : chunk A, first 8192 bytes            -> $0801
;   bank 0 HI : this code (reset vector at $fffc)
;   bank 1 LO : chunk B, next 8192 bytes             -> $2801
;   bank 1 HI : chunk C, the tail                    -> $4801
;
; ROM filesystem (banks FS_BANK.., HI half-banks only, see efbuild.py):
;   bank 2 HI $a000 : directory, then file data, continuing into bank 3+
;   directory entry : namelen, name, bank, ptrlo, ptrhi, lenlo, lenhi
;                     terminated by a namelen of 0.  Must fit one bank.
;   Only HI half-banks are used: reading at $a000-$bfff never disturbs
;   durexForth, which lives in $0801-$9fff.  (Banking in does shadow
;   $8000-$9fff with ROML, but nothing is read there while banked.)
;
; EasyFlash registers:
;   $de00 bank
;   $de02 control (bit7 LED, bit2 M, bit1 EXROM, bit0 GAME)
;     $87 = LED + M + EXROM low + GAME low -> 16K mode, both windows ROM
;     $04 = M + EXROM high + GAME high     -> cart off, all RAM

!cpu 6510
!ct raw
!initmem $ff            ; unprogrammed flash reads as $ff
!to "build/cart-ef.bin", plain

EF_BANK    = $de00
EF_CONTROL = $de02
EF_16K     = $87
EF_OFF     = $04

TRAMP   = $033c         ; tape buffer, boot only
BOOT    = $c000         ; unpack code, boot only - dead once durexForth runs
STAGE   = $c100         ; romfs staging buffer, boot only (see the two-hop copy)

; The resident romfs has to live somewhere durexForth will never reuse, and
; there is only one such hole.  $a000-$cbff is the editor text buffer
; (forth/v.fs "bufstart"), $cc00-$cfff is hi-res colour and $e000-$ffff the
; hi-res bitmap (forth/gfx.fs), and $0801-$9fff is code + dictionary.  So
; base-ef.fs lowers durexForth's dictionary top to $9cff with "$9cff top!",
; reserving $9d00-$9fff (768 bytes) here.  Keep the two in sync.
;
; $9d00 is inside the ROML window, so the romfs must NOT be visible as ROM
; while it runs: it banks in with the CPU port at $36 (LORAM=0), which turns
; ROML off and leaves $8000-$9fff as RAM, while $a000-$bfff still maps ROMH.
; That is why the filesystem only ever uses HI half-banks.  Order matters -
; set $01 before enabling the cart, and disable the cart before restoring
; $01, or the ground disappears mid-instruction.
DRIVER  = $9d00
DRIVER_LIMIT = $a000 - DRIVER

PORT_ROML = $37         ; ROML+ROMH visible: boot only, never at $9d00
PORT_NOML = $36         ; LORAM=0: $8000-$9fff RAM, $a000-$bfff ROMH
FS_BANK = 2             ; first ROM filesystem bank
NSLOTS  = 4             ; concurrently open ROM files (includes nest)

; KERNAL zero page used by SETNAM/SETLFS
FNLEN  = $b7
LFN    = $b8
DEVNUM = $ba
FNADR  = $bb
STATUS = $90

* = $e000

; ------------------------------------------------------------------
; bank 0 HI, low end: ROM images of the code that runs from RAM.
; !pseudopc assembles each for the address it executes at, while
; emitting the bytes here for 'start' to copy out.
; ------------------------------------------------------------------

tramp_rom
!pseudopc TRAMP {
tramp
    ; CPU port: RAM + I/O + KERNAL once we drop out of ultimax
    lda #$2f
    sta $00
    lda #$37
    sta $01

    ; leave ultimax.  Safe here: we are executing from RAM.
    lda #EF_16K
    sta EF_CONTROL      ; $8000=bank0 LO, $a000=bank0 HI, $e000=KERNAL

    ; copy the boot code out of bank 0 HI to $c000
    lda #<(boot_rom - $4000)    ; $e000 window -> $a000 window
    sta $fb
    lda #>(boot_rom - $4000)
    sta $fc
    lda #$00
    sta $fd
    lda #>BOOT
    sta $fe
    ldx #BOOT_PAGES
    ldy #0
-   lda ($fb),y
    sta ($fd),y
    iny
    bne -
    inc $fc
    inc $fe
    dex
    bne -

    jmp BOOT
}
tramp_end

!if tramp_end - tramp_rom > 192 {
    !error "tramp is ", tramp_end - tramp_rom, " bytes - too big for the tape buffer"
}

; ------------------------------------------------------------------
; Boot code, at $c000.  Transient: it needs ROML ($01=$37) to read
; chunks A and B, so it cannot live at $9d00 like the romfs does.
; ------------------------------------------------------------------
boot_rom
!pseudopc BOOT {

boot
    jsr $fda3           ; IOINIT  - CIAs, prepare IRQ
    lda #4
    sta $288            ; screen page, read by CINT below
    jsr $fd15           ; RESTOR  - I/O vectors at $0314-$0333
    jsr $ff5b           ; CINT    - init video
    lda #8
    sta DEVNUM          ; last device = drive 8

    lda #$01            ; dest = $0801
    sta $fd
    lda #$08
    sta $fe

    lda #0              ; chunk A: bank 0 LO, $8000
    sta EF_BANK
    lda #$80
    jsr set_src
    lda lenA
    ldx lenA+1
    jsr copy

    lda #1              ; chunk B: bank 1 LO, $8000
    sta EF_BANK
    lda #$80
    jsr set_src
    lda lenB
    ldx lenB+1
    jsr copy

    lda #$a0            ; chunk C: bank 1 HI, $a000 (bank is still 1)
    jsr set_src
    lda lenC
    ldx lenC+1
    jsr copy

    ; Get the resident romfs to $9d00, in two hops.
    ;
    ; It cannot be copied straight there: $9d00 is inside the ROML window, and
    ; a write there with the cart mapped is a FLASH COMMAND to the EasyFlash,
    ; not a store to RAM - the bytes never arrive.  So stage it through $c100,
    ; which is outside every ROM window and free at boot, then drop the cart
    ; and do a plain RAM-to-RAM copy.
    lda #0              ; back to bank 0 - chunk C left this at 1, and the
    sta EF_BANK         ; romfs blob lives in bank 0 HI
    lda #<(romfs_rom - $4000)   ; hop 1: flash $a0xx -> $c100 scratch
    sta $fb
    lda #>(romfs_rom - $4000)
    sta $fc
    lda #$00
    sta $fd
    lda #>STAGE
    sta $fe
    lda #<(romfs_end - romfs_rom)
    ldx #>(romfs_end - romfs_rom)
    jsr copy

    lda #EF_OFF         ; cart off -> $8000-$bfff is plain RAM again
    sta EF_CONTROL

    lda #$00            ; hop 2: $c100 -> $9d00, RAM to RAM
    sta $fb
    lda #>STAGE
    sta $fc
    lda #$00
    sta $fd
    lda #>DRIVER
    sta $fe
    lda #<(romfs_end - romfs_rom)
    ldx #>(romfs_end - romfs_rom)
    jsr copy

    jsr install         ; hook the KERNAL so "include" sees flash
    cli
    ldx #0
    jmp $80d

set_src
    sta $fc
    lda #0
    sta $fb
    rts

; copy A/X (lo/hi) bytes from $fb to $fd, advancing $fd
copy
    sta cnt
    stx cnt+1
    ldy #0
cloop
    lda cnt
    ora cnt+1
    beq cdone
    lda ($fb),y
    sta ($fd),y
    inc $fb
    bne +
    inc $fc
+   inc $fd
    bne +
    inc $fe
+   lda cnt
    bne +
    dec cnt+1
+   dec cnt
    jmp cloop
cdone
    rts

cnt  !word 0
lenA !word 0            ; patched by asm/efbuild.py
lenB !word 0
lenC !word 0

}
boot_end

BOOT_PAGES = (boot_end - boot_rom + 255) / 256

; ------------------------------------------------------------------
; The resident romfs, at $9d00.  Hooks the KERNAL indirect vectors.
; Anything not found in flash falls through to the original routine,
; so a real drive still works.
; ------------------------------------------------------------------
romfs_rom
!pseudopc DRIVER {

install
    ldx #0              ; save $031a-$0325: open/close/chkin/chkout/clrchn/chrin
-   lda $031a,x
    sta oldopen,x
    inx
    cpx #12
    bne -

    lda #<open_hook
    sta $031a
    lda #>open_hook
    sta $031b
    lda #<close_hook
    sta $031c
    lda #>close_hook
    sta $031d
    lda #<chkin_hook
    sta $031e
    lda #>chkin_hook
    sta $031f
    lda #<clrchn_hook
    sta $0322
    lda #>clrchn_hook
    sta $0323
    lda #<chrin_hook
    sta $0324
    lda #>chrin_hook
    sta $0325

    lda #$ff
    sta cur
    lda #0
    ldx #NSLOTS-1
-   sta slot_lfn,x
    dex
    bpl -
    rts

; OPEN - serve from flash if the name is in the directory
open_hook
    lda DEVNUM
    cmp #8
    bne +
    jsr find_file
    bcc .ok
+   jmp (oldopen)
.ok
    clc
    rts

; CHKIN - X = logical file number
chkin_hook
    stx tmp
    txa
    jsr find_slot
    bcs +
    stx cur
    lda #0
    sta STATUS
    clc
    rts
+   ldx tmp
    jmp (oldchkin)

; CHRIN
chrin_hook
    lda cur
    bmi +
    jmp rom_getbyte
+   jmp (oldchrin)

; CLOSE - A = logical file number
close_hook
    pha
    jsr find_slot
    bcs +
    lda #0
    sta slot_lfn,x
    cpx cur
    bne ++
    lda #$ff
    sta cur
++  pla
    clc
    rts
+   pla
    jmp (oldclose)

; CLRCHN
clrchn_hook
    lda #$ff
    sta cur
    jmp (oldclrchn)

; A = lfn -> C=0 and X = slot if open
find_slot
    cmp #0
    beq .no
    ldx #NSLOTS-1
-   cmp slot_lfn,x
    beq .yes
    dex
    bpl -
.no sec
    rts
.yes
    clc
    rts

; Look the SETNAM name up in the directory; fill a slot.  C=0 on success.
find_file
    lda FNLEN
    beq .fail
    cmp #17
    bcs .fail
    sta namelen
    tay
    dey
-   lda (FNADR),y       ; copy the name out before banking in
    sta namebuf,y
    dey
    bpl -

    ldx #NSLOTS-1       ; a free slot?
-   lda slot_lfn,x
    beq .got
    dex
    bpl -
.fail
    sec
    rts
.got
    stx slotidx
    lda $fb
    sta savefb
    lda $fc
    sta savefc
    php
    sei
    lda #PORT_NOML      ; ROML off BEFORE the cart goes on - we run at $9d00
    sta $01
    lda #FS_BANK
    sta EF_BANK
    lda #EF_16K
    sta EF_CONTROL
    lda #$00
    sta $fb
    lda #$a0
    sta $fc

.entry
    ldy #0
    lda ($fb),y         ; namelen, 0 = end of directory
    beq .nomatch
    sta elen
    cmp namelen
    bne .next
    ldx #0
    ldy #1
-   lda ($fb),y
    cmp namebuf,x
    bne .next
    iny
    inx
    cpx elen
    bcc -

    ldx slotidx         ; matched; y indexes the bank byte
    lda ($fb),y
    sta slot_bank,x
    iny
    lda ($fb),y
    sta slot_ptrlo,x
    iny
    lda ($fb),y
    sta slot_ptrhi,x
    iny
    lda ($fb),y
    sta slot_lenlo,x
    iny
    lda ($fb),y
    sta slot_lenhi,x
    lda LFN
    sta slot_lfn,x
    jsr .unbank
    plp                 ; the php'd flags, now that .unbank's rts is gone
    clc
    rts

.next
    lda $fb             ; skip namelen + name + 5 payload bytes
    clc
    adc elen
    sta $fb
    bcc +
    inc $fc
+   lda $fb
    clc
    adc #6
    sta $fb
    bcc +
    inc $fc
+   jmp .entry

.nomatch
    jsr .unbank
    plp
    sec
    rts

; NB: no plp here - the caller's jsr return address is on top of the
; php'd flags, so the plp must happen after this returns.
.unbank
    lda #EF_OFF         ; cart off first, THEN restore the port
    sta EF_CONTROL
    lda #PORT_ROML
    sta $01
    lda savefb
    sta $fb
    lda savefc
    sta $fc
    rts

; Next byte of the current ROM file -> A.  Sets ST=$40 at EOF.
; Uses no zero page: the flash read is self-modified.
rom_getbyte
    php
    sei
    ldx cur
    lda slot_lenlo,x
    ora slot_lenhi,x
    beq .eof

    lda #PORT_NOML      ; ROML off BEFORE the cart goes on - we run at $9d00
    sta $01
    lda slot_bank,x
    sta EF_BANK
    lda #EF_16K
    sta EF_CONTROL
    lda slot_ptrlo,x
    sta .rd+1
    lda slot_ptrhi,x
    sta .rd+2
.rd lda $ffff
    pha
    lda #EF_OFF         ; cart off first, THEN restore the port
    sta EF_CONTROL
    lda #PORT_ROML
    sta $01

    inc slot_ptrlo,x    ; advance, crossing into the next bank at $c000
    bne +
    inc slot_ptrhi,x
    lda slot_ptrhi,x
    cmp #$c0
    bne +
    lda #$a0
    sta slot_ptrhi,x
    inc slot_bank,x
+   lda slot_lenlo,x
    bne +
    dec slot_lenhi,x
+   dec slot_lenlo,x

    lda slot_lenlo,x
    ora slot_lenhi,x
    bne +
    lda #$40
    sta STATUS
+   pla
    plp
    clc
    rts
.eof
    lda #$40
    sta STATUS
    plp
    lda #0
    clc
    rts

oldopen   !word 0       ; must stay in KERNAL vector order
oldclose  !word 0
oldchkin  !word 0
oldchkout !word 0
oldclrchn !word 0
oldchrin  !word 0

cur       !byte $ff     ; slot feeding CHRIN, $ff = none
tmp       !byte 0
slotidx   !byte 0
elen      !byte 0
namelen   !byte 0
savefb    !byte 0
savefc    !byte 0
namebuf   !fill 16, 0

slot_lfn   !fill NSLOTS, 0
slot_bank  !fill NSLOTS, 0
slot_ptrlo !fill NSLOTS, 0
slot_ptrhi !fill NSLOTS, 0
slot_lenlo !fill NSLOTS, 0
slot_lenhi !fill NSLOTS, 0

}
romfs_end

; The romfs is copied byte-exactly, but it still must not reach $a000 or it
; would be eaten by the editor text buffer the first time you edit a file.
!if romfs_end - romfs_rom > DRIVER_LIMIT {
    !error "romfs is ", romfs_end - romfs_rom, " bytes - only ", \
           DRIVER_LIMIT, " reserved below the editor buffer at $a000"
}

; ------------------------------------------------------------------
; bank 0 HI, top: reset entry.  Runs in ultimax at $ff00.
; ------------------------------------------------------------------
* = $ff00

start
    sei
    ldx #$ff
    txs
    cld
    stx $d016

    ; clear RAM the way the KERNAL reset would.  Ultimax leaves
    ; $0000-$0fff writable, so this is legal here.
    lda #0
    tay
-   sta $02,y
    sta $0200,y
    sta $0300,y
    iny
    bne -

    ; install the tape-buffer stub and hand over to it
    ldx #tramp_end - tramp_rom
-   lda tramp_rom-1,x
    sta TRAMP-1,x
    dex
    bne -

    jmp TRAMP

nmi
    rti

* = $fffa
    !word nmi           ; NMI
    !word start         ; RESET
    !word nmi           ; IRQ
