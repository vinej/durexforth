#!/usr/bin/env python
# efbuild.py - lay out the durexForth EasyFlash cart image.
#
# Run from the repo root.  The full build is:
#
#   echo '!pet "durexForth ef"' > build/version.asm
#   acme -I asm asm/durexforth.asm                 # -> durexforth.prg
#   for f in forth/*.fs; do m=$(basename $f .fs);  # -> build/*.pet
#       printf aa | cat - $f | petcat -text -w2 -o build/$m.pet -; done
#   printf aa | cat - forth/base-ef.fs | petcat -text -w2 -o build/base-ef.pet -
#   # pack: a d64 of durexforth.prg + base-ef.pet written AS "base" + the
#   # modules base-ef.fs includes, booted once so base-ef save-packs itself.
#   # It prints "cart: N bytes remain"; N is NEGATIVE (-893) because that is
#   # the 16K single-bank budget, which the 2-bank layout below does not use.
#   x64sc -warp -debugcart -limitcycles 2000000000 -autostart build/efpack.d64
#   c1541 -attach build/efpack.d64 -read durexforth build/durexforth-ef2
#   acme --symbollist build/cart-ef.sym asm/cart-ef.asm
#   python asm/efbuild.py
#   cartconv -t easy -i deploy/durexforth-ef.bin -o deploy/durexforth-ef.crt \
#            -n "DUREXFORTH EF"
#
# Test with:  x64sc +easyflashcrtwrite -cartcrt deploy/durexforth-ef.crt
# The +easyflashcrtwrite matters: VICE writes the emulated flash BACK over
# the .crt on exit by default, silently rewriting the file you just built.
#
# Inputs:
#   build/cart-ef.bin    assembled boot stub: the whole 8K of bank 0 HI
#                        ($e000-$ffff, reset vector at $fffc)
#   build/cart-ef.sym    acme symbol list, for the lenA/lenB/lenC patch sites
#   build/durexforth-ef2 packed durexForth (load address $0801 in first 2 bytes)
#   build/*.pet          module sources to put in the ROM filesystem
#
# Output:
#   deploy/durexforth-ef.bin   1 MB EasyFlash raw image (feed to cartconv -t easy)
#
# The stub copies its tramp to the tape buffer and the boot code to $c000, so
# the lenA/lenB/lenC words live inside the boot block at $c000+ as far as the
# symbol list is concerned.  Their offsets in the ROM image are therefore
#   (boot_rom - $e000) + (sym - $c000).
# (The resident romfs is a separate blob, copied on to $9d00 by the boot code.)
#
# EasyFlash 1MB file layout: bank N LO at N*0x4000, bank N HI at N*0x4000+0x2000.
#
# The ROM filesystem lives in the HI half-banks of banks FS_BANK.., because the
# driver reads it at $a000-$bfff where it cannot disturb durexForth ($0801-$9fff).
# Bank FS_BANK HI starts with the directory, then file data runs on, spanning
# into later banks as needed:
#   entry: namelen, name, bank, ptrlo, ptrhi, lenlo, lenhi ; namelen 0 ends it
# Names are stored uppercase-ASCII, which is what durexForth's lowercase
# PETSCII sends for "include foo".

import glob
import os
import re
import sys

STUB = "build/cart-ef.bin"
SYM = "build/cart-ef.sym"
PACKED = "build/durexforth-ef2"
PETDIR = "build"
OUT = "deploy/durexforth-ef.bin"

ROMH_BASE = 0xE000  # assembly origin of bank 0 HI
BOOT = 0xC000  # where the transient boot code runs; must match cart-ef.asm
# (the resident romfs runs at $9d00, reserved by base-ef.fs "$9cff top!", but
#  the lenA/B/C patch sites live in the boot block, hence BOOT here)
FS_BANK = 2  # first ROM filesystem bank; must match cart-ef.asm
WIN = 0x2000  # 8K per half-bank
FILE_BANK = 0x4000  # bytes per bank in the 1MB file
LOAD_ADDR = 0x0801
NBANKS = 64

SRCDIR = "forth"  # the module list comes from here, NOT from a build/*.pet glob,
# so stray scratch .pet files can never end up in the cart

# "base"/"base-ef" are the bootstrap itself - already packed into the image
SKIP = {"base-ef", "base"}


def syms():
    table = {}
    for line in open(SYM):
        m = re.match(r"\s*([A-Za-z_][A-Za-z_0-9]*)\s*=\s*\$([0-9a-fA-F]+)", line)
        if m:
            table[m.group(1)] = int(m.group(2), 16)
    return table


sym = syms()
for name in ("boot_rom", "lenA", "lenB", "lenC"):
    if name not in sym:
        sys.exit("symbol %s not found in %s (stale sym file?)" % (name, SYM))

stub = bytearray(open(STUB, "rb").read())
if len(stub) != WIN:
    sys.exit("ERROR: %s is %d bytes, expected %d (bank 0 HI must be whole)"
             % (STUB, len(stub), WIN))

packed = open(PACKED, "rb").read()
load = packed[0] | (packed[1] << 8)
if load != LOAD_ADDR:
    sys.exit("ERROR: %s loads at $%04X, expected $%04X" % (PACKED, load, LOAD_ADDR))
data = packed[2:]
total = len(data)

# chunk A -> bank 0 LO, chunk B -> bank 1 LO, chunk C -> bank 1 HI
lenA = min(WIN, total)
lenB = min(WIN, total - lenA)
lenC = total - lenA - lenB
if lenC > WIN:
    sys.exit("ERROR: image is %d bytes, %d too big for 3 half-banks"
             % (total, lenC - WIN))


def patch_word(name, val):
    off = (sym["boot_rom"] - ROMH_BASE) + (sym[name] - BOOT)
    stub[off] = val & 0xFF
    stub[off + 1] = (val >> 8) & 0xFF


patch_word("lenA", lenA)
patch_word("lenB", lenB)
patch_word("lenC", lenC)

img = bytearray(b"\xff" * (NBANKS * FILE_BANK))  # unprogrammed flash reads $ff


def place(bank, hi, payload):
    off = bank * FILE_BANK + (WIN if hi else 0)
    img[off:off + len(payload)] = payload


place(0, False, data[0:lenA])
place(0, True, bytes(stub))
place(1, False, data[lenA:lenA + lenB])
place(1, True, data[lenA + lenB:])

# ---- ROM filesystem ----

mods = []
for src in sorted(glob.glob(os.path.join(SRCDIR, "*.fs"))):
    name = os.path.basename(src)[:-3]
    if name in SKIP:
        continue
    pet = os.path.join(PETDIR, name + ".pet")
    if not os.path.exists(pet):
        sys.exit("ERROR: %s has no %s - petcat the modules first" % (src, pet))
    mods.append((name, open(pet, "rb").read()))
if not mods:
    sys.exit("ERROR: no modules found in %s/" % SRCDIR)

dirsize = sum(1 + len(n) + 5 for n, _ in mods) + 1
if dirsize > WIN:
    sys.exit("ERROR: directory is %d bytes, must fit one half-bank" % dirsize)


def fs_write(bank, off, payload):
    """Write payload to the HI half-banks, spanning banks. Returns the new pos."""
    for b in payload:
        if off >= WIN:
            bank, off = bank + 1, 0
        if bank >= NBANKS:
            sys.exit("ERROR: ROM filesystem overflows the 1MB flash")
        img[bank * FILE_BANK + WIN + off] = b
        off += 1
    return bank, off


entries = []
bank, off = FS_BANK, dirsize
for name, blob in mods:
    if off >= WIN:
        bank, off = bank + 1, 0
    entries.append((name, bank, 0xA000 + off, len(blob)))
    bank, off = fs_write(bank, off, blob)

directory = bytearray()
for name, fbank, fptr, flen in entries:
    n = name.upper().encode("ascii")  # durexForth sends this for lowercase input
    directory += bytes([len(n)]) + n + bytes(
        [fbank, fptr & 0xFF, fptr >> 8, flen & 0xFF, flen >> 8])
directory += b"\x00"
assert len(directory) == dirsize, (len(directory), dirsize)
fs_write(FS_BANK, 0, directory)

os.makedirs("deploy", exist_ok=True)
open(OUT, "wb").write(img)

fs_bytes = sum(len(b) for _, b in mods)
print("packed image     : %d bytes -> $%04X-$%04X" % (total, LOAD_ADDR, LOAD_ADDR + total - 1))
print("boot             : $%04X in ROM, runs at $%04X" % (sym["boot_rom"], BOOT))
print("romfs driver     : $%04X in ROM, resident at $9D00" % sym["romfs_rom"])
print("bank 0 LO chunk A: %5d -> $%04X" % (lenA, LOAD_ADDR))
print("bank 0 HI        : boot stub (%d bytes)" % len(stub))
print("bank 1 LO chunk B: %5d -> $%04X" % (lenB, LOAD_ADDR + lenA))
print("bank 1 HI chunk C: %5d -> $%04X" % (lenC, LOAD_ADDR + lenA + lenB))
print("romfs            : %d modules, %d bytes + %d dir, banks %d-%d HI"
      % (len(mods), fs_bytes, dirsize, FS_BANK, bank))
print("wrote %s (%d bytes)" % (OUT, len(img)))
