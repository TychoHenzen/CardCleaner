# Private Assets Submodule

Read this when you add or change art, models or their `.import` files. Moved out of CLAUDE.md, which keeps only the
rules most tasks need. Notes about this area belong here.

All art and models live in the private repo `TychoHenzen/cardcleaner-assets`, a git submodule mounted at `Assets/` (so `res://Assets/...` resolves there). It holds the former `Graphics/` folder as `Assets/Graphics/`, the Synty files in `Assets/Synty/`, the card, terrain and material resources, and the Godot `.import` files (tracked there so UIDs stay stable). It does not use Git LFS (about 75 MB, no file over 5 MB); revisit if a single file passes 50 MB.

- Clone with `git clone --recurse-submodules`, or run `git submodule update --init` in an existing clone. An empty `Assets/` means the submodule is not initialised.
- Changing an asset is two commits: commit and push inside `Assets/` first, then commit the updated `Assets` pointer in this repo. Pushing the pointer before the submodule commit breaks CI.
- CI fetches the submodule with the read-only deploy key in the `ASSETS_DEPLOY_KEY` secret (`.github/actions/checkout-assets`). The project cannot load without `Assets/`, so the step fails when the key is unavailable; fork and Dependabot pull requests cannot read the secret and fail there.
- `Z:\Tycho\Projects\Assets` is our internal asset collection and the Asset Manager workspace. Whenever new assets are needed, look there first. Assets are added with the Asset Manager editor tab (`addons/asset_manager`): it exports to `res://Assets/<Type>/` (export roots under `[asset_manager]` in `project.godot`). Export the individual assets you need; never copy a whole source pack into the repo. The Synty packs are under `Z:\Tycho\Projects\Assets\_extracted\3d` (one `POLY_*` folder per pack, each a converted Godot project; the source FBX files of the NewYork and Megapolis packs sit in its `.godot\unidot_temp`, and several packs hold only imported `.scn` caches).
