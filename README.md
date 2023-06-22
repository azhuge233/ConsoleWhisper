# ConsoleWhisper

## Usage

```pwsh
> ConsoleWhisper --help
  -i, --input       Required. Input media files.
  -m, --model       (Default: small) Whisper model: base, tiny, small, medium, large.
  -o, --output      (Default: current directory) Output directory.
  -l, --language    (Default: auto) Specify transcribe language.
  --only-extract    (Default: false) Extract audio stream, without transcribing.
  --multithread     (Default: false) Use multithreading when extracting soundtrack.
  --help            Display this help screen.
  --version         Display version information.
```

## Example

### Generate .srt subtitle for videos

```pwsh
> ConsoleWhisper.exe -i .\Friends.s01.e02.mkv
".\Friends.s01.e02.mkv": Multiple audio stream detected.  
        - 0:  
                Language: ukr | Title: AC3 2.0 @ 192 kbps 1+1  
        - 1:  
                Language: eng | Title: AC3 5.1 @ 640 kbps  
Please specify the extracting audio stream index: 1  
Extracting audio to tmp5B84.mp3  
whisper_init_from_file_no_state: loading model from '[Application path]\Model\ggml-small.bin'  
# Whisper outputs
whisper_full_with_state: progress =   5%  
whisper_full_with_state: progress =  10%
# ...
```

### Extract soundtracks from video files

```pwsh
> ConsoleWhipser -i .\*.mp4 -o .\ --multithread --only-extract
Use multithreading will disable user input.  
If media file has multiple soundtracks, program will extract the first one, you will NOT be able to choose.  
If you are transcribing, make sure to have enough disk space to store the temp .wav file.  
Proceed? yes(y)/No(N)): y  
Extracting audio to 1-mp4.mp3  
Extracting audio to 2-mp4.mp3  
Extracting audio to 3-mp4.mp3  
Extracting audio to 4-mp4.mp3
```
