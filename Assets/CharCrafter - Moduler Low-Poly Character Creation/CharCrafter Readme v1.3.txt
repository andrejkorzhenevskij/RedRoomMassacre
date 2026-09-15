[CharCrafter - Modular Character Customization System]
Version: 1.3
Developer: AyuoDev
Email: ayuodev@gmail.com
Discord: https://discord.gg/AqzJBWEqbz

Table of Contents

Introduction

What's New in Version 1.3

What's Included

Installation & Setup

Character Customization Guide
  5.1 General Overview
  5.2 Customize Section
  5.3 Base Model Section
  5.4 Presets Section
  5.5 Body Shape Section
  5.6 Future Sections (Currently Inactive)

Adding Your Own Materials

Changing Clothes and Colors

Undo & Redo System

Render Pipeline Conversion

System Overview (Important for Developers)

Support & Contact

License

1. Introduction
CharCrafter is a highly modular, extendable character creation tool built for Unity. It allows you to fully customize low-poly, stylized characters with modular outfits, hairstyles, gender selection, color variations, and now body shape morphing. The system is designed for quick integration, runtime use, and weekly expansion with new features and content.

2. What's New in Version 1.3
Body Morphing System with synced blendshapes

Smart Hat Compatibility

Optimized Clothing Meshes (~50% fewer vertices)

Automatic Render Pipeline Converter

Improved Randomizer

Bug Fixes & UI Tweaks

New Clothing Eras (City & Western - WIP)

3. What's Included
Complete customization scene

Male and Female base meshes

100+ modular clothing pieces in 10+ categories

Skin tones with custom color support

Multi-color material system

Body morphing with blendshapes

Randomizer, Prefab saving, Presets

Full UI system

Pipeline converter tool

Ongoing weekly updates

4. Installation & Setup
Import CharCrafter into your Unity project.

Open the CharacterCustomization scene.

Enter Play Mode to begin.

5. Character Customization Guide
5.1 General Overview

Sections: Customize, Base Model, Presets, Body Shape

Future: Settings, Import

Undo / Redo / Save buttons are always visible

5.2 Customize Section

Browse clothing categories

Select items per category

Adjust material colors

Randomize appearance

Reset appearance

Save current character as prefab

Save your own presets

Adjust body shape with sliders

Body Parts Toggle Section

Controlled Randomisation Section

5.3 Base Model Section

Switch between Male and Female with Flat Shade / Smooth Shade Mesh

Clothing list updates with gender

5.4 Presets Section

Apply pre-configured presets

Save new presets with “Save as New Preset”

5.5 Body Shape Section

9 shape sliders: chest, arms, legs, stomach, etc.

All blendshapes synced across clothing parts

Live feedback and Undo support

5.6 Future Sections (Currently Inactive)

Settings (UI and system preferences)

Import (external preset importing)

6. Adding Your Own Materials
Add your materials inside the CharacterCustomization script (Skin Colors and Color Materials arrays). They will auto-appear in the UI.

7. Changing Clothes and Colors
Use arrows to change clothing, palette icons to recolor materials (multi-materials supported), and switch skin tone at any time.

8. Undo & Redo System
Undo and Redo cover clothing changes, color edits, randomizations, and resets. Always accessible on screen.

9. Render Pipeline Conversion
Default is URP. For HDRP or Built-in:

Go to:
Window > CharCrafter > PipeLine Converter > Auto Convert Materials

Keep the original folder path intact. Tool depends on default structure.

10. System Overview (Important for Developers)
Prefab-based modular system

Clothing is identified by string names (not instance IDs)

Supports:

External preset save/load

Runtime integration

Slot/category expansion

Blendshape & material pipeline support

11. Support & Contact
Email: ayuodev@gmail.com
Discord: https://discord.gg/AqzJBWEqbz

12. License
[OK] Use in commercial and personal Unity projects
[NO] Redistribution or resale of assets is not allowed