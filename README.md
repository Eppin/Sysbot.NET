# SysBot.NET

![License](https://img.shields.io/badge/License-AGPLv3-blue.svg)

## Disclaimer

This is a fork with some new routines that I started working on since Scarlet/Violet (which is also my main focus). I am working on this project for personal purposes, because I enjoy developing in my free time (as a full-time full-stack .NET developer). While my goal is to develop a working SysBot.NET for everyone, some routines may only work for me. If you run into any issues, just let me know and I'll try to help.

If this project does not suit your needs, you are welcome to use an alternative.

## New routines

These routines are new compared to the original SysBot.NET.

#### Scarlet/Violet

- Encounters (Ruinous, Loyal Three, Gimmighoul, and static/new Paradox Pokémon in Area Zero)
- Fast egg hatching (~1,800 eggs/hour)
- Unlimited egg hatching with multiple parents (see [Documentation](./Documentation/README.md))
- Partner Mark (just run circles)
- Reset
  - Bloodmoon Ursaluna (see [Documentation](./Documentation/4.-Encounter-(Bloodmoon-Ursaluna)) for proper setup)
  - Pecharunt (see [Documentation](./Documentation/5.-Encounter-(Pecharunt)) for proper setup)
  - _Probably all other Raid-like statics_
- Overworld
  - Scanner (just walk and save)
  - Research Station
  - Mass Outbreak (search for species, list KO count, and Picnic resetting) **[Requires fork]**

#### Sword/Shield

- Max Lair (resets for IVs/Nature)
- Calyrex and Spectrier/Glastrier combined resetting (see [Documentation](./Documentation/6.-Encounter-Calyrex-and-Spectrier-Glastrier) for proper setup)
- Unlimited egg hatching with multiple parents (see [Documentation](./Documentation/README.md))

## New sys-botbase fork

Routines marked with **[Requires fork]** require you to use my fork of [sys-botbase](https://github.com/Eppin/sys-botbase/releases).  
Currently, the [latest](https://github.com/Eppin/sys-botbase/releases) version is [2.438](https://github.com/Eppin/sys-botbase/releases/tag/2.438). Please use the latest available version.

## Version compatibility

### Scarlet/Violet

| Version |                                SysBot Release                                 |                                       Egg-mod Release                                       |
| :-----: | :---------------------------------------------------------------------------: | :-----------------------------------------------------------------------------------------: |
|  4.0.0  | [25.06.03.262](https://github.com/Eppin/Sysbot.NET/releases/tag/25.06.03.262) | [3.0.0](https://github.com/Eppin/Sysbot.NET/blob/develop/Resources/Instant%20egg/3.0.0.zip) |
|  3.0.1  | [24.08.06.151](https://github.com/Eppin/Sysbot.NET/releases/tag/24.08.06.151) | [3.0.0](https://github.com/Eppin/Sysbot.NET/blob/develop/Resources/Instant%20egg/3.0.0.zip) |
|  3.0.1  | [24.05.02.134](https://github.com/Eppin/Sysbot.NET/releases/tag/24.05.02.134) | [3.0.0](https://github.com/Eppin/Sysbot.NET/blob/develop/Resources/Instant%20egg/3.0.0.zip) |
|  3.0.0  | [23.12.23.51](https://github.com/Eppin/Sysbot.NET/releases/tag/23.12.23.51)   | [3.0.0](https://github.com/Eppin/Sysbot.NET/blob/develop/Resources/Instant%20egg/3.0.0.zip) |
|  2.0.1  | [23.11.20.22](https://github.com/Eppin/Sysbot.NET/releases/tag/23.11.20.22)   | [2.0.1](https://github.com/Eppin/Sysbot.NET/blob/develop/Resources/Instant%20egg/2.0.1.zip) |

### Sword/Shield

| Version |                                SysBot Release                                 |
| :-----: | :---------------------------------------------------------------------------: |
|  1.3.2  | [24.08.06.151](https://github.com/Eppin/Sysbot.NET/releases/tag/24.08.06.151) |

## Support & Discord

For support on setting up your own instance of SysBot.NET.
Detailed guides for specific routines can be found in the [Documentation](./Documentation/README.md) folder in this repository.

- Create an issue here or send a message to **viletung** on Discord
- ~[Official Discord support server](https://discord.gg/tDMvSRv)~ (please be aware this is a fork and you might not receive support!)
- [sys-botbase](https://github.com/olliz0r/sys-botbase): client for remote control automation of Nintendo Switch consoles
- [Hybrid sys-botbase](https://github.com/Koi-3088/sys-usb-botbase): client for remote control using a hybrid connection (USB or WiFi)
- [USB-Botbase](https://github.com/Koi-3088/USB-Botbase): client for remote USB control

# Dependencies

- [.NET 9 runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
- [PKHeX](https://github.com/kwsch/PKHeX/)
- [AutoMod](https://github.com/architdate/PKHeX-Plugins/)
- [Discord.Net](https://github.com/discord-net/Discord.Net)
- [TwitchLib](https://github.com/TwitchLib/TwitchLib)
- [StreamingClientLibrary](https://github.com/SaviorXTanren/StreamingClientLibrary)

# License

Refer to the `License.md` for details regarding licensing.

# Credits

Thank you to all the open source projects and contributors. Credits are listed in no particular order:

- [@LegoFigure11](https://www.github.com/LegoFigure11) for [RaidCrawler](https://github.com/LegoFigure11/RaidCrawler)
- [@kwsch](https://www.github.com/kwsch) for the base of this bot and [PKHeX](https://github.com/kwsch/PKHeX/)
- [@Lusamine](https://github.com/Lusamine) for [moarencounterbots](https://github.com/Lusamine/SysBot.NET) fork
- [@olliz0r](https://www.github.com/olliz0r) for [sys-botbase](https://github.com/olliz0r/sys-botbase)
- [@Koi-3088](https://www.github.com/Koi-3088) for a fork of the base bot, [Hybrid sys-botbase](https://github.com/Koi-3088/sys-usb-botbase), and [USB-Botbase](https://github.com/Koi-3088/USB-Botbase)
- [@zyro670](https://www.github.com/zyro670) for [NotForkBot.NET](https://github.com/zyro670/NotForkBot.NET)
- [@Manu098vm](https://github.com/Manu098vm) for [Sys-EncounterBot.NET](https://github.com/Manu098vm/Sys-EncounterBot.NET)
- [@berichan](https://github.com/berichan) for a fork of [SysBot.NET](https://github.com/berichan/SysBot.NET)
- _Everyone else who contributed to the repositories this project uses. Did I miss you? Just let me know and you'll be added!_
