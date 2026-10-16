# Changelog

All notable changes to this project are documented here. Every released version has a `v<version>`
git tag and its own section below; the release workflow takes the section matching the tag as the
release notes.

## [1.4.0] - 2026-10-16

### Added

- **Parallel transcription**: `--multithread` now transcribes several files at once on a GPU backend
  (`clamp(cores / 4, 2, 4)` files at once, each using `cores / N` threads) in addition to the parallel
  audio extraction it already did. Batches of 3-4 files gain the most; a CPU backend and single-file
  runs stay serial, behaving exactly like before.

### Changed

- Parallel transcription prints one line per file with a `[file]` prefix and no per-segment progress
  (several files would write over each other); the summary, failure handling and exit codes are
  unchanged.

## [1.3.0] - 2026-10-16

### Added

- **GPU packages**: `CW-win-x64-gpu.zip` and `CW-linux-x64-gpu.zip` - the CPU runtime plus the CUDA
  and Vulkan runtimes. CUDA needs an NVIDIA GPU and the CUDA Toolkit >= 13.0.1 (the Runtime component
  alone is enough); Vulkan needs any Vulkan-capable driver; without a usable GPU it falls back to the
  CPU runtime silently.
- A `Runtime:` line on startup, reporting the backend that actually ran (`Cuda`, `Vulkan`, `Cpu`, ...);
  `--verbose` adds the capability text of the loaded runtime.

## [1.2.1] - 2026-10-15

### Changed

- The force-quit hint appears only when needed: the first Ctrl+C prints `Cancelling...`, and the
  "press Ctrl+C again to force quit" hint is only added if cancellation takes longer than 1.5 seconds.

## [1.2.0] - 2026-10-14

### Added

- **Wildcard batch input**: `-i` accepts files and patterns such as `.\*.mp4` or `.\**\*.mkv`,
  separated by `,`; duplicates are processed once, order is stable (sorted).
- **Audio stream selection** for files with several soundtracks: `--audio-index <n>` or
  `--audio-lang <code>`; when `--audio-lang` matches nothing a single-stream file is still processed
  (with a warning), a multi-stream file fails.

### Changed

- Output files default to the media file's directory; `--only-extract` writes `<name>-<ext>.wav`.
- A wildcard pattern matching nothing stops with an error instead of silently doing nothing.
- A failing file no longer aborts the batch: the run continues, prints a failure summary and exits
  with code 3.
