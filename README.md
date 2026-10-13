# ConsoleWhisper

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
  --multithread     (Default: false) Use multithread, only works for audio
                    extraction.
  --audio-index     Audio stream index (0-based) to use, for media files with
                    multiple audio streams.
  --audio-lang      Audio stream language to use (e.g. eng or en), for media
                    files with multiple audio streams.
  -y, --yes         (Default: false) Answer yes to confirmation prompts
                    (non-interactive use).
  --verbose         (Default: false) Print full exception details (stack trace)
                    on errors.
  --help            Display this help screen.
  --version         Display version information.
```

FFmpeg and the Whisper model are downloaded automatically on first use (into the program directory), no manual setup needed.

## Input patterns

`-i` accepts one or more files or wildcard patterns, separated by `,`:

| Pattern | Matches |
|---------|---------|
| `.\Friends.s01.e02.mkv` | a single file |
| `.\*.mp4` | every `.mp4` in that directory (not recursive) |
| `.\**\*.mp4` | every `.mp4` in that directory and all sub directories |
| `.\S01\?\*.mkv` | `?` matches a single character |

A duplicate file matched by several patterns is only processed once, and files are processed in a stable (sorted) order. If nothing matches at all the program exits with code `2`; a single pattern that matches nothing while others do prints a warning.

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | all files processed successfully |
| 1 | fatal error (bad arguments, download failure, ...) |
| 2 | no input file matched |
| 3 | the batch finished, but at least one file failed (see the summary at the end) |
| 130 | cancelled with Ctrl+C (temporary files are cleaned up, no partial output is left behind) |

## Examples

### Generate .srt subtitles for a whole season

```pwsh
> ConsoleWhisper.exe -i ".\Season 01\**\*.mkv" -m medium --audio-lang eng
Using audio stream #1 (language: eng | AC3 5.1 @ 640 kbps)
Extracting audio to tmp3f9k2a.wav
Start transcribing ".\Season 01\S01E01.mkv"...
Done: S01E01-mkv.srt
...
All done: 12/12 file(s) processed.
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
Please specify the extracting audio stream index (0-1): 1
Using audio stream #1 (language: eng | AC3 5.1 @ 640 kbps)
Extracting audio to tmp5b84d1.wav
Start transcribing ".\Friends.s01.e02.mkv"...
Done: Friends.s01.e02-mkv.srt
```

- neither, without an interactive terminal (script, CI, no stdin) - the program stops with an error telling you to use `--audio-index` or `--audio-lang`

### Extract soundtracks from video files

```pwsh
> ConsoleWhisper -i ".\*.mp4" --only-extract --multithread -y
Using audio stream #0 (language: und)
Extracting audio to 1-mp4.wav
Extracting audio to 2-mp4.wav
Extracting audio to 3-mp4.wav
Extracting audio to 4-mp4.wav
All done: 4/4 file(s) processed.
```

- `--only-extract` writes `<name>-<ext>.wav` (the extracted waveform, no Whisper model is downloaded)
- `--multithread` only speeds up the audio extraction; transcription still runs one file at a time
- `--multithread` disables user input, so it asks for a confirmation - `-y` / `--yes` answers it upfront (required when there is no interactive terminal)

### One failing file does not stop the batch

```pwsh
> ConsoleWhisper -i ".\*.mp4"
...
Failed: ".\broken.mp4" - Invalid file. Cannot load file ".\broken.mp4"
Finished with failures: 2 succeeded, 1 failed, 3 total.
  - ".\broken.mp4": Invalid file. Cannot load file ".\broken.mp4"
```

Add `--verbose` to see the full exception (including the stack trace) for failures.
