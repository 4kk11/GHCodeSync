<div align="center">
    <h1>GHCodeSync</h1>
    <div>
        <a href="https://marketplace.visualstudio.com/items?itemName=4kk11.GHCodeSync">
            <img src="https://img.shields.io/visual-studio-marketplace/v/4kk11.GHCodeSync.svg?label=VSCode%20Marketplace&color=blue" alt="VSCode Marketplace">
        </a>
        <a href="https://www.food4rhino.com/en/app/ghcodesync">
            <img src="https://img.shields.io/badge/McNeel%20Packages-latest-blue" alt="Food4Rhino">
        </a>
    </div>
    <br>
    <img src="art\logo.png" alt="Logo" width="400">
</div>

## Overview
GHCodeSync is an integration tool that enables coding Grasshopper's C# script components in VSCode.  
Using WebSocket-based bidirectional communication, it allows you to leverage VSCode's powerful development features for Grasshopper script development.

https://github.com/user-attachments/assets/22f60aaa-47c8-48e5-adde-4709fa11a6ea

## System Requirements

- Rhinoceros: 8.18.25100.11001 or later
- VSCode: 1.70.0 or later

## Usage

### Setup Instructions

1. **Install Plugins**
   - VSCode Extension: Install "GHCodeSync" from Visual Studio Code Marketplace
   - Grasshopper Plugin: Install "GHCodeSync" from McNeel PackageManager

2. **Script Editing**
   - Select a C# script component in Grasshopper
   - Click the "Open with VSCode" button from the toolbar
   - Edit the script in VSCode
   - Save (Ctrl+S) to automatically sync changes with Grasshopper

## License

This project is released under the MIT License. See the [LICENSE](LICENSE) file for details.