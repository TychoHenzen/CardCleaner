# Where the mess is in Card Cleaner

A one-page map of the repo's mess, drawn as the game's own card shop seen from above. Made on 2026-11-21 from
master at e458b795.

- `page.png` is the poster to look at. `page.svg` is the same page, editable and with its meaning tagged.
- Each yellow price tag marks one mess. Its number links the plan, the index cards around it and the cleaning order
  on the clipboard. The number is not a ranking.
- `manifest.json` says what every part means, the evidence behind it, how sure it is, and the open questions. The
  drawing takes its names, descriptions and the cleaning order from it, so change the manifest first.

The figures for `reports/` and the root leftovers were measured in a local working folder; a fresh clone has neither.
This is a snapshot: it is not kept up to date as the code changes.

## Rebuilding

The page is built with the `one-page-design` skill's scripts (Node and Playwright Chromium). `S` below is that
skill's `scripts` folder, by default `~/.claude/skills/one-page-design/scripts`; `draw/render.mjs` reads the same
folder, or `ONE_PAGE_DESIGN_SCRIPTS` when it is set. Run from this folder:

```bash
node draw/build.mjs                    # writes sections/*.svg and layout.json
node "$S/compose.mjs" layout.json page.svg
node "$S/measure.mjs" page.svg         # lint: must report ok true
node draw/render.mjs                   # writes page.png
node draw/semantic.mjs                 # optional: writes semantic.json for a machine read-back
```
