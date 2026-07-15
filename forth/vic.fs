\ vic.fs - vic-ii register name constants.
\ Pure ergonomics: named addresses for the vic-ii
\ chip so game code reads clearly.

base @ hex

d000 constant vic          \ base of vic-ii

d011 constant vic-ctrl1    \ y-scroll + display ctrl
d016 constant vic-ctrl2    \ x-scroll + display ctrl
d012 constant vic-raster   \ raster line compare/read
d018 constant vic-mem      \ screen/char memory ptrs
d019 constant vic-irq      \ irq status / acknowledge
d01a constant vic-irqmask  \ irq enable mask

d015 constant sp-enable    \ sprite enable bits
d017 constant sp-expand-y  \ sprite y-expand bits
d01d constant sp-expand-x  \ sprite x-expand bits
d01b constant sp-priority  \ sprite/background priority
d01c constant sp-multi     \ sprite multicolor bits
d01e constant sp-sp-hit    \ sprite-sprite collision
d01f constant sp-bg-hit    \ sprite-background collision
d025 constant sp-mc0       \ shared sprite multicolor 0
d026 constant sp-mc1       \ shared sprite multicolor 1

d020 constant border       \ border color
d021 constant bgcol        \ background color 0
d022 constant bgcol1       \ background color 1
d023 constant bgcol2       \ background color 2

base !
