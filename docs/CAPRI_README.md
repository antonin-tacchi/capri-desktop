<p align="center">
  <img src="assets/chip-source.png" alt="Chip" width="40%" align="middle">
  <img src="assets/capri.png" alt="Capri" width="40%" align="middle">
</p>

<p align="center">
  Bring Capri (back lol) to life as a desktop companion! Inspired by jpartnma_'s tiktok video.
</p>

<p align="center">
  <a href="https://github.com/lachydotmcg/capri-desktop/releases/latest/download/Capri-Setup.exe"><b>Download for Windows</b></a>
  &nbsp;·&nbsp;
  <a href="#mac-and-linux-early-preview">Mac and Linux</a>
  &nbsp;·&nbsp;
  <a href="https://lachydotmcg.github.io/capri-desktop/setup/">Setup guide</a>
  &nbsp;·&nbsp;
  <a href="https://lachydotmcg.github.io/capri-desktop/">Website</a>
  &nbsp;·&nbsp;
  <a href="https://discord.gg/F4fDHfGtFk">Discord</a>
</p>

<table align="center">
  <tr>
    <td align="center"><img src="assets/readme/idle.webp" alt="Capri idling and blinking" width="150"><br><sub>Hanging out</sub></td>
    <td align="center"><img src="assets/readme/talk.webp" alt="Capri talking" width="150"><br><sub>Talking</sub></td>
    <td align="center"><img src="assets/readme/tossed.webp" alt="Capri flailing while dragged" width="150"><br><sub>Getting tossed</sub></td>
    <td align="center"><img src="assets/readme/glitch.webp" alt="Capri glitching" width="150"><br><sub>Glitching</sub></td>
    <td align="center"><img src="assets/readme/sleep.webp" alt="Capri asleep, then booting back up" width="150"><br><sub>Napping</sub></td>
  </tr>
</table>

Beware that Capri may try to take over your human CPU. We take no responsibility for her actions.

## What she does

- **Talks back.** Type to her and she answers in her own words.
- **Talks out loud.** Add an ElevenLabs key and she speaks, or voice chat with her.
- **Does things for you.** Ask her to open apps, play music or close a tab.
- **Watches you play.** Turn it on and she comments on your games.
- **Sits on your stuff.** Drop her on a window, an icon or the bottom of your screen.

<p align="center">
  <img src="assets/readme/bubble.png" alt="Capri's speech box with the pencil and wave buttons" width="31%">
  <img src="assets/readme/sit.png" alt="Capri sitting on a window" width="31%">
  <img src="assets/readme/mainframe.png" alt="The Capri Mainframe" width="22%">
</p>

## What you need

- Windows 10 or 11 (64-bit). Nothing else to install.
- A mic, if you want to voice chat.

| | Brain on your PC | Brain from a key (like the free NVIDIA one) |
| --- | --- | --- |
| Memory | 8 GB, 16 GB is better | 4 GB |
| Graphics card | Optional, but 4 GB or more makes her faster | Not needed |
| Space | 3 to 5 GB | Under 100 MB |
| Internet | Only for the download | Always |

## Getting started

1. Download **Capri-Setup.exe** and run it
2. In the Capri Mainframe, press **Set up her brain**, or press **Free key** to use a free NVIDIA key
3. Optional: paste an ElevenLabs key so she can talk. Make the key with **Restrict key** turned off

Stuck? The [setup guide](https://lachydotmcg.github.io/capri-desktop/setup/) walks through it, or ask in the [Discord](https://discord.gg/F4fDHfGtFk).

Windows or your antivirus may warn you because it's a new app. All the code is here.

## Mac and Linux (early preview)

She sits on your windows and chats. Voice, the Mainframe and doing things for you are Windows-only for now.

1. Download [Mac (Apple Silicon)](https://github.com/lachydotmcg/capri-desktop/releases/latest/download/Capri-mac-apple-silicon.zip), [Mac (Intel)](https://github.com/lachydotmcg/capri-desktop/releases/latest/download/Capri-mac-intel.zip) or [Linux](https://github.com/lachydotmcg/capri-desktop/releases/latest/download/Capri-linux-x64.tar.gz)
2. Install [Ollama](https://ollama.com), then run `ollama pull gemma4:e4b` for her brain
3. **Mac:** unzip it, drag Capri into Applications, then open Terminal and run this once. Needs macOS 15 or newer
   ```bash
   xattr -dr com.apple.quarantine /Applications/Capri.app
   ```
   Apple hasn't approved (notarized) her yet, so macOS blocks her or calls her "damaged" until you do. That line only clears the downloaded-from-the-internet flag on Capri
4. **Linux:** unpack it and run `Capri/Capri`. She sits on windows on X11; on Wayland she sits on the bottom of your screen

## How to use her

- **Pencil:** type to her
- **Wave:** voice chat
- **Drag her:** sit her on a window
- **Right-click her** or the tray icon: menu and settings

Just ask her for things, like "open Spotify", "play Blinding Lights", "pause", "what's playing?" or "close Discord".

<details>
<summary>All controls</summary>

| Control | What it does |
| --- | --- |
| Shift + drop | sit her right where you drop her |
| Ctrl + drop | she hangs on; click her to let go |
| Mic button | mute your mic in voice chat |
| Ctrl + Alt + V | start or stop voice chat (click its hint in the Mainframe to change it) |
| Ctrl + Alt + N | mute your mic (click its hint in the Mainframe to change it) |
| Ctrl + Alt + C | get her off your cursor |
| Right-click, Move | she hops between your open windows |
| Watch my screen | in the Mainframe: she comments as you play |
| FREE VOICE / ELEVENLABS | in the Mainframe under VOICE: FREE VOICE runs on your PC and costs nothing; ELEVENLABS sounds amazing and uses your ElevenLabs credits |
| Help | in the Mainframe: opens the help page |

</details>

## Coming up

- Working with your files: open, read and edit the ones you pick
- Controlling YouTube and your apps: skip, fullscreen, click, type and scroll
- A free voice that runs on your PC
- Remembering things about you
- Looking things up and telling you the answer
- Her own cursor that she moves around your screen
- Head pats (smh)
- Mischief mode: screen glitches, stealing your cursor, "you've been hacked" notes
- Mac and Linux: her voice, the Mainframe and doing things for you

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for how to build her, test her and send a pull request.

## Licence

Free for anything non-commercial: use her, share her, change her. No selling her or putting her in paid products. Full terms in [LICENSE.md](LICENSE.md) (PolyForm Noncommercial 1.0.0).

Have fun and more updates to come!

<sub>Setup guide based on Dancer's guide in the Discord. Font: Share Tech Mono (OFL). Free voice: Kokoro-82M (Apache-2.0). Her own ears: [NVIDIA Parakeet TDT 0.6B v2](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v2) ([CC-BY-4.0](https://creativecommons.org/licenses/by/4.0/)), int8 export by sherpa-onnx. Both run with [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) (Apache-2.0).</sub>
