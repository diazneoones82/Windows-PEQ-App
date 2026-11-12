# Windows PEQ App

Windows PEQ App is a Windows desktop audio processor. It keeps the Magic Audio/Magic acoustics work as its own tab and adds a Windows-native PEQ system ported from the `D:\opencode\PEQApp` codebase.

Source details used from <https://magic.audio/company/about/>, <https://magic.audio/web-extension-addon/>, and <https://magic.audio/web-extension-addon/real-time-demo/>:

- Magic.Audio's stated mission is to offer a superior sound experience by processing web-browser audio.
- The about page describes the founder's background in 3D audio, virtual psychoacoustic experiments, and the Precedence Effect.
- Magic.Audio describes the product as "Virtual Surround for music and movies in the web browser."
- The extension/addon page publishes Chrome and Edge extension install options.
- The real-time demo page presents the demo as part of the web extension/addon flow.

## What This Build Supports

- Windows desktop audio capture through WASAPI loopback.
- Optional processed monitor playback through the default Windows audio output. This is off by default to prevent feedback when capturing and playing through the same device.
- Built-in processed test tone so Magic/PEQ sliders and presets can be heard without creating a feedback loop.
- Tabbed interface:
  - **Magic acoustics** keeps the Magic Audio spatial signature controls.
  - **PEQ System** adds PEQApp-derived parametric EQ controls.
- PEQApp-derived Windows PEQ system:
  - PEQApp-like PEQ screen with status text, response graph, sound profiles, enhancements, band table, and import/export area.
  - Equalizer APO-style import/export.
  - PEQ-AutoEQ / AutoEq-style searchable profile list with one-click apply.
  - `.peq` JSON-style import support for PEQ-AutoEQ style configs.
  - RBJ biquad filters for `PK`, `LSC`, `HSC`, `LP`, `HP`, `NO`, `BP`, and `AP`.
  - Preamp, bass boost, loudness, stereo width, virtualizer, and reverb controls.
  - Built-in profiles including Flat, Bass Boost, Vocal, Rock, Classical, Electronic, Atmos, Cinema, Sony 360 Reality, and HD Studio.
- Live Edge extension output scanner for matching the active browser sound profile.
- Live left/right level meters for default system playback audio.
- Magic.Audio extension-derived processing path:
  - One-click Magic acoustics basic signature preset
  - ARCC-style speaker cross-talk cancellation
  - Headphones/Speakers listening-mode selector
  - AREI-style headphone binaural monitor path with 200 Hz crossover
  - Precedence-style early-reflection spatial layer
  - 3-sample cancellation delay
  - 3-band attenuation profile based on the installed Edge extension defaults
  - Default simple gain of `1.375`
  - Dynamics compression defaults based on the extension graph
- Additional desktop tuning controls:
  - Master volume
  - Bass lift
  - Treble lift
  - Stereo width
  - Virtual surround intensity
  - Dynamics compression
- Local processing preview metrics without uploading audio.

## Important Windows Audio Note

Browser extensions can directly modify tab audio because the browser owns that audio graph. A desktop app cannot replace all system audio in-place without either:

- a virtual audio device/driver, or
- routing output through a separate device.

This first app is the safe user-mode foundation: it captures desktop audio, runs the enhancement pipeline locally, and can play a processed monitor stream. Monitor playback is intended for headphones, a second output device, or virtual routing. When monitor playback is enabled on the same device being captured, Windows can feed the processed signal back into capture; the app includes a guard that turns monitor output off if that loop gets too loud. A later driver-backed or virtual-device build can route only the processed stream back as a Windows playback endpoint.

## DSP Source Notes

The installed Edge extension found on this machine is:

```text
Magic Audio 0.0.4
dhhmadpanhlgalnlopcoklmacbhfebgp
```

The extension graph includes `STEREO_WIDENING`, `ARCC`, `AREI`, `REVERB`, `SIMPLE_GAIN`, `EQ_BIQUAD`, and `DYNAMICS_COMPRESSOR`. In its default graph, `AREI` headphone surround is present but disabled, while `ARCC` speaker virtual surround is enabled. The Windows app implements the observed speaker and headphone graph behavior in C# rather than copying the bundled minified/compiled extension code verbatim.

## Build

This project is intentionally buildable on machines that have the standard .NET Framework compiler, even when the modern .NET SDK is not installed.

```powershell
.\build.ps1
```

The output executable is created at:

```text
bin\WindowsPEQApp.exe
```

## Run

```powershell
.\bin\WindowsPEQApp.exe
```

Start any system audio first, then press **Start desktop audio** in the app.

## How To Hear Changes

Browser extensions change tab audio inside the browser. This Windows app captures desktop playback and creates a processed path beside it; it cannot silently overwrite the already-playing Windows output without a virtual audio endpoint.

For a quick no-feedback check:

1. Move any Magic acoustics slider or apply a PEQ/AutoEQ preset.
2. Press **Test processed tone**.
3. Change the sliders again and press **Test processed tone** again to hear the current processed signature.

To audition live music:

1. Start music in Edge, Chrome, YouTube Music, Spotify, or another app.
2. Press **Start desktop audio**.
3. Turn on **Audition processed audio**.
4. Use headphones, a second output device, or virtual audio routing. If you audition through the same speakers being captured, Windows can feed the processed signal back into the capture stream and create noise; the app will turn audition off if that loop gets too loud.

For full desktop replacement:

1. Route Windows playback into a virtual audio cable/device.
2. Capture that virtual device in this app.
3. Play the processed output to your real speakers/headphones.

To match the extension while Edge is playing:

1. Play music in Edge with the Magic Audio extension enabled.
2. Press **Start desktop audio**.
3. Press **Scan Edge extension** and let it listen for 6 seconds.
4. The app applies the closest desktop profile from the scanned output.

## Extension Install

The Magic.Audio web page links users to Chrome and Edge store installs. Browser stores require an interactive user approval flow, so this repository does not silently install the extension. Install it from the vendor page when you want the browser-only version, and use this app when you want Windows desktop audio capture.

## License

Original project code, documentation, and assets are licensed under the GNU General Public License, version 3 or (at your option) any later version (**GPL-3.0-or-later**). You may use, copy, modify, and redistribute them under those terms. See [LICENSE](LICENSE).

This software is provided without any warranty. Third-party components, models, and assets retain their own licenses and copyright notices; this grant does not relicense them.
