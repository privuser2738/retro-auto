# RetroAuto - RetroArch Playlist Automation

Automatically plays through a randomized playlist of RetroArch games, perfect for showcasing your ROM collection.

## Features

- **Randomized Playlists**: Automatically scans and randomizes your ROM collection
- **Progress Tracking**: Resume where you left off with automatic progress saving
- **Random Play Time**: Each game plays for 20-60 seconds
- **GUI Popups**: Beautiful title cards show game info before each launch
- **Window Memory**: RetroArch remembers window position, size, and monitor across launches
- **Easy Controls**: Simple command-line interface

## Requirements

- Windows (64-bit)
- .NET 8.0 Runtime (or use self-contained build)
- RetroArch installed at `C:\Program Files\RetroArch\`
- Stella core for Atari 2600 (`stella_libretro.dll`)

## Quick Start

1. **First Run** - Start playing from beginning:
   ```
   RetroAuto.exe
   ```

2. **Reset Playlist** - Create new randomized order:
   ```
   RetroAuto.exe reset
   ```

3. **Continue** - Resume where you stopped:
   ```
   RetroAuto.exe continue
   ```

4. **Check Status** - See progress:
   ```
   RetroAuto.exe status
   ```

## Usage

```
RetroAuto.exe [command] [rom_directory]
```

### Commands

- `play` / `continue` - Start or resume playlist playback (default)
- `reset` - Reset and re-randomize the playlist
- `status` - Show current progress
- `help` - Show help message

### Examples

```bash
# Play with default directory (C:\Users\rob\Games\ATARI2600)
RetroAuto.exe

# Play with custom directory
RetroAuto.exe play "C:\Games\NES"

# Reset the playlist
RetroAuto.exe reset

# Check progress
RetroAuto.exe status
```

## How It Works

1. **First Run**: Scans ROM directory for `.bin` files and creates `games.txt` with randomized list
2. **Progress Tracking**: Creates `games_progress.txt` to track which games have been played
3. **Playback**:
   - Shows popup with game title and filename
   - Launches game in RetroArch with Stella core (with saved window position/size)
   - Plays for random 20-60 seconds
   - Saves RetroArch window position before closing
   - Closes and moves to next game
4. **Resume**: Progress is saved after each game, press Ctrl+C to stop anytime
5. **Window Memory**: Move RetroArch to any monitor and resize/maximize - it will remember for next launch

## Files Created

In your ROM directory:
- `games.txt` - Complete randomized playlist
- `games_progress.txt` - Remaining games (for resuming)
- `retroauto_window.json` - Saved RetroArch window position and size

## Building from Source

### Prerequisites
- .NET 8.0 SDK or later

### Build Commands

```bash
# Simple build
dotnet build

# Create release .exe (self-contained)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Or use the included batch file
build.bat
```

Output will be in: `bin\Release\net8.0-windows\win-x64\publish\RetroAuto.exe`

## Configuration

Default settings in `Program.cs`:
- ROM Directory: `C:\Users\rob\Games\ATARI2600`
- RetroArch Path: `C:\Program Files\RetroArch\retroarch.exe`
- Core: `stella` (Atari 2600)
- Play Duration: 20-60 seconds (random)
- Popup Duration: 3 seconds

## Troubleshooting

**RetroArch not found**
- Make sure RetroArch is installed at `C:\Program Files\RetroArch\`
- Or modify `DEFAULT_RETROARCH` in Program.cs

**Core not found**
- Install the Stella core in RetroArch
- In RetroArch: Online Updater → Core Downloader → Atari 2600 (Stella)

**Games not loading**
- Verify .bin files are in the ROM directory
- Check file permissions

## License

Free to use and modify.
