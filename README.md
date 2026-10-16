# ConsoleWhisper

> This repository has been developed with AI assistance since commit [ab260ec](https://github.com/azhuge233/ConsoleWhisper/commit/ab260ecda6a8d15e6a92b0184197bffb6ae68235) (v1.2.0).

Generate `.srt` subtitles for your video / audio files with [Whisper](https://github.com/ggerganov/whisper.cpp), or extract the audio track.

## Usage

```pwsh
> ConsoleWhisper.exe -i ".\*.mp4" -m small
```

Common flags (`ConsoleWhisper.exe --help` lists every option):

- `-i, --input` — files or wildcard patterns (required) · `-o, --output` — output directory (default: alongside each input file)
- `-m, --model` — Whisper model, default `small` · `-l, --language` — transcription language, default auto
- `--only-extract` — extract the audio track only, as `<name>-<ext>.wav` (no model download)
- `--multithread` — parallel mode, see [Parallel processing](#parallel-processing)
- `--audio-index` / `--audio-lang` — pick one of several audio streams
- `-y, --yes` — answer confirmation prompts upfront (non-interactive use) · `--verbose` — full exception details

FFmpeg and the Whisper model are downloaded automatically on first use (into the program directory), no manual setup needed.

## GPU acceleration

Besides the plain CPU packages, each supported platform ships a GPU package (`CW-win-x64-gpu.zip`,
`CW-linux-x64-gpu.zip`) - the CPU runtime **plus** the GPU runtimes, used the same way: the backend is
selected automatically at startup.

| Backend | Needs |
|---------|-------|
| CUDA | an NVIDIA GPU **and** the [CUDA Toolkit](https://developer.nvidia.com/cuda-downloads) >= 13.0.1 - the graphics driver alone is not enough; installing **only the Runtime component** of the Toolkit is sufficient (verified on an RTX 3060) |
| Vulkan | any GPU with a Vulkan capable driver (NVIDIA, AMD or Intel, integrated ones included) |
| CPU | nothing - the same runtime the plain packages use |

When the GPU runtime cannot be loaded the program falls back to the CPU runtime **silently** - check
the `Runtime:` line printed at startup (`Cuda`, `Vulkan`, `Cpu`, ...) first when you compare speed.
The plain packages print the same line (a CPU variant fallback shows as `CpuNoAvx`), and `--only-extract`
needs no runtime at all and prints `Runtime: not loaded`; `--verbose` adds the capability text.

Notes: the GPU package is a superset - on a machine without a usable GPU it behaves exactly like the
CPU package; `Runtime: Vulkan` on an integrated GPU is expected (the runtime only checks that the
library is present, not that the GPU is fast); the Linux GPU package is built by CI but **not verified
on hardware**.

## Parallel processing

`--multithread` runs a whole batch at once - parallel audio extraction, and parallel transcription on a GPU backend:

- transcription runs `clamp(cores / 4, 2, 4)` files at once (three of them on a 12 core machine),
  each using `cores / N` threads, `N` being that clamped count, not the number of files that happen
  to be left
- it stays one file at a time on a CPU backend (a second instance adds little there while the memory
  use doubles) and in a single-file run, which behaves exactly like the serial mode

Worth knowing before a long batch:

- **memory**: every parallel transcription holds its own model copy - `small` is about 530 MB per file, so four files need roughly 2.1 GB extra (larger models: several GB per file - use a smaller model when memory is tight)
- **integrated GPUs take the parallel path too** (the check ignores the GPU model - see
  [GPU acceleration](#gpu-acceleration)), but the gains were measured on a dedicated NVIDIA GPU
- on a 1-2 core machine the count stays at 2 (the clamp floor) with one thread each - intended, not a bug

Parallel transcription prints one line per file (file name in front, no per-segment progress - several files would write over each other); the audio extraction keeps its `[file]` prefix:

```pwsh
> ConsoleWhisper -i ".\*.mp4" --multithread -y -m small
Runtime: Cuda
[1.mp4] Start transcribing ".\1.mp4"...
[2.mp4] Start transcribing ".\2.mp4"...
[1.mp4] Done: 1-mp4.srt
All done: 2/2 file(s) transcribed.
```

`--multithread` disables user input (needs `-y` / `--yes` upfront - required without an interactive terminal); files with several soundtracks use the first one.

## Input patterns

`-i` accepts one or more files or wildcard patterns, separated by `,`:

| Pattern | Matches |
|---------|---------|
| `.\Friends.s01.e02.mkv` | a single file |
| `.\*.mp4` | every `.mp4` in that directory (not recursive) |
| `.\**\*.mp4` | every `.mp4` in that directory and all sub directories |
| `.\S01\?\*.mkv` | `?` matches a single character |

A duplicate file matched by several patterns is only processed once, files are processed in a stable (sorted) order, nothing matching at all is an error, and a single pattern matching nothing while others do prints a warning.

## Examples

### Generate .srt subtitles for a whole season

```pwsh
> ConsoleWhisper.exe -i ".\Season 01\**\*.mkv" -m medium --audio-lang eng
All done: 12/12 file(s) transcribed.
```

Subtitles are written next to each media file (`<name>-<ext>.srt`), unless `-o` says otherwise.

### Choose the audio stream of files with several soundtracks

```pwsh
> ConsoleWhisper.exe -i .\Friends.s01.e02.mkv
.\Friends.s01.e02.mkv: Multiple audio stream detected.
        - 0: language: ukr | AC3 2.0 @ 192 kbps 1+1
        - 1: language: eng | AC3 5.1 @ 640 kbps
Please specify the audio stream index to extract (0-1): 1
```

- `--audio-index <n>` - pick by that 0-based index; `--audio-lang <code>` - pick by language tag
  (`eng` and `en` both work, region suffixes like `en-US` are accepted)
- if `--audio-lang` matches nothing: a single-stream file is still processed (with a warning), a
  multi-stream file fails - use `--audio-index` for those
- neither, without an interactive terminal (script, CI, no stdin) - the program stops with an error
  telling you to use `--audio-index` or `--audio-lang`

### One failing file does not stop the batch

```pwsh
> ConsoleWhisper -i ".\*.mp4"
...
Failed: ".\broken.mp4" - Invalid file. Cannot load file ".\broken.mp4"
Finished with failures: 2 succeeded, 1 failed, 3 total.
```

Add `--verbose` to see the full exception (including the stack trace) for failures.
