# HTC Home

**HTC Home** is a desktop widget project originally created in 2010 – 2011, inspired by the iconic HTC Sense widgets from early Android smartphones.

This repository contains "remastered builds" of the original project, updated to run on modern Windows systems while preserving the original design, behavior, and spirit of the software.

This project is not under active development.
The releases published here are restored and fixed versions of the original software, made available for archival, nostalgia, and personal use.

## Version 2.x

HTC Home 2.x was built as a single host application that loads multiple widgets inside one platform.

This version focuses on:
-	extensibility
-	skinning
-	and a unified widget experience.


**Included widgets**
- Weather / Clock

Iconic Flip clock  widget with animated weather conditions. One of the key features – various animations of weather conditions, for example rain, snow or thunderstorm.

- Clock

Analog clock widgets (without weather).

- News
  
RSS / Atom news reader.

- Photos

Photo slideshow displayed as a stack of analog photos.

- Music

Controls music playback from external players
(originally supported Windows Media Player, AIMP and Winamp, in the latest release updated to use Windows 11 System Media Transport Controls).


HTC Home 2.x supports full visual skins, that can completely change the way widgets look. Many third-party skins existed in the past, some of which are sadly lost today. If you ever find old skins in the wild — preserving them would be amazing.

## Version 3.x

**HTC Home 3.x** is a later evolution where each widget is a separate executable (.exe).

This version is:
- technically more isolated,
- slightly more modern internally,
- and less dependent on a central platform.

Key differences from 2.x:
- No central widget host
- Each widget runs independently, allowing running multiple instances of each widget
- No skin system but most widgets have multiple built-in styles

Included widgets
- Clock

Clock widgets combining both Flip Clock with weather from the previous version and Analog clock.

- Weather

Weather widget without clock.

- News

RSS / Atom news reader.

- Photos
  
Photo slideshow displayed as a stack of analog photos.

## Supported systems

- Windows 11
- Windows 7, 8 and 10 may work, but were not tested. **Requires .NET Framework 4.8.**

