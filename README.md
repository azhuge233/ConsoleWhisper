# ConsoleWhisper

> This repository has been developed with AI assistance since commit [ab260ec](https://github.com/azhuge233/ConsoleWhisper/commit/ab260ecda6a8d15e6a92b0184197bffb6ae68235) (v1.2.0).

Generate `.srt` subtitles for your video / audio files with [Whisper](https://github.com/ggerganov/whisper.cpp), or extract the audio track.

## Usage

```pwsh
> ConsoleWhisper --help
  -i, --input       Required. Input media files.
  -m, --model       (Default: small) Whisper model: base, tiny, small, medium,
                    large-v1, large-v2, large-v3, large-v3-turbo.
  -o, --output      (Default: media file's directory) Output directory.
  -l, --language    (Default: auto) Specify transcribe language.
  --only-extract    (Default: false) Extract audio stream only.
  --multithread     (Default: false) Use multithread: parallel audio extraction
                    and transcription (parallel transcription needs a GPU
                    runtime).
  --audio-index     (Default: none) Audio stream index (0-based) to use, for
                    media files with multiple audio streams.
  --audio-lang      (Default: none) Audio stream language to use (e.g. eng or
                    en), for media files with multiple audio streams.
  -y, --yes         (Default: false) Answer yes to confirmation prompts
                    (non-interactive use).
  --verbose         (Default: false) Print full exception details (stack trace)
                    on errors.
  --help            Display this help screen.
  --version         Display version information.
```

FFmpeg and the Whisper model are downloaded automatically on first use (into the program directory), no manual setup needed.

## GPU acceleration

Besides the plain CPU packages, each supported platform ships a GPU package (`CW-win-x64-gpu.zip`,
`CW-linux-x64-gpu.zip`). It contains the CPU runtime **plus** the GPU runtimes and needs no different
usage - the backend is selected automatically at startup:

| Backend | Needs |
|---------|-------|
| CUDA | an NVIDIA GPU **and** the [CUDA Toolkit](https://developer.nvidia.com/cuda-downloads) >= 13.0.1 (the graphics driver alone is not enough - games talk to the driver, CUDA computation needs the CUDA libraries; installing **only the Runtime component** of the Toolkit is sufficient, verified on an RTX 3060) |
| Vulkan | any GPU with a Vulkan capable driver (NVIDIA, AMD or Intel, integrated ones included) |
| CPU | nothing - the same runtime the plain packages use |

When the GPU runtime cannot be loaded the program falls back to the CPU runtime **silently**, so every
run prints the backend that actually ran - check this line first when you compare speed:

```pwsh
> ConsoleWhisper.exe -i ".\episode.mkv" -m small
Runtime: Cuda
Extracting audio...
Start transcribing ".\episode.mkv"...
Done: episode-mkv.srt
```

Add `--verbose` for the capability text of the loaded runtime. The plain packages print the same line
(it also shows a CPU variant fallback such as `CpuNoAvx`); `--only-extract` runs need no runtime at all
and print `Runtime: not loaded`.

Notes:

- the GPU package is a superset: on a machine without a usable GPU it behaves exactly like the CPU package
- `Runtime: Vulkan` on an integrated GPU is expected - the Vulkan runtime only checks that the library is present, not that the GPU is a fast one; compare the timings if in doubt
- the Linux GPU package is built by CI but has **not** been verified on hardware

## Parallel processing

`--multithread` runs a batch with several files at once:

- **audio extraction** runs in parallel for the whole batch
- **transcription** runs in parallel on a GPU backend: `clamp(cores / 4, 2, 4)` files at once (three of them on a 12 core machine), each transcription using `cores / N` threads with `N` being that clamped count, not the number of files that happen to be left. It stays one file at a time on a CPU backend (a second instance adds little there while the memory use doubles) and in a run with a single file, which behaves exactly like the serial mode

Which backend is in use is on the `Runtime:` line the program prints at startup (`Cuda`, `Vulkan`, `Cpu`, ...) - that line decides. Three things are worth knowing before a long batch:

- **memory**: every parallel transcription holds its own copy of the model - with `small` that is about 530 MB per file, so four files need roughly 2.1 GB on top of the rest. That grows with the model (with `medium` and above it is several GB per file), so a big model may not fit as many parallel files as the count allows - use a smaller model, or plain serial runs, when memory is tight
- **on a 1-2 core machine** the count stays at 2 (the clamp floor) with one thread each - intended, not a bug: the CPU-backend fallback and the cap against the file count still apply
- **integrated GPUs take the parallel path too**: the check behind it does not care about the GPU (see [GPU acceleration](#gpu-acceleration)), while the gains above the serial mode were measured on a dedicated NVIDIA GPU - on an integrated one the parallel mode may buy little, so compare a small batch if in doubt

Parallel transcription prints one line per file with the file name in front and no per-segment progress (several files would write over each other), while the audio extraction keeps its `[file]` prefix:

```pwsh
> ConsoleWhisper -i ".\*.mp4" --multithread -y -m small
Runtime: Cuda
[1.mp4] Extracting audio...
[2.mp4] Extracting audio...
[3.mp4] Extracting audio...
[1.mp4] Start transcribing ".\1.mp4"...
[2.mp4] Start transcribing ".\2.mp4"...
[3.mp4] Start transcribing ".\3.mp4"...
[1.mp4] Done: 1-mp4.srt
[3.mp4] Done: 3-mp4.srt
[2.mp4] Done: 2-mp4.srt
All done: 3/3 file(s) transcribed.
```

`--multithread` disables user input, so it asks for a confirmation - `-y` / `--yes` answers it upfront (required when there is no interactive terminal) - and media files with several soundtracks always use the first one.

## Input patterns

`-i` accepts one or more files or wildcard patterns, separated by `,`:

| Pattern | Matches |
|---------|---------|
| `.\Friends.s01.e02.mkv` | a single file |
| `.\*.mp4` | every `.mp4` in that directory (not recursive) |
| `.\**\*.mp4` | every `.mp4` in that directory and all sub directories |
| `.\S01\?\*.mkv` | `?` matches a single character |

A duplicate file matched by several patterns is only processed once, and files are processed in a stable (sorted) order. If nothing matches at all the program stops with an error instead of doing nothing; a single pattern that matches nothing while others do prints a warning.

## Examples

### Generate .srt subtitles for a whole season

```pwsh
> ConsoleWhisper.exe -i ".\Season 01\**\*.mkv" -m medium --audio-lang eng
Runtime: Cpu
Using audio stream #1 (language: eng | AC3 5.1 @ 640 kbps)
Extracting audio...
Start transcribing ".\Season 01\S01E01.mkv"...
Done: S01E01-mkv.srt
...
All done: 12/12 file(s) transcribed.
```

Subtitles are written next to each media file (`<name>-<ext>.srt`), unless `-o` says otherwise.

### Choose the audio stream of files with several soundtracks

- `--audio-index <n>` - pick by the 0-based index shown in the list below
- `--audio-lang <code>` - pick by language tag (`eng` and `en` both work, region suffixes like `en-US` are accepted)
- if `--audio-lang` matches nothing: a file with a single audio stream is still processed (with a warning), a file with several streams fails (listed in the failure summary) - use `--audio-index` for those
- neither, in an interactive terminal - you get a prompt:

```pwsh
> ConsoleWhisper.exe -i .\Friends.s01.e02.mkv
.\Friends.s01.e02.mkv: Multiple audio stream detected.
        - 0:
                language: ukr | AC3 2.0 @ 192 kbps 1+1
        - 1:
                language: eng | AC3 5.1 @ 640 kbps
Please specify the audio stream index to extract (0-1): 1
Using audio stream #1 (language: eng | AC3 5.1 @ 640 kbps)
Extracting audio...
Start transcribing ".\Friends.s01.e02.mkv"...
Done: Friends.s01.e02-mkv.srt
```

- neither, without an interactive terminal (script, CI, no stdin) - the program stops with an error telling you to use `--audio-index` or `--audio-lang`

### Extract soundtracks from video files

```pwsh
> ConsoleWhisper -i ".\*.mp4" --only-extract --multithread -y
[1.mp4] Extracting audio to 1-mp4.wav
[2.mp4] Extracting audio to 2-mp4.wav
[3.mp4] Extracting audio to 3-mp4.wav
[4.mp4] Extracting audio to 4-mp4.wav
All done: 4/4 file(s) extracted.
```

- `--only-extract` writes `<name>-<ext>.wav` (the extracted waveform, no Whisper model is downloaded)
- `--multithread` extracts the soundtracks of the whole batch in parallel - and transcribes in parallel too whenever it pays off, see [Parallel processing](#parallel-processing)

### One failing file does not stop the batch

```pwsh
> ConsoleWhisper -i ".\*.mp4"
...
Failed: ".\broken.mp4" - Invalid file. Cannot load file ".\broken.mp4"
Finished with failures: 2 succeeded, 1 failed, 3 total.
  - ".\broken.mp4"
```

Add `--verbose` to see the full exception (including the stack trace) for failures.
