<div align="center">
    <h1>GHCodeSync</h1>
    <div>
        <a href="https://marketplace.visualstudio.com/items?itemName=4kk11.GHCodeSync">
            <img src="https://img.shields.io/visual-studio-marketplace/v/4kk11.GHCodeSync.svg?label=VSCode%20Marketplace&color=blue" alt="VSCode Marketplace">
        </a>
        <a href="https://www.food4rhino.com/en/app/ghcodesync">
            <img src="https://img.shields.io/badge/Food4Rhino-latest-blue" alt="Food4Rhino">
        </a>
    </div>
    <br>
    <img src="art\logo.png" alt="Logo" width="400">
</div>

## Descripción General
GHCodeSync es una herramienta de integración que permite programar los componentes de script C# de Grasshopper en VSCode.  
Utilizando una comunicación bidireccional basada en WebSocket, le permite aprovechar las potentes funciones de desarrollo de VSCode para el desarrollo de scripts de Grasshopper.

https://github.com/user-attachments/assets/22f60aaa-47c8-48e5-adde-4709fa11a6ea

## Requisitos del Sistema

- Rhinoceros: 8.18.25100.11001 o posterior
- VSCode: 1.70.0 o posterior

## Uso

### Instrucciones de Configuración

1. **Instalar Plugins**
   - Extensión de VSCode: Instale "GHCodeSync" desde el Visual Studio Code Marketplace
   - Plugin de Grasshopper: Instale "GHCodeSync" desde el McNeel PackageManager

2. **Edición de Scripts**
   - Seleccione un componente de script C# en Grasshopper
   - Haga clic en el botón "Open with VSCode" de la barra de herramientas
   - Edite el script en VSCode
   - Guarde (Ctrl+S) para sincronizar automáticamente los cambios con Grasshopper

## Licencia

Este proyecto se publica bajo la Licencia MIT. Consulte el archivo [LICENSE](LICENSE) para más detalles.
