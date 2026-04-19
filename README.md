# Jellyfin Language Flags Overlay

Adds language flag overlays to Jellyfin poster and landscape images for movies and series.

The plugin reads available audio and optionally subtitle languages from media streams, maps them to country codes, and generates overlay images with flags. It can update:

- **Primary poster**
- **Landscape thumb**

Generated images are stored in Jellyfin’s metadata folder and can be registered as the active images for the item.

---

## Features

- Adds language flags to **movie** and **series** artwork
- Supports **audio language priority**
- Optional fallback to **subtitle languages** if no audio language exists
- Supports **preferred language ordering**
- Supports **maximum number of displayed flags**
- Separate settings for:
  - **Primary poster**
  - **Landscape thumb**
- Separate control for:
  - enable/disable generation
  - overwrite existing generated images
  - overlay position
  - flag size
  - margin
  - background opacity
  - anchor tuning
- Fallback image generation if no source artwork exists
- Scheduled task support
- Manual generation from plugin configuration page

---

## How it works

The plugin scans items from the Jellyfin library and collects language information from media streams.

For each item it can generate:

- `poster.langflags.png`
- `landscape.langflags.png`

These files are typically written to the Jellyfin metadata folder:

```text id="g7yqqw"
/config/data/metadata/library/<xx>/<itemid>/