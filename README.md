# Elfive

A Windows desktop viewer for Rockwell Automation L5X files exported from Studio 5000 / RSLogix 5000.

## Features

- **L5X parsing** — opens L5X exports and auto-detects schema versions 32–37
- **Project tree** — browse programs, routines, modules, and tags in a sidebar
- **Tag browser** — hierarchical tag viewer with Name / Value / Data Type / Description columns and live text filtering
- **Routine viewers** — dedicated views for all four IEC routine types:
  - **RLL** — Relay Ladder Logic
  - **ST** — Structured Text with syntax highlighting (AvalonEdit)
  - **FBD** — Function Block Diagram
  - **SFC** — Sequential Function Chart
- **Cross-reference** — search any controller tag to see every instruction, routine, and program that references it across all routine types (RLL, ST, FBD, SFC)
- **Tag context menu** — copy a tag name or jump straight to its cross-reference from the tag browser

## Requirements

- Windows 10/11

## Building from source

```
git clone https://github.com/JebediahMilkshake/elfive.git
cd elfive
dotnet build
```

Run the app:

```
dotnet run --project Elfive.App
```

## Usage

1. Launch Elfive.
2. **File → Open** and select an `.L5X` file exported from Studio 5000.
3. Browse programs and routines in the left-hand tree.
4. Select a routine to view its content in the right panel.
5. Click the **Cross Reference** tab, choose a scope, and type a tag name to find all usages.

## Project structure

```
Elfive.Core/          Core library — parsing and data model
  L5X/                Versioned XML deserialization (V32–V37)
  RLL/                Ladder Logic parser and layout engine
  FBD/                Function Block Diagram parser
  SFC/                Sequential Function Chart parser
  TAG/                Tag model and cross-reference index builder

Elfive.App/           WPF desktop application
  Views/              RLL, ST, FBD, SFC, and XRef user controls
  Syntax/             AvalonEdit syntax definition for Structured Text
```

## Tech stack

- .NET 10 / WPF
- [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) — Structured Text editor/viewer
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM helpers
