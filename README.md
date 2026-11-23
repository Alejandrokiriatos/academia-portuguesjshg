# Rust Survival — Unity

Proyecto de supervivencia y construcción multijugador para Windows/Steam y Android/iOS.

## Objetivo técnico

- Unity **2022.3 LTS** (o una LTS posterior compatible con los paquetes fijados).
- Universal Render Pipeline (URP).
- Nuevo Input System: teclado/ratón, gamepad y controles táctiles.
- Netcode for GameObjects (NGO), Unity Transport, Authentication, Lobby y Relay.
- Autoridad de servidor: el cliente propone acciones; el servidor valida movimiento, recolección, inventario, supervivencia y construcción.

> Estado: base inicial del proyecto. El repositorio contiene código fuente y configuración, no un ejecutable ni una escena ya probada. Los modelos PBR finales, animaciones, audio, mapas de textura ASTC y pruebas en dispositivos se incorporarán y verificarán durante las siguientes etapas. Ningún modelo de cubo/procedural incluido se presentará como arte final.

## Estructura

```
Assets/_Game/
  Art/                 Modelos, materiales, texturas PBR y vegetación
  Audio/               Música y efectos
  Prefabs/             Jugador, recursos, construcción y UI
  Scenes/              Bootstrap y mundo
  Scripts/
    Building/          Colocación y validación de estructuras
    Core/              Arranque y configuración
    Networking/        Relay, Lobby y sincronización NGO
    Player/            Movimiento y controles
    Resources/         Recolección e inventario
    Survival/          Salud, hambre y sed
    UI/                HUD y controles táctiles
    World/             Generación de terreno y biomas
  Settings/            Recursos de renderizado y calidad
Packages/manifest.json
ProjectSettings/       Versión y ajustes de proyecto
```

## Abrir el proyecto

1. Instala **Unity Hub** y Unity Editor 2022.3 LTS con Android Build Support (SDK & NDK Tools y OpenJDK) y Windows Build Support.
2. En Unity Hub selecciona **Add > Add project from disk** y elige la carpeta clonada de este repositorio.
3. Abre con Unity 2022.3. Unity Package Manager descargará las dependencias que figuran en `Packages/manifest.json`.
4. Si Unity pregunta por el nuevo sistema de entrada, selecciona **Input System Package (New)** o **Both**, reinicia el Editor si lo solicita.
5. Abre la escena que se añadirá en `Assets/_Game/Scenes` cuando se genere el primer prototipo.

## Paquetes

Las versiones están fijadas para mantener una base reproducible en Unity 2022.3. No actualices paquetes del proyecto a ciegas: primero verifica compatibilidad y compila PC y Android.

## Controles de movimiento previstos

| Acción | PC | Móvil |
|---|---|---|
| Moverse | WASD | Joystick virtual |
| Mirar | Ratón | Arrastrar zona de mirada |
| Saltar | Espacio | Botón táctil |
| Correr | Shift | Mantener botón |
| Agacharse | Ctrl | Mantener botón |

## Multijugador y seguridad

Relay es transporte, no autoridad de juego. En esta fase el host que ejecuta NGO será la autoridad. Para un producto público de Rust-like con protección real frente a trampas, un host de jugador no equivale a un servidor dedicado: el anfitrión tiene ventaja y puede desconectarse. La arquitectura mantiene validaciones del lado del servidor para permitir migrar a servidores dedicados más adelante.

Antes de publicar:
- Configurar Project ID y entornos en Unity Dashboard.
- Activar Authentication, Lobby y Relay para el proyecto.
- Definir límites de jugadores, regiones y política de reconexión.
- Añadir persistencia de mundo, protección anticheat y moderación.

## Calidad y rendimiento

El objetivo es 60 FPS, no una garantía universal: se medirá en dispositivos reales. La iluminación global dinámica y niebla volumétrica pueden ser costosas en móviles; se usarán niveles de calidad, iluminación horneada cuando corresponda, LOD Groups, GPU instancing, occlusion culling y texturas ASTC por plataforma. Las texturas PBR finales deben incluir Albedo, Normal, AO y mapas de máscara Metallic/Smoothness según el shader URP elegido.

## Licencia y assets

No se han copiado assets comerciales de Rust. Los modelos y materiales finales serán originales o tendrán licencia verificada. Mantén las licencias de cada asset junto al recurso.
